using UnityEngine;

public class SignRecognitionResult
{
    public string label;
    public int classIndex;   // 매핑할 수어 인덱스
    public float confidence;   // 매핑 확신도
    public float distance;
    public bool accepted;

    // 수어 매핑 거절된 결과를 생성하는 정적 메서드
    public static SignRecognitionResult Reject(int classIndex, string label, float confidence, float distance)
    {
        return new SignRecognitionResult
        {
            classIndex = classIndex,
            label = label,
            confidence = confidence,
            distance = distance,
            accepted = false
        };
    }
    // 수어 매핑 수락된 결과를 생성하는 정적 메서드
    public static SignRecognitionResult Accept(int classIndex, string label, float confidence, float distance)
    {
        return new SignRecognitionResult
        {
            classIndex = classIndex,
            label = label,
            confidence = confidence,
            distance = distance,
            accepted = true
        };
    }
}
