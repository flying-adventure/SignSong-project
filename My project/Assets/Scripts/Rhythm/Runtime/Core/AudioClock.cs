using UnityEngine;

public class AudioClock : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float globalOffsetSec = 0f;
    private double _startDsp;
    private bool _started;

    public void Play(float scheduleDelaySec = 0.05f)
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("[AudioClock] AudioSource is missing.");
            return;
        }

        // DSP 기준으로 “정확히 언제 시작했는지”를 고정
        _startDsp = AudioSettings.dspTime + scheduleDelaySec;

        // 정확한 시작 보장
        audioSource.Stop();
        audioSource.PlayScheduled(_startDsp);

        _started = true;
    }

    public float NowSec()
    {
        if (!_started) return 0f;

        // 항상 DSPTime 기반(프레임/비디오/Time.time 영향 없음)
        return (float)(AudioSettings.dspTime - _startDsp) + globalOffsetSec;
    }

    public void SetOffset(float sec) => globalOffsetSec = sec;
}
