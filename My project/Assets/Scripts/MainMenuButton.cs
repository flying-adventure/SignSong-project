using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuButton : MonoBehaviour
{
    public void GoToMainMenu()
    {
        SceneManager.LoadScene("Sign_list");
    }
}
