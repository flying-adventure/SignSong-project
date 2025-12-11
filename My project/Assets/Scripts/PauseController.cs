using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseController : MonoBehaviour
{
    public GameObject pausePanel;

    public void OnPause()
    {
        pausePanel.SetActive(true);
        Time.timeScale = 0f; // 게임 정지
    }

    public void OnContinue()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f; // 재개
    }

    public void OnTryAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Game_1");
    }

    public void OnExit()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Sign_list");
    }
}
