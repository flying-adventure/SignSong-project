using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class DropdownSceneLoader : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    void Start()
    {
        // 씬 시작 시 이전 선택값 복원
        dropdown.value = SelectionMemory.lastSelectedIndex;
    }

    public void OnDropdownChanged(int index)
    {
        // 선택값 저장
        SelectionMemory.lastSelectedIndex = index;

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
