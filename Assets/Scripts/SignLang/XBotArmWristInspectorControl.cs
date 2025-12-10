using UnityEngine;

/// <summary>
/// XBot의 오른팔 관절(어깨/팔꿈치/손목 등)을
/// Inspector에서 슬라이더(float [Range])로 직접 조절할 수 있게 해주는 스크립트.
///
/// - 이 스크립트를 XBot(Animator가 붙어 있는 오브젝트)에 붙이고
/// - joints 배열에 제어하고 싶은 본(HumanBodyBones)을 등록한 뒤
/// - Play 모드에서 Inspector의 angle 슬라이더를 움직이면
///   → 해당 관절이 바로 회전한다.
///
/// Animator가 Idle/SignIdle 같은 애니메이션을 돌리고 있어도
/// 이 스크립트가 LateUpdate에서 마지막에 회전을 덮어쓴다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class XBotArmWristInspectorControl : MonoBehaviour
{
    /// <summary>
    /// 어떤 축으로 회전시킬지 선택용 (Inspector에서 보기 편하게 enum으로)
    /// </summary>
    public enum AxisMode
    {
        X,
        Y,
        Z
    }

    [System.Serializable]
    public class JointControl
    {
        [Header("Inspector에서 보기 위한 라벨 (선택)")]
        public string label;

        [Header("프레임 간 각도 스무딩 정도 (0~1, 클수록 빨리 따라감)")]
        [Range(0f, 1f)]
        public float smoothingFactor = 0.3f;   // 0.2~0.5 정도 추천

        [Header("한 프레임당 최대 회전 변화량 (deg)")]
        public float maxStepPerFrame = 15f;    // 한 프레임에 15도 이상은 안 가게


         [Header("이 관절에 항상 더해줄 보정 각도 (deg)")]
        public float offset = 0f;   // ★ 새로 추가

        [Header("조절할 Humanoid 본 (예: RightUpperArm, RightLowerArm, RightHand 등)")]
        public HumanBodyBones bone = HumanBodyBones.RightUpperArm;

        [Header("어느 축으로 회전시킬지 (로컬 기준 X/Y/Z 중 하나)")]
        public AxisMode axis = AxisMode.X;

        [Header("이 관절에 허용할 최소/최대 각도 (deg)")]
        public float minAngle = -90f;
        public float maxAngle = 90f;

        [Header("현재 각도 (Inspector 슬라이더로 조절)")]
        [Range(-180f, 180f)]
        public float angle = 0f;

        [HideInInspector] public Transform boneTransform;
        [HideInInspector] public Quaternion baseLocalRotation;
    }

    [Header("팔/손목 관절 제어 리스트")]
    [Tooltip("오른어깨/오른팔꿈치/오른손목 등 제어하고 싶은 본들을 추가")]
    public JointControl[] joints;

    private Animator animator;
    private bool initialized = false;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized) return;

        animator = GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            Debug.LogError("[XBotArmWristInspectorControl] Humanoid Avatar가 유효하지 않습니다. XBot 설정을 확인하세요.");
            enabled = false;
            return;
        }

        if (joints == null || joints.Length == 0)
        {
            Debug.LogWarning("[XBotArmWristInspectorControl] joints 배열이 비어 있습니다. Inspector에서 관절을 추가하세요.");
        }

        // 각 JointControl에 대해 본 찾고, 현재 로컬 회전을 베이스로 저장
        foreach (var jc in joints)
        {
            if (jc == null) continue;

            jc.boneTransform = animator.GetBoneTransform(jc.bone);
            if (jc.boneTransform == null)
            {
                Debug.LogWarning("[XBotArmWristInspectorControl] 본을 찾지 못했습니다: " + jc.bone);
                continue;
            }

            jc.baseLocalRotation = jc.boneTransform.localRotation;

            // angle 초기값은 0으로 두고, min/max 범위도 한 번 정리
            jc.angle = Mathf.Clamp(jc.angle, jc.minAngle, jc.maxAngle);
        }

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            Initialize();
            if (!initialized) return;
        }

        // Animator가 본 회전을 적용한 이후에, 우리가 Inspector 값으로 덮어쓴다.
        foreach (var jc in joints)
        {
            if (jc == null) continue;
            if (jc.boneTransform == null) continue;

            // Inspector에서 슬라이더로 바뀐 angle 값을 min/max 범위 안으로 제한
            float clamped = Mathf.Clamp(jc.angle, jc.minAngle, jc.maxAngle);

            // 사용할 축 벡터 계산
            Vector3 axis = AxisToVector3(jc.axis);
            if (axis.sqrMagnitude < 1e-6f)
                continue;

            // 기본 로컬 회전에서 clamped 각도만큼 회전
            jc.boneTransform.localRotation =
                jc.baseLocalRotation * Quaternion.AngleAxis(clamped, axis);
        }
    }

    /// <summary>
    /// 외부(플레이어 스크립트)에서 프레임별 각도 배열을 넘겨와서
    /// joints[i].angle에 그대로 세팅하는 함수.
    /// angles.Length는 joints.Length와 같아야 한다.
    /// </summary>
   public void SetFrameAngles(float[] angles)
{
    if (joints == null || angles == null) return;

    if (!initialized)
    {
        Initialize();
        if (!initialized) return;
    }

    if (angles.Length != joints.Length)
    {
        Debug.LogWarning(
            $"[XBotArmWristInspectorControl] angles.Length({angles.Length}) != joints.Length({joints.Length})");
        return;
    }

    for (int i = 0; i < joints.Length; i++)
    {
        var jc = joints[i];
        if (jc == null || jc.boneTransform == null) continue;

        // ───────────────────────────────────
        // 1) JSON 값 + per-joint offset
        // ───────────────────────────────────
        float targetRaw = angles[i] + jc.offset;
        float target = Mathf.Clamp(targetRaw, jc.minAngle, jc.maxAngle);

        // ───────────────────────────────────
        // 2) 이전 각도(jc.angle)에서 target으로 스무딩 (LERP)
        //    joint마다 smoothingFactor를 다르게 줄 수 있음
        // ───────────────────────────────────
        float t = Mathf.Clamp01(jc.smoothingFactor);   // ★ 변경됨
        float lerped = Mathf.Lerp(jc.angle, target, t);

        // ───────────────────────────────────
        // 3) 한 프레임당 최대 이동량 제한 (튀는 값 컷)
        // ───────────────────────────────────
        float delta = lerped - jc.angle;
        float maxStep = Mathf.Max(1f, jc.maxStepPerFrame);  // ★ 변경됨
        delta = Mathf.Clamp(delta, -maxStep, maxStep);

        float finalAngle = jc.angle + delta;

        // ───────────────────────────────────
        // 4) Inspector 값 업데이트
        // ───────────────────────────────────
        jc.angle = finalAngle;

        // ───────────────────────────────────
        // 5) 실제 본 회전 적용
        // ───────────────────────────────────
        Vector3 axis = AxisToVector3(jc.axis);
        if (axis.sqrMagnitude < 1e-6f) continue;

        jc.boneTransform.localRotation =
            jc.baseLocalRotation * Quaternion.AngleAxis(finalAngle, axis);
    }
}

    private Vector3 AxisToVector3(AxisMode mode)
    {
        switch (mode)
        {
            case AxisMode.X:
                return Vector3.right;
            case AxisMode.Y:
                return Vector3.up;
            case AxisMode.Z:
                return Vector3.forward;
            default:
                return Vector3.right;
        }
    }

#if UNITY_EDITOR
    // Inspector에서 값 바뀔 때도 베이스 초기화가 필요한 경우를 대비해서
    private void OnValidate()
    {
        // 에디터에서 스크립트 변경 시 Awake 전에 호출될 수 있어서
        // 여기서는 단순 플래그만 초기화 → 다음 LateUpdate에서 다시 Initialize
        initialized = false;
    }
#endif
}