using UnityEngine;

/// <summary>
/// 수어 한 단어(또는 문장)에 해당하는 키포인트 프레임 묶음.
/// ex) word = "고민", keypointFrames = 30fps JSON 35장
/// </summary>
[CreateAssetMenu(fileName = "SignClip", menuName = "KSL/SignClip")]
public class SignClip : ScriptableObject
{
    [Tooltip("수어 단어 (예: '고민')")]
    public string word;

    [Tooltip("이 단어에 해당하는 키포인트 JSON 프레임들 (시간 순서대로)")]
    public TextAsset[] keypointFrames;
}