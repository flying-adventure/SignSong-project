using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigationManager : MonoBehaviour
{
    public static string previousSceneName;

    public void LoadProfileScene()
    {
        previousSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene("ProfileScene");
    }

    public void LoadSettingScene()
    {
        previousSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene("SettingsScene");
    }
    public void LoadGayoGame()
    {
        SceneManager.LoadScene("Gayo_game");
    }

    public void GoBackToPreviousScene()
    {
        if (!string.IsNullOrEmpty(previousSceneName))
        {
            SceneManager.LoadScene(previousSceneName);
        }
        else
        {
            Debug.LogWarning("이전 씬 이름이 저장되지 않았습니다.");
        }
    }
}