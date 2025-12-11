using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseController : MonoBehaviour
{
    public GameObject pausePanel;

    public void OnPause()
    {
        pausePanel.SetActive(true);
        Time.timeScale = 0f; // ???? ????
    }

    public void OnContinue()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f; // ??
    }

    public void OnTryAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Game_1");
    }

    public void OnExit()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("Dongyo_list");

        SceneManager.LoadScene("Sign_list");

    }
}
