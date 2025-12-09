using UnityEngine;
using TMPro;

public class GestureJudge : MonoBehaviour
{
    public TextMeshProUGUI resultText;
    public string targetLetter;
    public string[] classes;

    public void Judge(string predicted)
    {
        if (predicted == targetLetter)
        {
            resultText.text = "SUCCESS";
            resultText.color = Color.green;
        }
        else
        {
            resultText.text = "FAIL";
            resultText.color = Color.red;
        }
    }
}
