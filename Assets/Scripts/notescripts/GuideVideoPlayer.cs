using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using TMPro;

public class GuideVideoPlayer : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public TMP_Text guideWordText;

    [Header("Playback Speed")]
    public float normalSpeed = 1.4f;
    public float queuedSpeed = 2.5f;

    private Queue<QueuedWord> videoQueue = new Queue<QueuedWord>();
    private bool isPlaying = false;

    public void PlayWord(string word, float targetDuration)
    {
        bool isOverlapped = isPlaying || videoQueue.Count > 0;

        videoQueue.Enqueue(new QueuedWord
        {
            word = word,
            speed = isOverlapped ? queuedSpeed : normalSpeed
        });

        if (!isPlaying)
        {
            StartCoroutine(PlayQueue());
        }
    }

    private IEnumerator PlayQueue()
    {
        isPlaying = true;

        while (videoQueue.Count > 0)
        {
            QueuedWord item = videoQueue.Dequeue();

            string path = $"guidevideo/{item.word}";
            VideoClip clip = Resources.Load<VideoClip>(path);

            if (clip == null)
            {
                Debug.LogWarning($"[GuideVideoPlayer] VideoClip not found: Resources/{path}");
                continue;
            }

            if (guideWordText != null)
            {
                guideWordText.text = item.word;
            }

            videoPlayer.Stop();
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = clip;
            videoPlayer.playbackSpeed = item.speed;
            videoPlayer.time = 0;
            videoPlayer.Play();

            Debug.Log($"[GuideVideoPlayer] Play queued video: {item.word}, speed={item.speed:F1}x");

            yield return new WaitForSeconds((float)(clip.length / item.speed));
        }

        isPlaying = false;
    }

    private class QueuedWord
    {
        public string word;
        public float speed;
    }
}