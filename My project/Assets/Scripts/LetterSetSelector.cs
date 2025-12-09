using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LetterSetSelector : MonoBehaviour
{
    private static readonly List<string> consonants = new List<string> {
        "ㄱ","ㄴ","ㄷ","ㄹ","ㅁ","ㅂ","ㅅ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ",
        "ㄲ","ㄸ","ㅃ","ㅆ","ㅉ"
    };

    private static readonly List<string> vowels = new List<string> {
        "ㅏ","ㅐ","ㅑ","ㅒ","ㅓ","ㅔ","ㅕ","ㅖ","ㅗ","ㅘ","ㅙ","ㅚ","ㅛ",
        "ㅜ","ㅝ","ㅞ","ㅟ","ㅠ","ㅡ","ㅢ","ㅣ"
    };

    // 버튼 1 → 자음 세트
    public void SelectConsonants()
    {
        LetterSetManager.selectedLetters = new List<string>(consonants);
        SceneManager.LoadScene("Game_1");
    }

    // 버튼 2 → 모음 세트
    public void SelectVowels()
    {
        LetterSetManager.selectedLetters = new List<string>(vowels);
        SceneManager.LoadScene("Game_1");
    }

    // 버튼 3 → 자음+모음 섞어서 랜덤 5개
    public void SelectRandom5()
    {
        List<string> mix = new List<string>();
        mix.AddRange(consonants);
        mix.AddRange(vowels);

        // 셔플
        for (int i = 0; i < mix.Count; i++)
        {
            int r = Random.Range(0, mix.Count);
            (mix[i], mix[r]) = (mix[r], mix[i]);
        }

        // 앞에서 5개만 뽑기
        LetterSetManager.selectedLetters = mix.GetRange(0, 5);

        SceneManager.LoadScene("Game_1");
    }
}
