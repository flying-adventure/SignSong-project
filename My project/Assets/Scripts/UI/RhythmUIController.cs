using System.Collections;
using UnityEngine;
using TMPro;

public class RhythmUIController : MonoBehaviour
{
    [Header("Engine Refs")]
    [SerializeField] private JudgementEngine judgementEngine;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Judge Labels (PERFECT / GOOD / MISS)")]
    [SerializeField] private GameObject perfectLabel;
    [SerializeField] private GameObject goodLabel;
    [SerializeField] private GameObject missLabel;

    [Header("Label Display")]
    [SerializeField] private float labelShowSeconds = 0.25f;

    [Header("Text UI (optional)")]
    [SerializeField] private TextMeshProUGUI perfectCountText;
    [SerializeField] private TextMeshProUGUI goodCountText;
    [SerializeField] private TextMeshProUGUI missCountText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI maxComboText;

    private Coroutine _hideLabelCo;
    private Coroutine _refreshTextCo;

    private void Awake()
    {
        if (judgementEngine == null) judgementEngine = FindObjectOfType<JudgementEngine>();
        if (scoreManager == null)    scoreManager    = FindObjectOfType<ScoreManager>();

        // 시작 시 UI 초기화
        SetAllJudgeLabelsOff();
        RefreshTexts();
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

    private void HandleJudged(JudgeEvent e)
    {
        Debug.Log($"[UI] t={Time.time:F2} Judged={e.result} noteId={e.noteId}");
        
        // 1) 라벨 표시 (잠깐만)
        ShowJudgeLabel(e.result);

        // 2) ScoreManager 값으로 텍스트 갱신
        //    (ScoreManager가 같은 이벤트를 받아 업데이트하는 타이밍 문제 방지)
        if (_refreshTextCo != null) StopCoroutine(_refreshTextCo);
        _refreshTextCo = StartCoroutine(RefreshTextsNextFrame());
    }

    private void BringToFront(GameObject go)
    {
        if (!go) return;
        go.transform.SetAsLastSibling(); // 같은 Canvas 내에서 최상단 렌더
    }

    private void ShowJudgeLabel(JudgeResult result)
    {
        SetAllJudgeLabelsOff();
        if (result == JudgeResult.Perfect && perfectLabel)
        {
            perfectLabel.SetActive(true);
            BringToFront(perfectLabel);
        }
        else if (result == JudgeResult.Good && goodLabel)
        {
            goodLabel.SetActive(true);
            BringToFront(goodLabel);
        }
        else if (result == JudgeResult.Miss && missLabel)
        {
            missLabel.SetActive(true);
            BringToFront(missLabel);
        }

        if (_hideLabelCo != null) StopCoroutine(_hideLabelCo);
        _hideLabelCo = StartCoroutine(HideLabelsAfter(labelShowSeconds));

    }

    private IEnumerator HideLabelsAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        SetAllJudgeLabelsOff();
    }

    private void SetAllJudgeLabelsOff()
    {
        if (perfectLabel) perfectLabel.SetActive(false);
        if (goodLabel)    goodLabel.SetActive(false);
        if (missLabel)    missLabel.SetActive(false);
    }

    private IEnumerator RefreshTextsNextFrame()
    {
        yield return null; // 다음 프레임에 갱신 (ScoreManager 업데이트 타이밍 안전)
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        if (scoreManager == null) return;

        if (perfectCountText) perfectCountText.text = scoreManager.perfectCount.ToString();
        if (goodCountText)    goodCountText.text    = scoreManager.goodCount.ToString();
        if (missCountText)    missCountText.text    = scoreManager.missCount.ToString();

        if (comboText)
        {
            comboText.text = (scoreManager.combo <= 1) ? "" : $"{scoreManager.combo} Combo";
        }

        if (maxComboText)
        {
            maxComboText.text = $"MAX {scoreManager.maxCombo}";
        }
    }
}
