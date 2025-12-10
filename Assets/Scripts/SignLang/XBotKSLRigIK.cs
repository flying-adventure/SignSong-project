using UnityEngine;

/// <summary>
/// OpenPose 3D keypoint + Unity Animator IK를 이용해서
/// - 상체(척추/가슴/목/머리) 회전
/// - 오른손/왼손 위치/회전을 IK로 제어
///
/// 특징:
/// - 상체 정면은 처음 바라본 방향을 유지 (yaw 고정)
/// - 팔은 직접 회전하지 않고, IK Position/Rotation만 설정
/// - 손 목표 위치는 가슴 앞 평면 + 좌우 영역으로 클램프해서
///   팔이 몸 뒤로 넘어가거나 서로 교차하는 것을 줄임
///
/// 사용법:
/// 1) XBot에 이 스크립트와 OpenPoseHandLoader 둘 다 붙이기
/// 2) Animator → 해당 레이어(대부분 Base Layer)의 "IK Pass" 체크
/// 3) loader 필드에 OpenPoseHandLoader 연결
/// 4) SignPlayer가 loader.LoadFromJsonText()를 매 프레임 호출하면
///    → OnAnimatorIK에서 자동으로 포즈 적용
/// </summary>
[RequireComponent(typeof(Animator))]
public class XBotKSLRigIK : MonoBehaviour
{
    [Header("OpenPose 3D 데이터를 제공하는 Loader (XBot에 붙어 있는 컴포넌트)")]
    public OpenPoseHandLoader loader;

    [Header("좌표계 설정")]
    [Tooltip("데이터셋에 따라 Z축 방향이 반대일 수 있으므로, 필요하면 -1로 바꿔서 테스트")]
    public float zSign = -1f; // 팔이 등 뒤로 가면 1 ↔ -1 바꿔보면 됨

    [Header("상체 보간 강도 (0~1)")]
    [Range(0f, 1f)] public float upperBodyLerp = 0.3f;

    [Header("관절 각도 제한")]
    [Tooltip("척추/가슴이 초기자세에서 최대 몇 도까지 기울어질지")]
    [Range(0f, 90f)] public float maxTorsoAngle = 45f;

    [Tooltip("머리(Head)가 초기자세에서 최대 몇 도까지 돌 수 있을지")]
    [Range(0f, 90f)] public float maxHeadAngle = 45f;

    [Header("IK 설정")]
    [Range(0f, 1f)] public float rightHandIKWeight = 1.0f;
    [Range(0f, 1f)] public float leftHandIKWeight  = 1.0f;

    [Tooltip("손목 회전이 IK에 어느 정도 반영될지 (0~1)")]
    [Range(0f, 1f)] public float handRotationWeight = 0.8f;

    [Header("팔/손이 몸 뒤로 가지 않게 하기")]
    [Tooltip("손/팔 목표 위치가 가슴 뒤로 가면 가슴 앞 평면으로 당겨오기")]
    public bool keepHandsInFront = true;

    [Tooltip("가슴 평면 기준으로 손이 최소 얼마만큼은 앞에 있어야 하는지 (m)")]
    [Range(0.01f, 0.3f)]
    public float minChestForward = 0.08f;

    [Header("양손이 서로 겹치지 않게 하기")]
    [Tooltip("왼손/오른손이 몸 중심선에서 최소 얼마나 떨어져 있어야 하는지 (m)")]
    [Range(0.01f, 0.3f)]
    public float minHandSideOffset = 0.10f;   // 10cm 정도

    [Header("손 위치 스케일 (OpenPose 좌표 → Unity 월드좌표)")]
    [Tooltip("OpenPose 3D 단위를 Unity 월드 스케일과 맞추기 위한 스케일 팩터")]
    public float positionScale = 0.5f;

    // --- 본들 ---
    private Animator animator;

    // 상체 본
    private Transform spine;      // Spine
    private Transform chest;      // Chest or UpperChest
    private Transform neck;       // Neck
    private Transform head;       // Head

    // 초기 로컬 회전값 (기준 포즈)
    private Quaternion spineInitialLocalRot;
    private Quaternion chestInitialLocalRot;
    private Quaternion neckInitialLocalRot;
    private Quaternion headInitialLocalRot;

