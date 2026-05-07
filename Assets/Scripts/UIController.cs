using UnityEngine;

public class UIController : MonoBehaviour
{
    public GameObject panel;

    public void OnChangePhoto()
    {
        Debug.Log("사진 변경 버튼 클릭");
    }
    public void OnLogout()
    {
        Debug.Log("로그아웃 버튼 클릭");
    }
}