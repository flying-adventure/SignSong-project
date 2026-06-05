using UnityEngine;
using TMPro;

public class UserNameDisplay : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text userNameText;

    private const string UserNameKey = "USER_NAME";

    void Start()
    {
        UpdateUserName();
    }

    void OnEnable()
    {
        UpdateUserName();
    }

    public void UpdateUserName()
    {
        string savedName = PlayerPrefs.GetString(UserNameKey, "»ç¿ëÀÚ");

        if (userNameText != null)
        {
            userNameText.text = savedName;
        }
    }
}