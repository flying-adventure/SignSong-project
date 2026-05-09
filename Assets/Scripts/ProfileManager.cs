using UnityEngine;
using TMPro;
using System.Collections;

public class ProfileManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField nameInputField;

    private const string UserNameKey = "USER_NAME";

    void Start()
    {
        StartCoroutine(LoadUserNameAfterOneFrame());
    }

    private IEnumerator LoadUserNameAfterOneFrame()
    {
        yield return null;

        string savedName = PlayerPrefs.GetString(UserNameKey, "사용자");

        if (nameInputField != null)
        {
            nameInputField.text = savedName;
        }

        Debug.Log("[Profile] 불러온 사용자 이름: " + savedName);
    }

    public void SaveUserName()
    {
        string newName = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("[Profile] 사용자 이름이 비어 있어서 저장하지 않았습니다.");
            return;
        }

        PlayerPrefs.SetString(UserNameKey, newName);
        PlayerPrefs.Save();

        Debug.Log("[Profile] 사용자 이름 저장 완료: " + newName);
    }
}