using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class SignGameBridge : MonoBehaviour
{
    public SignPredictionProvider predictionProvider;

    [Header("Note Manager")]
    public SignNoteManager noteManager;

    [Header("Judge Settings")]
    public float judgeCooldown = 0.8f;
    private float lastJudgeTime = -999f;

    [Header("Debug Judge")]
    public bool allowSameLabelEvenIfRejected = true;
    public float sameLabelMinConfidence = 0.75f;
    public float sameLabelMaxDistance = 17.0f;

    [Header("Judge UI")]
    public GameObject perfectObject;
    public GameObject goodObject;
    public GameObject missObject;
    public float judgeDisplayTime = 0.5f;

    [Header("Judge Count / Score UI")]
    [SerializeField] private bool showJudgeCountLog = true;

    [SerializeField] private TextMeshProUGUI perfectCountText;
    [SerializeField] private TextMeshProUGUI goodCountText;
    [SerializeField] private TextMeshProUGUI missCountText;
    [SerializeField] private TextMeshProUGUI totalCountText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Score Settings")]
    [SerializeField] private int perfectScore = 100;
    [SerializeField] private int goodScore = 50;
    [SerializeField] private int missScore = 0;

    private int perfectCount = 0;
    private int goodCount = 0;
    private int missCount = 0;
    private int totalJudgedCount = 0;
    private int totalScore = 0;

    [Header("Result Scene")]
    [SerializeField] private string resultSceneName = "result2";
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private bool goResultWhenMusicEnds = true;
    [SerializeField] private float resultDelay = 1.0f;

    private int currentCombo = 0;
    private int maxCombo = 0;
    private bool gameFinished = false;
    private bool musicStarted = false;

    [Header("Timing Debug")]
    public bool enableTimingStats = true;
    private int matchedCount = 0;
    private float matchedDeltaSum = 0f;

    private bool isJudgeShowing = false;

    private Coroutine hideJudgeCoroutine;

    private void OnEnable()
    {
        if (predictionProvider != null)
        {
            predictionProvider.OnPrediction += HandlePrediction;
        }
    }

    private void OnDisable()
    {
        if (predictionProvider != null)
        {
            predictionProvider.OnPrediction -= HandlePrediction;
        }
    }

    /*
    public void SetCurrentTargetLabel(string label)
    {
        currentTargetLabel = label;
        Debug.Log($"[SignGameBridge] target changed: {currentTargetLabel}");
    }
    */

    private void ShowJudgeUI(string judge)
    {
        bool hasJudge = !string.IsNullOrEmpty(judge);
        isJudgeShowing = hasJudge;

        if (perfectObject != null) perfectObject.SetActive(judge == "PERFECT");
        if (goodObject != null) goodObject.SetActive(judge == "GOOD");
        if (missObject != null) missObject.SetActive(judge == "MISS");

        if (hideJudgeCoroutine != null)
        {
            StopCoroutine(hideJudgeCoroutine);
            hideJudgeCoroutine = null;
        }

        if (hasJudge)
        {
            hideJudgeCoroutine = StartCoroutine(HideJudgeAfterDelay());
        }
    }

    private IEnumerator HideJudgeAfterDelay()
    {
        yield return new WaitForSeconds(judgeDisplayTime);

        if (perfectObject != null) perfectObject.SetActive(false);
        if (goodObject != null) goodObject.SetActive(false);
        if (missObject != null) missObject.SetActive(false);

        isJudgeShowing = false;
        hideJudgeCoroutine = null;
        // ShowJudgeUI("");
    }

    private void AddJudgeCount(string result)
    {
        switch (result)
        {
            case "PERFECT":
                perfectCount++;
                totalScore += perfectScore;
                currentCombo++;
                break;

            case "GOOD":
                goodCount++;
                totalScore += goodScore;
                currentCombo++;
                break;

            case "MISS":
                missCount++;
                totalScore += missScore;
                currentCombo = 0;
                break;
        }

        if (currentCombo > maxCombo)
            maxCombo = currentCombo;

        totalJudgedCount++;

        UpdateScoreUI();

        if (showJudgeCountLog)
        {
            Debug.Log(
                $"[JudgeCount] total={totalJudgedCount}, " +
                $"PERFECT={perfectCount}, GOOD={goodCount}, MISS={missCount}, " +
                $"score={totalScore}, combo={currentCombo}, maxCombo={maxCombo}"
            );
        }
    }

    private void UpdateScoreUI()
    {
        if (perfectCountText != null)
            perfectCountText.text = $"PERFECT: {perfectCount}";

        if (goodCountText != null)
            goodCountText.text = $"GOOD: {goodCount}";

        if (missCountText != null)
            missCountText.text = $"MISS: {missCount}";

        if (totalCountText != null)
            totalCountText.text = $"TOTAL: {totalJudgedCount}";

        if (scoreText != null)
            scoreText.text = $"SCORE: {totalScore}";
    }

    private void Start()
    {
        ShowJudgeUI("");
        UpdateScoreUI();
    }

    private string GetTargetLabel(SignNoteData note)
    {
        if (note == null)
            return "";

        if (!string.IsNullOrWhiteSpace(note.signId))
            return note.signId.Trim();

        return note.keyword.Trim();
    }

    private void HandlePrediction(SignRecognitionResult result)
    {
        SignNoteData note = noteManager.GetCurrentJudgeableNote();

        if (note == null)
        {
            return;
        }

        string targetLabel = GetTargetLabel(note);

        float audioTime = noteManager.GetCurrentTime();
        float judgeTime = noteManager.GetJudgeTime();
        float delta = noteManager.GetTimingDiff(note);

        Debug.Log(
            $"[JudgeDebug] pred={result.label}, target={targetLabel}, " +
            $"keyword={note.keyword}, signId={note.signId}, " +
            $"accepted={result.accepted}, conf={result.confidence:F4}, dist={result.distance:F4}, " +
            $"audio={audioTime:F3}, judge={judgeTime:F3}, note={note.timeSec:F3}, delta={delta:F3}"
        );

        // get average timing delta for correct predictions to help adjust model latency and note offset.
        // delta가 양수면 예측이 늦은 것, 음수면 예측이 빠른 것.
        if (enableTimingStats && result.label == targetLabel)
        {
            matchedCount++;
            matchedDeltaSum += delta;

            if (matchedCount % 10 == 0)
            {
                float avgDelta = matchedDeltaSum / matchedCount;

                Debug.Log(
                    $"[TimingStats] matchedCount={matchedCount}, avgDelta={avgDelta:F3}, " +
                    $"currentModelLatency={noteManager.modelLatencySec:F3}, " +
                    $"suggestedModelLatency={(noteManager.modelLatencySec + avgDelta):F3}, " +
                    $"suggestedNoteOffset={(noteManager.noteTimeOffsetSec + avgDelta):F3}"
                );
            }
        }

        // accepted가 false면 아직 판정하지 않음.
        // 단, 시간이 완전히 지나면 MISS 처리.

        bool sameLabelSoftAccepted =
            allowSameLabelEvenIfRejected &&
            result.label == targetLabel &&
            result.confidence >= sameLabelMinConfidence &&
            (result.distance < 0f || result.distance <= sameLabelMaxDistance);

        if (!result.accepted && !sameLabelSoftAccepted)
        {
            if (noteManager.ShouldMiss(note))
            {
                Debug.Log(
                    $"[SignGameBridge] MISS by timeout: predicted={result.label}, " +
                    $"target={targetLabel}, keyword={note.keyword}, signId={note.signId}, delta={delta:F3}"
                );
                ShowJudgeUI("MISS");
                AddJudgeCount("MISS");
                noteManager.MarkJudged(note);
            }

            return;
        }

        // 정답 예측이면 PERFECT/GOOD/MISS 판정
        if (result.label == targetLabel)
        {
            string judge = noteManager.JudgeTiming(note);

            // 정답이지만 아직 노트 타이밍보다 너무 이르면 GOOD으로 확정하지 않고 기다림
        if (judge == "GOOD" && delta < -noteManager.perfectWindow)
        {
            Debug.Log(
                $"[SignGameBridge] WAIT: correct label but too early. " +
                $"predicted={result.label}, target={targetLabel}, delta={delta:F3}"
            );

            return;
        }

            // 정답이지만 타이밍이 너무 늦은 경우는 MISS
            if (judge == "MISS")
            {
                if (noteManager.ShouldMiss(note))
                {
                    Debug.Log(
                        $"[SignGameBridge] MISS: correct label but late. " +
                        $"predicted={result.label}, target={targetLabel}, " +
                        $"keyword={note.keyword}, signId={note.signId}, delta={delta:F3}"
                    );

                    ShowJudgeUI("MISS");
                    AddJudgeCount("MISS");
                    noteManager.MarkJudged(note);
                }

                return;
            }

            Debug.Log(
                $"[SignGameBridge] {judge}: predicted={result.label}, target={targetLabel}, " +
                $"keyword={note.keyword}, signId={note.signId}, " +
                $"delta={delta:F3}, noteTime={note.timeSec:F3}, softAccepted={sameLabelSoftAccepted}"
            );

            ShowJudgeUI(judge);
            noteManager.MarkJudged(note);
            AddJudgeCount(judge);
            return;            
        }

        // 오답 예측이라고 바로 MISS 처리하지 않음.
        // goodWindow가 끝날 때까지 정답 예측을 기다림.
        if (noteManager.ShouldMiss(note))
        {
            Debug.Log(
                $"[SignGameBridge] MISS by timeout: predicted={result.label}, " +
                $"target={targetLabel}, keyword={note.keyword}, signId={note.signId}, delta={delta:F3}"
            );

            ShowJudgeUI("MISS");
            AddJudgeCount("MISS");
            noteManager.MarkJudged(note);
        }
    }

    private void Update()
    {
        if (!goResultWhenMusicEnds || gameFinished)
            return;

        if (musicAudioSource == null)
            return;

        if (musicAudioSource.isPlaying)
        {
            musicStarted = true;
        }

        if (musicStarted && !musicAudioSource.isPlaying)
        {
            StartCoroutine(FinishGameAfterDelay());
        }
    }

    private IEnumerator FinishGameAfterDelay()
    {
        gameFinished = true;

        yield return new WaitForSeconds(resultDelay);

        FinishGameAndGoResult();
    }

    public void FinishGameAndGoResult()
    {
        if (gameFinished == false)
            gameFinished = true;

        string finalGrade = CalculateGrade();

        GameResultData.SetResult(
            perfectCount,
            goodCount,
            missCount,
            totalScore,
            maxCombo,
            finalGrade
        );

        Debug.Log(
            $"[GameResult] PERFECT={perfectCount}, GOOD={goodCount}, MISS={missCount}, " +
            $"score={totalScore}, maxCombo={maxCombo}, grade={finalGrade}"
        );

        SceneManager.LoadScene(resultSceneName);
    }

    private string CalculateGrade()
    {
        if (totalJudgedCount <= 0)
            return "F";

        int maxPossibleScore = totalJudgedCount * perfectScore;
        float scoreRatio = (float)totalScore / maxPossibleScore;

        if (scoreRatio >= 0.60f)
            return "A";
        else if (scoreRatio >= 0.30f)
            return "B";
        else if (scoreRatio >= 0.10f)
            return "C";
        else
            return "F";
    }
}
