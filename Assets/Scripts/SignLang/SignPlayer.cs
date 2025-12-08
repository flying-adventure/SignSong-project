using System.Collections;
using UnityEngine;

public class SignPlayer : MonoBehaviour
{
    [Header("초기 재생 설정")]
    public bool playOnStart = true;
    public string startWord = "고민";

    [Header("재생 속도")]
    [Tooltip("1프레임당 시간(초). 30fps = 0.0333")]
    public float frameDuration = 1f / 30f;

    [Header("레퍼런스")]
    public SignDatabase database;
    public OpenPoseHandLoader handLoader;

    private Coroutine playRoutine;

    private void Start()
    {
        Debug.Log("[SignPlayer] Start 호출됨");

        if (playOnStart && !string.IsNullOrEmpty(startWord))
        {
            Debug.Log($"[SignPlayer] playOnStart = true, startWord = {startWord}");
            PlaySignByWord(startWord);
        }
    }

    /// <summary>
    /// 단어(예: '고민')로 클립 찾아서 한 번만 재생
    /// </summary>
    public void PlaySignByWord(string word)
    {
        if (database == null)
        {
            Debug.LogError("[SignPlayer] database가 비어 있습니다.");
            return;
        }

        if (handLoader == null)
        {
            Debug.LogError("[SignPlayer] handLoader가 비어 있습니다.");
            return;
        }

        var clip = database.GetClipByWord(word);
        if (clip == null)
        {
            Debug.LogError($"[SignPlayer] '{word}' 에 해당하는 클립을 찾지 못했습니다.");
            return;
        }

        if (clip.keypointFrames == null || clip.keypointFrames.Length == 0)
        {
            Debug.LogError($"[SignPlayer] '{word}' 클립에 keypointFrames 가 비어 있습니다.");
            return;
        }

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        Debug.Log($"[SignPlayer] '{word}' 클립 재생 시작 (frames: {clip.keypointFrames.Length})");
        playRoutine = StartCoroutine(PlayClipRoutine(clip));
    }

    /// <summary>
    /// ★ 여기서 "딱 1번만" 모든 프레임을 재생하고 끝냄 (루프 없음)
    /// </summary>
    private IEnumerator PlayClipRoutine(SignClip clip)
    {
        Debug.Log($"[SignPlayer] PlayClipRoutine 시작 (frameDuration = {frameDuration})");

        for (int i = 0; i < clip.keypointFrames.Length; i++)
        {
            TextAsset frameJson = clip.keypointFrames[i];
            if (frameJson == null)
            {
                Debug.LogWarning($"[SignPlayer] frame {i} 의 TextAsset 이 비어 있습니다.");
            }
            else
            {
                handLoader.LoadFromJsonText(frameJson.text);
            }

            // 다음 프레임까지 대기 (30fps 기준)
            yield return new WaitForSeconds(frameDuration);
        }

        Debug.Log("[SignPlayer] 클립 재생 완료 (한 번만 재생함)");
        playRoutine = null;
    }
}