using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ResultSceneController : MonoBehaviour
{
    [Header("Result Text UI")]
    [SerializeField] private TextMeshProUGUI perfectText;
    [SerializeField] private TextMeshProUGUI goodText;
    [SerializeField] private TextMeshProUGUI missText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI maxComboText;

    [Header("Grade Objects")]
    [SerializeField] private GameObject gradeAObject;
    [SerializeField] private GameObject gradeBObject;
    [SerializeField] private GameObject gradeCObject;
    [SerializeField] private GameObject gradeFObject;

    private void Start()
    {
        UpdateResultUI();
    }

    private void UpdateResultUI()
    {
        if (perfectText != null)
            perfectText.text = GameResultData.perfectCount.ToString();

        if (goodText != null)
            goodText.text = GameResultData.goodCount.ToString();

        if (missText != null)
            missText.text = GameResultData.missCount.ToString();

        if (scoreText != null)
            scoreText.text = GameResultData.totalScore.ToString();

        if (maxComboText != null)
            maxComboText.text = GameResultData.maxCombo.ToString();

        ShowGrade(GameResultData.grade);
    }

    private void ShowGrade(string grade)
    {
        if (gradeAObject != null) gradeAObject.SetActive(grade == "A");
        if (gradeBObject != null) gradeBObject.SetActive(grade == "B");
        if (gradeCObject != null) gradeCObject.SetActive(grade == "C");
        if (gradeFObject != null) gradeFObject.SetActive(grade == "F");
    }

    public void GoToGame()
    {
        SceneManager.LoadScene("Gayo_game");
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("Sign_list");
    }
}