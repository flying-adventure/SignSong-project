using UnityEngine;

/// <summary>
/// NIA OpenPose 3D keypoint를 이용해서
/// XBot의 상반신 + 양팔(어깨/팔꿈치) + 양손목을 움직이는 리깅 스크립트.
///
/// 포함되는 것:
/// - BODY_25 3D → 척추/가슴(Spine/Chest) 회전 (정면 유지 + 기울기만 반영)
/// - BODY_25 3D → 목/머리(Neck/Head) 회전 (정면 유지 + 고개만 살짝)
/// - BODY_25 3D → 양쪽 팔(UpperArm / LowerArm) 회전
/// - Hand 3D → 양손 손목 회전
/// - 손/팔이 몸(가슴) 뒤로 넘어가지 않도록 '앞쪽 평면'으로 클램프
/// - 양손이 서로 반대쪽 공간으로 넘어가지 않도록 좌/우 분리
/// - 손목/상체/머리 회전은 초기 포즈 기준에서 각도 제한
/// </summary>
[RequireComponent(typeof(Animator))]
public class XBotKSLRig : MonoBehaviour
{
    [Header("OpenPose 3D 데이터를 제공하는 Loader (XBot에 붙어 있는 컴포넌트)")]
    public OpenPoseHandLoader loader;

    [Header("좌표계 설정")]
    [Tooltip("데이터셋에 따라 Z축 방향이 반대일 수 있으므로, 필요하면 -1로 바꿔서 테스트")]
    public float zSign = -1f; // 팔이 등 뒤로 가면 1 ↔ -1 바꿔보면 됨

    [Header("상체 보간 강도 (0~1)")]
    [Range(0f, 1f)] public float upperBodyLerp = 0.3f;

    [Header("팔/손 보간 강도 (0~1)")]
    [Range(0f, 1f)] public float limbLerp = 0.4f;

    [Header("관절 각도 제한")]
    [Tooltip("척추/가슴이 초기자세에서 최대 몇 도까지 기울어질지")]
    [Range(0f, 90f)] public float maxTorsoAngle = 45f;

    [Tooltip("머리(Head)가 초기자세에서 최대 몇 도까지 돌 수 있을지")]
    [Range(0f, 90f)] public float maxHeadAngle = 45f;

    [Tooltip("손목이 초기자세에서 최대 몇 도까지 꺾일지")]
    [Range(0f, 180f)] public float maxWristAngle = 60f;

    [Tooltip("팔(UpperArm/LowerArm)이 초기자세에서 최대 몇 도까지 회전할지")]
    [Range(0f, 180f)] public float maxArmAngle = 120f;   // 새로 추가

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

    // --- 본들 ---
    private Animator animator;

    // 상체 본
    private Transform spine;      // Spine
    private Transform chest;      // Chest or UpperChest
    private Transform neck;       // Neck
    private Transform head;       // Head

    // 오른팔
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform rightHand;

    // 왼팔
    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform leftHand;

    // 초기 로컬 회전값 (기준 포즈)
    private Quaternion spineInitialLocalRot;
    private Quaternion chestInitialLocalRot;
    private Quaternion neckInitialLocalRot;
    private Quaternion headInitialLocalRot;

    private Quaternion rightUpperArmInitialLocalRot;
    private Quaternion rightLowerArmInitialLocalRot;
    private Quaternion rightHandInitialLocalRot;

