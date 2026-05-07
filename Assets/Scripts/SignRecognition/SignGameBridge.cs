using UnityEngine;
using System.Collections;

public class SignGameBridge : MonoBehaviour
{
    public SignPredictionProvider predictionProvider;

    [Header("Test Target")]
    // public string currentTargetLabel = "바람";

    [Header("Note Manager")]
    public SignNoteManager noteManager;

    [Header("Judge Settings")]
    public float judgeCooldown = 0.8f;
    private float lastJudgeTime = -999f;

    [Header("Judge UI")]
    public GameObject perfectObject;
    public GameObject goodObject;
    public GameObject missObject;
    public float judgeDisplayTime = 0.5f;

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

    private void Start()
    {
        ShowJudgeUI("");
    }

    private void HandlePrediction(SignRecognitionResult result)
    {
        /*
        Debug.Log(
            $"[SignGameBridge] received label={result.label}, " +
            $"accepted={result.accepted}, " +
            $"conf={result.confidence:F4}, dist={result.distance:F4}"
        );

        if (!result.accepted)
        {
            return;
        }
        if (isJudgeShowing)
        {
            return;
        }
        if (Time.time - lastJudgeTime < judgeCooldown)
        {
            return;
        }

        lastJudgeTime = Time.time;

        if (result.label == currentTargetLabel)
        {
            Debug.Log($"[SignGameBridge] PERFECT: {result.label}");
            ShowJudgeUI("PERFECT");
        }
        else
        {
            Debug.Log($"[SignGameBridge] MISS: predicted={result.label}, target={currentTargetLabel}");
            ShowJudgeUI("MISS");
        }
        */
        SignNoteData note = noteManager.GetCurrentJudgeableNote();

        if (note == null)
        {
            /*
            if (enableDebugLog)
            {
                Debug.Log("[SignGameBridge] No judgeable note.");
            }
            */
            return;
        }

        if (result.label == note.keyword)
        {
            string judge = noteManager.JudgeTiming(note);

            Debug.Log($"[SignGameBridge] {judge}: predicted={result.label}, target={note.keyword}, time={note.timeSec}");

            ShowJudgeUI(judge);
            noteManager.MarkJudged(note);
        }
        else
        {
            Debug.Log($"[SignGameBridge] MISS: predicted={result.label}, target={note.keyword}");

            ShowJudgeUI("MISS");
            noteManager.MarkJudged(note);
        }
    }
}