    // 상체가 "원래 바라보던 정면 방향" (월드 좌표)
    private Vector3 initialTorsoForward;

    // OpenPose BODY_25 인덱스
    private const int BODY_NOSE       = 0;
    private const int BODY_NECK       = 1;
    private const int BODY_R_SHOULDER = 2;
    private const int BODY_R_ELBOW    = 3;
    private const int BODY_R_WRIST    = 4;
    private const int BODY_L_SHOULDER = 5;
    private const int BODY_L_ELBOW    = 6;
    private const int BODY_L_WRIST    = 7;
    private const int BODY_MID_HIP    = 8;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[XBotKSLRigIK] Animator가 없습니다. XBot에 이 스크립트를 붙였는지 확인하세요.");
            enabled = false;
            return;
        }

        if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            Debug.LogError("[XBotKSLRigIK] Humanoid Avatar가 유효하지 않습니다. XBot 모델의 Rig을 Humanoid로 설정하고 Avatar를 연결하세요.");
            enabled = false;
            return;
        }

        // 상체 본 찾기
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null)
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        neck  = animator.GetBoneTransform(HumanBodyBones.Neck);
        head  = animator.GetBoneTransform(HumanBodyBones.Head);

        if (spine == null && chest == null)
        {
            Debug.LogWarning("[XBotKSLRigIK] Spine/Chest 본을 찾지 못했습니다. 상체 회전은 제한됩니다.");
        }

        // 초기 로컬 회전값 저장
        if (spine != null) spineInitialLocalRot = spine.localRotation;
        if (chest != null) chestInitialLocalRot = chest.localRotation;
        if (neck  != null) neckInitialLocalRot  = neck.localRotation;
        if (head  != null) headInitialLocalRot  = head.localRotation;

        // "정면" 기준은 Chest가 있으면 그 forward, 없으면 Spine, 그것도 없으면 전체 transform.forward
        Transform torsoRef = chest != null ? chest : (spine != null ? spine : transform);
        initialTorsoForward = torsoRef.forward;

        Debug.Log("[XBotKSLRigIK] 초기화 완료. 상체 본 연결됨. 정면 방향 고정 + IK 준비 완료.");
    }

    /// <summary>
    /// Unity Animator가 IK를 계산할 때 호출됨.
    /// 여기서:
    /// - 상체 회전(척추/가슴/목/머리)을 적용하고
    /// - 오른손/왼손에 IK Position/Rotation을 설정
    /// </summary>
    private void OnAnimatorIK(int layerIndex)
    {
        if (loader == null || loader.body3D == null)
            return;

        // 1) 상체(Spine/Chest/Neck/Head) 회전 먼저 적용
        ApplyUpperBody();

        // 2) 손 IK 타겟 계산
        ApplyRightHandIK();
        ApplyLeftHandIK();
    }

    // ===================== 상체(척추/가슴/목/머리) =====================

    private void ApplyUpperBody()
    {
        var body = loader.body3D;
        if (body == null || body.Length <= BODY_MID_HIP)
            return;

        // OpenPose → Unity 좌표로 변환 (스케일 포함)
        Vector3 neckPos     = MapToUnity(body[BODY_NECK]);
        Vector3 midHipPos   = MapToUnity(body[BODY_MID_HIP]);
        Vector3 rShoulder   = MapToUnity(body[BODY_R_SHOULDER]);
        Vector3 lShoulder   = MapToUnity(body[BODY_L_SHOULDER]);
        Vector3 shoulderMid = (rShoulder + lShoulder) * 0.5f;

        // 상체 up 방향: 골반→어깨 중심
        Vector3 torsoUp = (shoulderMid - midHipPos);
        if (torsoUp.sqrMagnitude < 1e-6f)
            torsoUp = Vector3.up;
        else
            torsoUp.Normalize();

        // (중요) 정면 방향은 "처음 봤던 방향"을 계속 사용 → yaw(좌우 회전) 고정
        Vector3 torsoForward = initialTorsoForward.normalized;

        // 오른쪽 방향은 forward x up 으로 재계산
        Vector3 torsoRight = Vector3.Cross(torsoForward, torsoUp);
        if (torsoRight.sqrMagnitude < 1e-6f)
            torsoRight = Vector3.right;
        else
            torsoRight.Normalize();

        // up 방향 다시 정규화
        torsoUp = Vector3.Cross(torsoRight, torsoForward);
        torsoUp.Normalize();

        // 상체 전체의 월드 회전 (정면은 고정, 기울기만 반영)
        Quaternion torsoWorldRot = Quaternion.LookRotation(torsoForward, torsoUp);

        // Spine / Chest 에 적용
        if (spine != null)
            ApplyBoneWithClamp(spine, spineInitialLocalRot, torsoWorldRot, upperBodyLerp, maxTorsoAngle);

        if (chest != null)
            ApplyBoneWithClamp(chest, chestInitialLocalRot, torsoWorldRot, upperBodyLerp, maxTorsoAngle);

        // 목/머리: "정면 유지 + 고개만 약간" 컨셉
        if ((neck != null || head != null) && body.Length > BODY_NOSE)
        {
            Vector3 nosePos = MapToUnity(body[BODY_NOSE]);
            Vector3 headDirRaw = nosePos - neckPos;
            if (headDirRaw.sqrMagnitude > 1e-6f)
            {
                headDirRaw.Normalize();

                Vector3 headForward = initialTorsoForward.normalized;
                Vector3 headUp      = torsoUp;

                Quaternion headWorldRot = Quaternion.LookRotation(headForward, headUp);

                if (neck != null)
                    ApplyBoneWithClamp(neck, neckInitialLocalRot, headWorldRot, upperBodyLerp, maxHeadAngle);

                if (head != null)
                    ApplyBoneWithClamp(head, headInitialLocalRot, headWorldRot, upperBodyLerp, maxHeadAngle);
            }
        }
    }

    // 지정된 본에 대해:
    //   - 부모 기준 targetWorldRot을 로컬로 변환
    //   - 초기 로컬 회전에서 maxAngle 안쪽으로만 회전하도록 각도 클램프
    //   - 현재 로컬에서 보간(lerp)해서 적용
    private void ApplyBoneWithClamp(
        Transform bone,
        Quaternion initialLocal,
        Quaternion targetWorldRot,
        float lerp,
        float maxAngle
    )
    {
        if (bone == null || bone.parent == null)
            return;

        Transform parent = bone.parent;

        Quaternion targetLocal = Quaternion.Inverse(parent.rotation) * targetWorldRot;

        Quaternion delta = targetLocal * Quaternion.Inverse(initialLocal);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z))
            return;

        angle = Mathf.Min(angle, maxAngle);

        Quaternion clampedLocal = Quaternion.AngleAxis(angle, axis) * initialLocal;

        bone.localRotation = Quaternion.Slerp(
            bone.localRotation,
            clampedLocal,
            lerp
        );
    }

    // ===================== 오른손 IK =====================

    private void ApplyRightHandIK()
    {
        if (rightHandIKWeight <= 0f)
            return;

        var hand = loader.rightHand3D;
        if (hand == null || hand.Length < 9) // 0: wrist, 8: index tip
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        // OpenPose → Unity 월드좌표 (스케일 포함)
        Vector3 wrist    = MapToUnity(hand[0]);
        Vector3 indexTip = MapToUnity(hand[8]);

        // XBot 기준 월드 위치로 맞추기:
        // OpenPose 좌표를 "로컬 공간"이라고 보고, XBot의 현재 위치/회전을 기준으로 변환
        wrist    = transform.TransformPoint(wrist    * positionScale);
        indexTip = transform.TransformPoint(indexTip * positionScale);

        // 오른손은 몸의 오른쪽 + 앞쪽 영역으로 클램프
        wrist    = ClampToFrontOfChest(wrist, true);
        indexTip = ClampToFrontOfChest(indexTip, true);

        Vector3 dir = indexTip - wrist;
        if (dir.sqrMagnitude < 1e-6f)
            dir = transform.forward;
        else
            dir.Normalize();

        Quaternion handWorldRot = Quaternion.LookRotation(dir, transform.up);

        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandIKWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, handRotationWeight);

        animator.SetIKPosition(AvatarIKGoal.RightHand, wrist);
        animator.SetIKRotation(AvatarIKGoal.RightHand, handWorldRot);
    }

    // ===================== 왼손 IK =====================

    private void ApplyLeftHandIK()
    {
        if (leftHandIKWeight <= 0f)
            return;

        var hand = loader.leftHand3D;
        if (hand == null || hand.Length < 9)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            return;
        }

        Vector3 wrist    = MapToUnity(hand[0]);
        Vector3 indexTip = MapToUnity(hand[8]);

        wrist    = transform.TransformPoint(wrist    * positionScale);
        indexTip = transform.TransformPoint(indexTip * positionScale);

        // 왼손은 몸의 왼쪽 + 앞쪽 영역으로 클램프
        wrist    = ClampToFrontOfChest(wrist, false);
        indexTip = ClampToFrontOfChest(indexTip, false);

        Vector3 dir = indexTip - wrist;
        if (dir.sqrMagnitude < 1e-6f)
            dir = transform.forward;
        else
            dir.Normalize();

        Quaternion handWorldRot = Quaternion.LookRotation(dir, transform.up);

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandIKWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, handRotationWeight);

        animator.SetIKPosition(AvatarIKGoal.LeftHand, wrist);
        animator.SetIKRotation(AvatarIKGoal.LeftHand, handWorldRot);
    }

    // ===================== 공통 유틸 =====================

    /// <summary>
    /// 손/손목 목표 위치가 몸(가슴) 뒤로 가거나,
    /// 왼손/오른손이 서로 반대쪽 공간으로 넘어가는 걸 막아주는 클램프.
    ///
    /// isRightSide:
    ///   - true  → 오른쪽 손/팔 (몸의 오른쪽 반쪽 공간)
    ///   - false → 왼쪽 손/팔  (몸의 왼쪽 반쪽 공간)
    /// </summary>
    private Vector3 ClampToFrontOfChest(Vector3 worldPos, bool isRightSide)
    {
        if (!keepHandsInFront)
            return worldPos;

        // 가슴 본 기준 사용, 없으면 spine 사용
        Transform refBone = chest != null ? chest : spine;
        if (refBone == null)
            return worldPos;

        Vector3 chestPos     = refBone.position;
        Vector3 chestForward = refBone.forward.normalized;
        Vector3 chestRight   = refBone.right.normalized;
        Vector3 chestUp      = Vector3.Cross(chestRight, chestForward).normalized;

        // 월드 위치 → 가슴 기준 방향 벡터
        Vector3 dir = worldPos - chestPos;

        // 각 축 방향 성분
        float forwardDot = Vector3.Dot(dir, chestForward);
        float sideDot    = Vector3.Dot(dir, chestRight);
        float upDot      = Vector3.Dot(dir, chestUp);

        // 1) 앞/뒤 클램프: 항상 가슴 앞쪽으로 (minChestForward 이상)
        if (forwardDot < minChestForward)
            forwardDot = minChestForward;

        // 2) 좌/우 클램프: 왼손/오른손이 서로 반대편으로 못 넘어가게
        if (isRightSide)
        {
            // 오른손은 몸의 오른쪽 공간(+) 안에서만
            if (sideDot < minHandSideOffset)
                sideDot = minHandSideOffset;
        }
        else
        {
            // 왼손은 몸의 왼쪽 공간(-) 안에서만
            if (sideDot > -minHandSideOffset)
                sideDot = -minHandSideOffset;
        }

        // 방향 벡터 재조합
        Vector3 clampedDir =
            chestForward * forwardDot +
            chestRight   * sideDot +
            chestUp      * upDot;

        return chestPos + clampedDir;
    }

    /// <summary>
    /// OpenPose 3D 좌표계를 Unity용으로 변환.
    /// OpenPose: x 오른쪽, y 아래, z 카메라 밖 방향(추정)
    /// Unity:    x 오른쪽, y 위,   z 앞
    /// + positionScale로 스케일 조정은 본문에서 TransformPoint할 때 처리
    /// </summary>
    private Vector3 MapToUnity(Vector3 p)
    {
        return new Vector3(p.x, -p.y, zSign * p.z);
    }
}