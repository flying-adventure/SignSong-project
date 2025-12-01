using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class SignupUI : MonoBehaviour
{
    public TMP_InputField idInput;
    public TMP_InputField pwInput;
    public TextMeshProUGUI messageText;

    // 회원가입 성공 후 이동할 씬 (보통 login)
    public string afterRegisterSceneName = "Login";

    private void Awake()
    {
        UserDatabase.Load();
    }

    public void OnClickOk()
    {
        string id = idInput.text;
        string pw = pwInput.text;

        bool ok = UserDatabase.Register(id, pw, out string msg);
        messageText.text = msg;

        if (ok)
        {
            // 회원가입 성공 후 로그인 씬으로 이동
            SceneManager.LoadScene(afterRegisterSceneName);
        }
    }

    public void OnClickBack()
    {
        SceneManager.LoadScene("Init");
    }
}