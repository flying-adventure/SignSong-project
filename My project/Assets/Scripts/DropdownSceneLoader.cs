using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class DropdownSceneLoader : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    void Start()
    {
        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnDropdownChanged(int index)
    {
        switch (index)
        {
            case 0:
                SceneManager.LoadScene("Sign_list");
                break;
            case 1:
                SceneManager.LoadScene("Dongyo_list");
                break;
            case 2:
                SceneManager.LoadScene("Gayo_list");
                break;
        }
    }
}
