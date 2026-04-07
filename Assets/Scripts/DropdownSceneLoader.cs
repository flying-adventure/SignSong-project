using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class DropdownSceneLoader : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    private bool isInitializing = true;

    private void Start()
    {
        SetDropdownValueByCurrentScene();
        dropdown.onValueChanged.AddListener(OnDropdownChanged);
        isInitializing = false;
    }

    private void OnDestroy()
    {
        dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
    }

    private void SetDropdownValueByCurrentScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        switch (currentScene)
        {
            case "Sign_list":
                dropdown.value = 0;
                break;

            case "Dongyo_list":
                dropdown.value = 1;
                break;

            case "Gayo_list":
                dropdown.value = 2;
                break;
        }

        dropdown.RefreshShownValue();
    }

    private void OnDropdownChanged(int index)
    {
        if (isInitializing) return;

        switch (index)
        {
            case 0:
                LoadSceneIfNeeded("Sign_list");
                break;

            case 1:
                LoadSceneIfNeeded("Dongyo_list");
                break;

            case 2:
                LoadSceneIfNeeded("Gayo_list");
                break;
        }
    }

    private void LoadSceneIfNeeded(string sceneName)
    {
        if (SceneManager.GetActiveScene().name != sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}