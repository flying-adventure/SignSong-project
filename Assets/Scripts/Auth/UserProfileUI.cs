using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UserProfileUI : MonoBehaviour
{
    [Header("Profile Image")]
    [SerializeField]
    private Image profileImage;

    [Header("Name Input Field")]
    [SerializeField]
    private TMP_InputField nameInputField;

    [Header("Optional")]
    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private Sprite defaultProfileSprite;

    private void Start()
    {
        LoadUserProfile();
    }

    private void LoadUserProfile()
    {
        string name = PlayerPrefs.GetString("USER_NAME", "");
        string email = PlayerPrefs.GetString("USER_EMAIL", "");
        string photoUrl = PlayerPrefs.GetString("USER_PHOTO_URL", "");

        Debug.Log("[ProfileUI] USER_NAME: " + name);
        Debug.Log("[ProfileUI] USER_EMAIL: " + email);
        Debug.Log("[ProfileUI] USER_PHOTO_URL: " + photoUrl);

        if (string.IsNullOrEmpty(name))
        {
            name = MakeNameFromEmail(email);
        }

        if (string.IsNullOrEmpty(name))
        {
            name = "사용자";
        }

        ApplyName(name);

        if (!string.IsNullOrEmpty(photoUrl))
        {
            StartCoroutine(LoadProfileImage(photoUrl));
        }
        else
        {
            Debug.LogWarning("[ProfileUI] Photo URL is empty");

            if (profileImage != null && defaultProfileSprite != null)
            {
                profileImage.sprite = defaultProfileSprite;
            }
        }
    }

    private void ApplyName(string name)
    {
        // 화면에 "ID 홍길동"처럼 보이게 하고 싶으면 여기서 ID를 붙임
        string displayText = name;

        if (nameInputField != null)
        {
            nameInputField.text = displayText;
            nameInputField.ForceLabelUpdate();
            Debug.Log("[ProfileUI] Applied name to TMP_InputField: " + displayText);
        }

        if (nameText != null)
        {
            nameText.text = displayText;
            Debug.Log("[ProfileUI] Applied name to TMP_Text: " + displayText);
        }
    }

    private string MakeNameFromEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "";
        }

        int atIndex = email.IndexOf("@");

        if (atIndex <= 0)
        {
            return email;
        }

        return email.Substring(0, atIndex);
    }

    private IEnumerator LoadProfileImage(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[ProfileUI] Failed to load profile image: " + request.error);

                if (profileImage != null && defaultProfileSprite != null)
                {
                    profileImage.sprite = defaultProfileSprite;
                }

                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );

            if (profileImage != null)
            {
                profileImage.sprite = sprite;
                Debug.Log("[ProfileUI] Profile image applied");
            }
        }
    }
}