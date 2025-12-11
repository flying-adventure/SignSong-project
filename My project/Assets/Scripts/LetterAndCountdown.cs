using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class LetterAndCountdown : MonoBehaviour
{
    public TextMeshProUGUI letterText;
    public TextMeshProUGUI countdownText;

    void Start()
    {
        StartCoroutine(LetterRoutine());
        StartCoroutine(CountdownRoutine());
    }

    IEnumerator LetterRoutine()
    {
        List<string> letters = LetterSetManager.selectedLetters;

        for (int i = 0; i < letters.Count; i++)
        {
            letterText.text = letters[i];
            yield return new WaitForSeconds(3f);
        }

        letterText.text = "";
        SceneManager.LoadScene("result1");

    }

    IEnumerator CountdownRoutine()
    {
        while (true)
        {
            countdownText.text = "3";
            yield return new WaitForSeconds(1f);

            countdownText.text = "2";
            yield return new WaitForSeconds(1f);

            countdownText.text = "1";
            yield return new WaitForSeconds(1f);
        }
    }
}
