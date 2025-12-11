using UnityEngine;
using UnityEngine.Video;

public class GuideVideoPlayer : MonoBehaviour
{
    [Header("Components")]
    public VideoPlayer videoPlayer;
    public AudioSource audioSource;

    public void PlayWord(string word, float targetDuration)
    {
        if (videoPlayer == null)
        {
            Debug.LogError("[GuideVideoPlayer] VideoPlayer is not assigned.");
            return;
        }

        videoPlayer.Stop();

        string path = $"guidevideo/{word}";
        var clip = Resources.Load<VideoClip>(path);

        if (clip == null)
        {
            Debug.LogWarning($"[GuideVideoPlayer] VideoClip not found at Resources/{path}.mp4");
            return;
        }

        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;

        if (audioSource != null)
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }

        // 🔥 클립 길이에 맞춰 배속 설정
        float clipLength = (float)clip.length;
        float duration = Mathf.Max(0.1f, targetDuration);

        // playbackSpeed = 클립길이 / 우리가 주고 싶은 구간 길이
        // 예: clip 2초, target 1초 ⇒ 2배속
        float speed = clipLength / duration;
        videoPlayer.playbackSpeed = speed;

        videoPlayer.time = 0;
        videoPlayer.Play();

        Debug.Log($"[GuideVideoPlayer] Play {word}, clip={clipLength:F2}s, target={duration:F2}s, speed={speed:F2}x");
    }
}