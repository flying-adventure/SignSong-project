using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LoginUI : MonoBehaviour
{
    public TMP_InputField idInput;
    public TMP_InputField pwInput;
    public TextMeshProUGUI messageText;

    // 로그인 성공 후 이동할 씬 이름 (일단 init으로 돌려보자)
    public string nextSceneName = "Main";

    private void Awake()
    {
        UserDatabase.Load();   // 로컬 유저 정보 로딩
    }

    public void OnClickLogin()
    {
        string id = idInput.text;
        string pw = pwInput.text;

        bool ok = UserDatabase.ValidateLogin(id, pw, out string msg);
        messageText.text = msg;

        if (ok)
        {
            PlayerPrefs.SetString("currentUser", id);
            PlayerPrefs.Save();

            SceneManager.LoadScene(nextSceneName);
        }
    }

    // 뒤로 가기 버튼 만들 거면
    public void OnClickBack()
    {
        SceneManager.LoadScene("init");
    }
}