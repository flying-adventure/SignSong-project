using UnityEngine;
using UnityEngine.SceneManagement;

public class InitUI : MonoBehaviour
{
    // init 씬의 "로그인" 버튼
    public void OnClickGoLogin()
    {
        SceneManager.LoadScene("Login");   // login 씬 이름 그대로
    }

    // init 씬의 "회원가입" 버튼
    public void OnClickGoSignup()
    {
        SceneManager.LoadScene("Signup");  // signup 씬 이름 그대로
    }
}