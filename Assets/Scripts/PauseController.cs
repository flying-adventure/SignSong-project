using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseController : MonoBehaviour
{
    public GameObject pausePanel;

    private AudioSource[] audioSources;

    void Start()
    {
        Time.timeScale = 1f;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
    }

    public void OnPause()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }

        Time.timeScale = 0f;

        audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        foreach (AudioSource audio in audioSources)
        {
            if (audio != null && audio.isPlaying)
            {
                audio.Pause();
            }
        }

        Debug.Log("[Pause] 게임 및 오디오 일시정지");
    }

    public void OnContinue()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        Time.timeScale = 1f;

        foreach (AudioSource audio in audioSources)
        {
            if (audio != null)
            {
                audio.UnPause();
            }
        }

        Debug.Log("[Pause] 게임 및 오디오 재개");
    }

    public void OnTryAgain()
    {
        Time.timeScale = 1f;
        StopAllAudio();

        SceneManager.LoadScene("Gayo_game");
    }

    public void OnExit()
    {
        Time.timeScale = 1f;
        StopAllAudio();

        SceneManager.LoadScene("Sign_list");
    }

    private void StopAllAudio()
    {
        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        foreach (AudioSource audio in allAudioSources)
        {
            if (audio != null)
            {
                audio.Stop();
            }
        }
    }
}