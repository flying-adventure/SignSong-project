using UnityEngine;

/// <summary>
/// 한 손가락(엄지/검지/중지/약지/새끼)의
/// 각 관절 3개(근위/중간/말단)를 위한 각도 배열.
/// 예: thumb = [관절1각도, 관절2각도, 관절3각도]
/// </summary>
[System.Serializable]
public class FingerAngles
{
    public float[] thumb;   // 3개 값
    public float[] index;   // 3개 값
    public float[] middle;  // 3개 값
    public float[] ring;    // 3개 값
    public float[] pinky;   // 3개 값
}

/// <summary>
/// 지화 하나(예: "ㄱ", "ㄴ", "테스트")에 대한 손가락 각도 포즈.
/// Python에서 npy → 이 구조로 JSON 저장해서 Unity에서 로드.
/// JSON 예:
/// {
///   "gesture_name": "테스트",
///   "finger_angles": {
///     "thumb":  [10.0, 20.0, 30.0],
///     "index":  [45.0, 60.0, 70.0],
///     "middle": [0.0,  10.0, 15.0],
///     "ring":   [0.0,  5.0,  10.0],
///     "pinky":  [0.0,  5.0,  10.0]
///   }
/// }
/// </summary>
[System.Serializable]
public class FingerAngleData
{
    public string gesture_name;
    public FingerAngles finger_angles;
}