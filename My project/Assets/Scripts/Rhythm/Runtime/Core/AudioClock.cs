using UnityEngine;

public class AudioClock : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    private double _startDsp;

    public void Play()
    {
        _startDsp = AudioSettings.dspTime;
        audioSource.Play();
    }

    public float NowSec() => (float)(AudioSettings.dspTime - _startDsp);
}
