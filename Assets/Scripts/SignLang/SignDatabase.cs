using UnityEngine;

/// <summary>
/// 여러 개의 수어 단어 클립을 모아둔 데이터베이스.
/// </summary>
[CreateAssetMenu(menuName = "KSL/SignDatabase", fileName = "SignDatabase")]
public class SignDatabase : ScriptableObject
{
    public SignClip[] clips;

    /// <summary>
    /// 단어(예: "고민")로 클립 찾기
    /// </summary>
    public SignClip GetClipByWord(string word)
    {
        if (string.IsNullOrEmpty(word) || clips == null)
            return null;

        foreach (var clip in clips)
        {
            if (clip == null) continue;
            if (clip.word == word) return clip;
        }

        return null;
    }
}