    private Quaternion leftUpperArmInitialLocalRot;
    private Quaternion leftLowerArmInitialLocalRot;
    private Quaternion leftHandInitialLocalRot;

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
            Debug.LogError("[XBotKSLRig] Animator가 없습니다. XBot에 이 스크립트를 붙였는지 확인하세요.");
            enabled = false;
            return;
        }

        if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            Debug.LogError("[XBotKSLRig] Humanoid Avatar가 유효하지 않습니다. XBot 모델의 Rig을 Humanoid로 설정하고 Avatar를 연결하세요.");
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

        // 팔 본 찾기
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand     = animator.GetBoneTransform(HumanBodyBones.RightHand);

        leftUpperArm  = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArm  = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand      = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        if (rightUpperArm == null || rightLowerArm == null || rightHand == null)
        {
            Debug.LogError("[XBotKSLRig] 오른팔 본(UpperArm/LowerArm/Hand)을 찾지 못했습니다.");
            enabled = false;
            return;
        }

        if (leftUpperArm == null || leftLowerArm == null || leftHand == null)
        {
            Debug.LogWarning("[XBotKSLRig] 왼팔 본(UpperArm/LowerArm/Hand)을 찾지 못했습니다. 왼팔 리깅은 비활성화됩니다.");
        }

        if (spine == null && chest == null)
        {
            Debug.LogWarning("[XBotKSLRig] Spine/Chest 본을 찾지 못했습니다. 상체 회전은 제한됩니다.");
        }

        // 초기 로컬 회전값 저장
        if (spine != null) spineInitialLocalRot = spine.localRotation;
        if (chest != null) chestInitialLocalRot = chest.localRotation;
        if (neck  != null) neckInitialLocalRot  = neck.localRotation;
        if (head  != null) headInitialLocalRot  = head.localRotation;

        rightUpperArmInitialLocalRot = rightUpperArm.localRotation;
        rightLowerArmInitialLocalRot = rightLowerArm.localRotation;
        rightHandInitialLocalRot     = rightHand.localRotation;

        if (leftUpperArm != null)
        {
            leftUpperArmInitialLocalRot = leftUpperArm.localRotation;
            leftLowerArmInitialLocalRot = leftLowerArm.localRotation;
            leftHandInitialLocalRot     = leftHand.localRotation;
        }

        // "정면" 기준은 Chest가 있으면 그 forward, 없으면 Spine, 그것도 없으면 전체 transform.forward
        Transform torsoRef = chest != null ? chest : (spine != null ? spine : transform);
        initialTorsoForward = torsoRef.forward;

        Debug.Log("[XBotKSLRig] 초기화 완료. 상체 + 팔 본 연결됨. 정면 방향 고정.");
    }

    private void LateUpdate()
    {
        if (loader == null || loader.body3D == null)
            return;

        // 1) 상체(Spine/Chest/Neck/Head)
        ApplyUpperBody();

        // 2) 팔 + 손
        ApplyRightArm();
        ApplyRightHand();

        ApplyLeftArm();
        ApplyLeftHand();
    }

    // ===================== 상체(척추/가슴/목/머리) =====================

    private void ApplyUpperBody()
    {
        var body = loader.body3D;
        if (body.Length <= BODY_MID_HIP)
            return;

        // OpenPose → Unity 좌표로 변환
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
        if (neck != null || head != null)
        {
            if (body.Length > BODY_NOSE)
            {
                Vector3 nosePos = MapToUnity(body[BODY_NOSE]);
                Vector3 headDirRaw = nosePos - neckPos;
                if (headDirRaw.sqrMagnitude > 1e-6f)
                {
                    headDirRaw.Normalize();

                    // 머리도 yaw는 고정: 전방을 initialTorsoForward 근처로,
                    // pitch/roll 정도만 데이터에 따라가도록 Up은 torsoUp 그대로 사용
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

    // ===================== 오른팔 / 오른손 =====================

    private void ApplyRightArm()
{
    var body = loader.body3D;
    if (body.Length <= BODY_R_WRIST)
        return;

    Vector3 s = MapToUnity(body[BODY_R_SHOULDER]); // shoulder
    Vector3 e = MapToUnity(body[BODY_R_ELBOW]);    // elbow
    Vector3 w = MapToUnity(body[BODY_R_WRIST]);    // wrist

    // 👉 팔꿈치/손목 둘 다 "가슴 앞 + 오른쪽" 영역으로 클램프
    e = ClampToFrontOfChest(e, true);
    w = ClampToFrontOfChest(w, true);

    Vector3 upperDir = e - s;
    Vector3 lowerDir = w - e;

    if (upperDir.sqrMagnitude < 1e-6f || lowerDir.sqrMagnitude < 1e-6f)
        return;

    upperDir.Normalize();
    lowerDir.Normalize();

    Quaternion upperWorldRot = Quaternion.LookRotation(upperDir, Vector3.up);
    Quaternion lowerWorldRot = Quaternion.LookRotation(lowerDir, Vector3.up);

    // 초기 로컬 회전 기준 + 각도 제한
    ApplyBoneWithClamp(
        rightUpperArm,
        rightUpperArmInitialLocalRot,
        upperWorldRot,
        limbLerp,
        maxArmAngle
    );

    ApplyBoneWithClamp(
        rightLowerArm,
        rightLowerArmInitialLocalRot,
        lowerWorldRot,
        limbLerp,
        maxArmAngle
    );
}

    private void ApplyRightHand()
    {
        var hand = loader.rightHand3D;
        if (hand == null || hand.Length < 9) // 0: wrist, 8: index tip
            return;

        Vector3 wrist    = MapToUnity(hand[0]);
        Vector3 indexTip = MapToUnity(hand[8]);

        // 오른손은 몸의 오른쪽 + 앞쪽 영역
        wrist    = ClampToFrontOfChest(wrist, true);
        indexTip = ClampToFrontOfChest(indexTip, true);

        Vector3 dir = indexTip - wrist;
        if (dir.sqrMagnitude < 1e-6f)
            return;
        dir.Normalize();

        Quaternion targetWorldRot = Quaternion.LookRotation(dir, Vector3.up);

        Transform parent = rightHand.parent;
        if (parent == null)
            return;

        Quaternion desiredLocal = Quaternion.Inverse(parent.rotation) * targetWorldRot;

        Quaternion delta = desiredLocal * Quaternion.Inverse(rightHandInitialLocalRot);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z))
            return;

        angle = Mathf.Min(angle, maxWristAngle);

        Quaternion clampedLocal = Quaternion.AngleAxis(angle, axis) * rightHandInitialLocalRot;

        rightHand.localRotation = Quaternion.Slerp(
            rightHand.localRotation,
            clampedLocal,
            limbLerp
        );
    }

    // ===================== 왼팔 / 왼손 =====================

    private void ApplyLeftArm()
{
    if (leftUpperArm == null || leftLowerArm == null)
        return;

    var body = loader.body3D;
    if (body.Length <= BODY_L_WRIST)
        return;

    Vector3 s = MapToUnity(body[BODY_L_SHOULDER]); // shoulder
    Vector3 e = MapToUnity(body[BODY_L_ELBOW]);    // elbow
    Vector3 w = MapToUnity(body[BODY_L_WRIST]);    // wrist

    // 👉 팔꿈치/손목 둘 다 "가슴 앞 + 왼쪽" 영역으로 클램프
    e = ClampToFrontOfChest(e, false);
    w = ClampToFrontOfChest(w, false);

    Vector3 upperDir = e - s;
    Vector3 lowerDir = w - e;

    if (upperDir.sqrMagnitude < 1e-6f || lowerDir.sqrMagnitude < 1e-6f)
        return;

    upperDir.Normalize();
    lowerDir.Normalize();

    Quaternion upperWorldRot = Quaternion.LookRotation(upperDir, Vector3.up);
    Quaternion lowerWorldRot = Quaternion.LookRotation(lowerDir, Vector3.up);

    ApplyBoneWithClamp(
        leftUpperArm,
        leftUpperArmInitialLocalRot,
        upperWorldRot,
        limbLerp,
        maxArmAngle
    );

    ApplyBoneWithClamp(
        leftLowerArm,
        leftLowerArmInitialLocalRot,
        lowerWorldRot,
        limbLerp,
        maxArmAngle
    );
}

    private void ApplyLeftHand()
    {
        if (leftHand == null)
            return;

        var hand = loader.leftHand3D;
        if (hand == null || hand.Length < 9) // 0: wrist, 8: index tip
            return;

        Vector3 wrist    = MapToUnity(hand[0]);
        Vector3 indexTip = MapToUnity(hand[8]);

        // 왼손은 몸의 왼쪽 + 앞쪽 영역
        wrist    = ClampToFrontOfChest(wrist, false);
        indexTip = ClampToFrontOfChest(indexTip, false);

        Vector3 dir = indexTip - wrist;
        if (dir.sqrMagnitude < 1e-6f)
            return;
        dir.Normalize();

        Quaternion targetWorldRot = Quaternion.LookRotation(dir, Vector3.up);

        Transform parent = leftHand.parent;
        if (parent == null)
            return;

        Quaternion desiredLocal = Quaternion.Inverse(parent.rotation) * targetWorldRot;

        Quaternion delta = desiredLocal * Quaternion.Inverse(leftHandInitialLocalRot);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z))
            return;

        angle = Mathf.Min(angle, maxWristAngle);

        Quaternion clampedLocal = Quaternion.AngleAxis(angle, axis) * leftHandInitialLocalRot;

        leftHand.localRotation = Quaternion.Slerp(
            leftHand.localRotation,
            clampedLocal,
            limbLerp
        );
    }

    // ===================== 공통 유틸 =====================

    /// <summary>
    /// 손/손목 목표 위치가 몸(가슴) 뒤로 가거나,
    /// 왼손/오른손이 서로 반대편 공간으로 넘어가는 걸 막아주는 클램프.
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
    /// </summary>
    private Vector3 MapToUnity(Vector3 p)
    {
        // x도 반전해서 "사람 기준 오른쪽"이 유니티에서도 올바르게 보이도록
        return new Vector3(-p.x, -p.y, zSign * p.z);
    }
}