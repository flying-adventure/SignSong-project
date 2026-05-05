using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultSceneController : MonoBehaviour
{
    public void GoToGame()
    {
        SceneManager.LoadScene("Gayo_game");
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("Sign_list");
    }
}