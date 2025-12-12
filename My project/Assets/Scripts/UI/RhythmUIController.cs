using UnityEngine;
using TMPro;   // TextMeshPro 쓸 거면 필요

public class RhythmUIController : MonoBehaviour
{
    [Header("Engine Refs")]
    [SerializeField] private JudgementEngine judgementEngine;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Judge Labels (PERFECT / GOOD / MISS)")]
    [SerializeField] private GameObject perfectLabel;
    [SerializeField] private GameObject goodLabel;
    [SerializeField] private GameObject missLabel;

    [Header("Text UI (optional)")]
    [SerializeField] private TextMeshProUGUI perfectCountText;
    [SerializeField] private TextMeshProUGUI goodCountText;
    [SerializeField] private TextMeshProUGUI missCountText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI maxComboText;

    private void Awake()
    {
        // 인스펙터에서 안 넣어줬으면 자동으로 찾아보기
        if (judgementEngine == null)
            judgementEngine = FindObjectOfType<JudgementEngine>();
        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();
    }

    private void OnEnable()
    {
        if (judgementEngine != null)
            judgementEngine.OnJudged += HandleJudged;
    }

    private void OnDisable()
    {
        if (judgementEngine != null)
            judgementEngine.OnJudged -= HandleJudged;
    }

    // --------- 콜백: 매 판정마다 호출됨 ---------
    private void HandleJudged(JudgeEvent e)
    {
        // 1) PERFECT / GOOD / MISS 라벨 ON/OFF
        if (perfectLabel) perfectLabel.SetActive(e.result == JudgeResult.Perfect);
        if (goodLabel)    goodLabel.SetActive(e.result == JudgeResult.Good);
        if (missLabel)    missLabel.SetActive(e.result == JudgeResult.Miss);

        // 2) 점수/콤보 텍스트 갱신 (ScoreManager 값 사용)
        if (scoreManager != null)
        {
            if (perfectCountText) perfectCountText.text = scoreManager.perfectCount.ToString();
            if (goodCountText)    goodCountText.text    = scoreManager.goodCount.ToString();
            if (missCountText)    missCountText.text    = scoreManager.missCount.ToString();

            if (comboText)
            {
                if (scoreManager.combo <= 1)
                    comboText.text = "";   // 0~1 콤보는 숨기고 싶으면
                else
                    comboText.text = scoreManager.combo + " Combo";
            }

            if (maxComboText)
                maxComboText.text = "MAX " + scoreManager.maxCombo;
        }
    }
}