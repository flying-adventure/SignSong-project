using System.Collections;
using UnityEngine;

public enum FingerAxis
{
    X,
    Y,
    Z
}

/// <summary>
/// - Animator(Humanoid) + SignIdle 애니메이션으로 "수화 기본 자세"를 만든 뒤,
/// - 그 자세를 기준으로 손가락만 JSON 각도만큼 굽혀주는 리그.
///
/// 동작 순서:
/// 1. Animator가 SignIdle 포즈를 적용 (팔/손 위치 고정)
/// 2. 첫 프레임 끝에서 현재 손가락 회전을 "기본 자세"로 캡처
/// 3. 이후 매 프레임 FingerAngleData(JSON) 기준으로 손가락 관절 각도(굽힘)를 추가
///
/// JSON 각도 해석:
/// - Python에서 계산한 각도는 대부분 "관절 사이의 기하학적 각도"라고 가정
/// - 완전히 펴짐 ≈ 180도, 굽힐수록 값이 작아짐
/// - 그래서 실제 굽힘(bend) = 180 - rawAngle 로 계산해서 사용
/// </summary>
[RequireComponent(typeof(Animator))]
public class XBotFingerRig : MonoBehaviour
{
    [Header("지화 포즈 JSON (TextAsset)")]
    [Tooltip("Python으로 생성한 지화 포즈 JSON 파일 (FingerAngleData 구조)")]
    public TextAsset gestureJson;

    [Header("적용 옵션")]
    [Tooltip("기본 포즈(Base Pose) 캡처 후, 바로 한 번 포즈를 적용할지 여부")]
    public bool applyOnCapture = true;

    [Tooltip("LateUpdate마다 계속 포즈를 유지할지 여부 (Animator 덮어쓰기 방지용)")]
    public bool applyEveryFrame = true;

    [Tooltip("굽힘 각도 배율 (1.0 = 그대로, 2.0 = 두 배로 더 많이 굽힘)")]
    public float angleMultiplier = 1.0f;

    [Tooltip("관절 하나가 최대 얼마나 굽힐 수 있는지 (deg)")]
    [Range(0f, 180f)]
    public float maxBend = 90f;

    [Tooltip("손가락을 어느 축을 기준으로 굽힐지 선택 (X/Y/Z 중 하나)")]
    public FingerAxis rotationAxis = FingerAxis.X;

    private Animator animator;
    private FingerAngleData pose;   // JSON에서 파싱된 지화 포즈 데이터

    // 오른손 손가락 본들 (Humanoid 기준)
    private Transform rThumb1, rThumb2, rThumb3;
    private Transform rIndex1, rIndex2, rIndex3;
    private Transform rMiddle1, rMiddle2, rMiddle3;
    private Transform rRing1,  rRing2,  rRing3;
    private Transform rPinky1, rPinky2, rPinky3;

    // "수화 기본 자세"에서의 로컬 회전 값
    private Quaternion rThumb1Base, rThumb2Base, rThumb3Base;
    private Quaternion rIndex1Base, rIndex2Base, rIndex3Base;
    private Quaternion rMiddle1Base, rMiddle2Base, rMiddle3Base;
    private Quaternion rRing1Base,  rRing2Base,  rRing3Base;
    private Quaternion rPinky1Base, rPinky2Base, rPinky3Base;

