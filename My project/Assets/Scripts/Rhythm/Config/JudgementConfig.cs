using UnityEngine;

[CreateAssetMenu(menuName = "Rhythm/JudgementConfig")]
public class JudgementConfig : ScriptableObject
{
    [Header("Time windows (seconds)")]
    public float perfectWindow = 0.2f;
    public float goodWindow = 0.40f;

    [Header("Minimum acceptance thresholds")]
    public float minProb = 0.20f;   // 분류 confidence 컷
    public float maxDist = 10.0f;   // dist 컷 (클수록 나쁨)

    [Header("Anti-duplicate")]
    public float sameSignCooldown = 0.1f; // 같은 sign 연속 입력 방지
}