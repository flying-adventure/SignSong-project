using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // TextMeshPro 사용 시
// using UnityEngine.UI;  // 기본 InputField/Text 쓰면 이것도

public class AuthUI : MonoBehaviour
{
    [Header("SignUp")]
    public TMP_InputField signUpIdInput;
    public TMP_InputField signUpPwInput;

    [Header("Login")]
    public TMP_InputField loginIdInput;
    public TMP_InputField loginPwInput;

    [Header("Common")]
    public TextMeshProUGUI messageText;

    private void Awake()
    {
        UserDatabase.Load();
    }

    public void OnClickSignUp()
    {
        string id = signUpIdInput.text;
        string pw = signUpPwInput.text;

        bool ok = UserDatabase.Register(id, pw, out string msg);
        messageText.text = msg;

        if (ok)
        {
            // 회원가입 성공 후 로그인 탭으로 포커스 옮기거나 자동 입력 등
            loginIdInput.text = id;
            loginPwInput.text = pw;
        }
    }

    public void OnClickLogin()
    {
        string id = loginIdInput.text;
        string pw = loginPwInput.text;

        bool ok = UserDatabase.ValidateLogin(id, pw, out string msg);
        messageText.text = msg;

        if (ok)
        {
            // 현재 로그인 유저 저장
            PlayerPrefs.SetString("currentUser", id);
            PlayerPrefs.Save();

            // 메인 메뉴 씬 이름은 네가 정한 이름으로 변경
            SceneManager.LoadScene("MainMenuScene");
        }
    }
}