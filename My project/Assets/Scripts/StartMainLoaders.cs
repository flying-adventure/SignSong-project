using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMainLoader : MonoBehaviour
{
    void Start()
    {
        Invoke("LoadNext", 2f);   // 2초 뒤 자동 실행
    }

    void LoadNext()
    {
        SceneManager.LoadScene("지화_list");
    }
}