    private bool basePoseCaptured = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            Debug.LogError("[XBotFingerRig] Humanoid Avatar가 유효하지 않습니다. XBot Rig 설정을 확인하세요.");
            enabled = false;
            return;
        }

        // Humanoid 오른손 손가락 본 자동으로 찾기
        rThumb1 = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
        rThumb2 = animator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate);
        rThumb3 = animator.GetBoneTransform(HumanBodyBones.RightThumbDistal);

        rIndex1 = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        rIndex2 = animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
        rIndex3 = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);

        rMiddle1 = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        rMiddle2 = animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
        rMiddle3 = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal);

        rRing1 = animator.GetBoneTransform(HumanBodyBones.RightRingProximal);
        rRing2 = animator.GetBoneTransform(HumanBodyBones.RightRingIntermediate);
        rRing3 = animator.GetBoneTransform(HumanBodyBones.RightRingDistal);

        rPinky1 = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        rPinky2 = animator.GetBoneTransform(HumanBodyBones.RightLittleIntermediate);
        rPinky3 = animator.GetBoneTransform(HumanBodyBones.RightLittleDistal);

        if (rIndex1 == null)
        {
            Debug.LogError("[XBotFingerRig] 오른손 손가락 본들을 찾지 못했습니다. XBot Humanoid 설정을 확인하세요.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        // OnEnable 시점에 코루틴 시작 → Animator가 포즈를 적용한 다음 프레임에 베이스 포즈 캡처
        StartCoroutine(CaptureBasePoseCoroutine());
    }

    private IEnumerator CaptureBasePoseCoroutine()
    {
        // 1프레임 기다려서 Animator가 SignIdle 등 기본 애니메이션을 적용할 시간을 줌
        yield return null;

        // JSON 파싱 (이 시점에서 해도 됨)
        if (gestureJson != null && pose == null)
        {
            pose = JsonUtility.FromJson<FingerAngleData>(gestureJson.text);
            if (pose == null || pose.finger_angles == null)
            {
                Debug.LogError("[XBotFingerRig] gestureJson 파싱 실패 또는 finger_angles 가 null 입니다.");
            }
            else
            {
                Debug.Log($"[XBotFingerRig] 지화 포즈 로드 완료: {pose.gesture_name}");
            }
        }
        else if (gestureJson == null)
        {
            Debug.LogWarning("[XBotFingerRig] gestureJson 이 비어 있습니다. Inspector에서 TextAsset를 연결하세요.");
        }

        // 지금 상태(Animator가 만든 수화 기본 자세)를 기준 포즈로 캡처
        CaptureCurrentAsBasePose();
        basePoseCaptured = true;

        Debug.Log("[XBotFingerRig] 수화 기본 자세(Base Pose) 캡처 완료");

        if (applyOnCapture && pose != null)
        {
            ApplyPose();
        }
    }

    private void CaptureCurrentAsBasePose()
    {
        SaveBase(rThumb1, ref rThumb1Base);
        SaveBase(rThumb2, ref rThumb2Base);
        SaveBase(rThumb3, ref rThumb3Base);

        SaveBase(rIndex1, ref rIndex1Base);
        SaveBase(rIndex2, ref rIndex2Base);
        SaveBase(rIndex3, ref rIndex3Base);

        SaveBase(rMiddle1, ref rMiddle1Base);
        SaveBase(rMiddle2, ref rMiddle2Base);
        SaveBase(rMiddle3, ref rMiddle3Base);

        SaveBase(rRing1, ref rRing1Base);
        SaveBase(rRing2, ref rRing2Base);
        SaveBase(rRing3, ref rRing3Base);

        SaveBase(rPinky1, ref rPinky1Base);
        SaveBase(rPinky2, ref rPinky2Base);
        SaveBase(rPinky3, ref rPinky3Base);
    }

    private void SaveBase(Transform t, ref Quaternion q)
    {
        if (t != null)
            q = t.localRotation;
    }

    private Vector3 GetAxis()
    {
        switch (rotationAxis)
        {
            case FingerAxis.Y:
                return Vector3.up;
            case FingerAxis.Z:
                return Vector3.forward;
            case FingerAxis.X:
            default:
                return Vector3.right;
        }
    }

    private void LateUpdate()
    {
        if (!basePoseCaptured)
            return;

        if (applyEveryFrame && pose != null)
        {
            ApplyPose();
        }
    }

    /// <summary>
    /// 현재 basePose + JSON 각도를 기준으로 오른손 손가락 전체에 포즈 적용.
    /// </summary>
    private void ApplyPose()
    {
        if (pose == null || pose.finger_angles == null)
            return;

        Vector3 axis = GetAxis();

        // 엄지
        SetFinger(rThumb1, rThumb1Base, rThumb2, rThumb2Base, rThumb3, rThumb3Base, pose.finger_angles.thumb, axis);
        // 검지
        SetFinger(rIndex1, rIndex1Base, rIndex2, rIndex2Base, rIndex3, rIndex3Base, pose.finger_angles.index, axis);
        // 중지
        SetFinger(rMiddle1, rMiddle1Base, rMiddle2, rMiddle2Base, rMiddle3, rMiddle3Base, pose.finger_angles.middle, axis);
        // 약지
        SetFinger(rRing1, rRing1Base, rRing2, rRing2Base, rRing3, rRing3Base, pose.finger_angles.ring, axis);
        // 새끼
        SetFinger(rPinky1, rPinky1Base, rPinky2, rPinky2Base, rPinky3, rPinky3Base, pose.finger_angles.pinky, axis);
    }

    /// <summary>
    /// angles 배열(rawAngle들)을
    ///   - rawAngle: 관절 기하학 각도 (완전 펴짐 ≈ 180, 굽을수록 작아짐)
    ///   - bend = 180 - rawAngle 로 굽힘 각도로 바꿔서
    /// basePose에서 그만큼만 꺾어주는 함수.
    /// </summary>
    private void SetFinger(
        Transform j1, Quaternion j1Base,
        Transform j2, Quaternion j2Base,
        Transform j3, Quaternion j3Base,
        float[] angles,
        Vector3 axis)
    {
        if (j1 == null || j2 == null || j3 == null)
            return;

        if (angles == null || angles.Length < 3)
            return;

        // 1) Python에서 계산한 각도 (안전하게 0~180으로 클램프)
        float raw0 = Mathf.Clamp(angles[0], 0f, 180f);
        float raw1 = Mathf.Clamp(angles[1], 0f, 180f);
        float raw2 = Mathf.Clamp(angles[2], 0f, 180f);

        // 2) "굽힘 정도"로 해석: 180 = 완전 펴짐, 0 = 완전 굽힘 → bend = 180 - raw
        float bend0 = 180f - raw0;
        float bend1 = 180f - raw1;
        float bend2 = 180f - raw2;

        // 3) 배율 적용 + 최대 굽힘 제한
        bend0 = Mathf.Clamp(bend0 * angleMultiplier, 0f, maxBend);
        bend1 = Mathf.Clamp(bend1 * angleMultiplier, 0f, maxBend);
        bend2 = Mathf.Clamp(bend2 * angleMultiplier, 0f, maxBend);

        // 4) 베이스 포즈에서 굽힘만 추가
        j1.localRotation = j1Base * Quaternion.AngleAxis(bend0, axis);
        j2.localRotation = j2Base * Quaternion.AngleAxis(bend1, axis);
        j3.localRotation = j3Base * Quaternion.AngleAxis(bend2, axis);

        // 만약 반대 방향으로 꺾이면 위 AngleAxis 쪽에 -bend 를 넣어서 방향만 뒤집어보면 됨:
        // j1.localRotation = j1Base * Quaternion.AngleAxis(-bend0, axis);
    }
}