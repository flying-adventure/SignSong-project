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
                SceneManager.LoadScene("지화_list");
                break;
            case 1:
                SceneManager.LoadScene("동요_list");
                break;
            case 2:
                SceneManager.LoadScene("가요_list");
                break;
        }
    }
}
