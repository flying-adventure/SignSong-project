using UnityEngine;
using TMPro;

public class MainUserDisplay : MonoBehaviour
{
    public TextMeshProUGUI usernameText;

    void Start()
    {
        string user = PlayerPrefs.GetString("currentUser", "Guest");
        usernameText.text = $"{user}";
    }
}