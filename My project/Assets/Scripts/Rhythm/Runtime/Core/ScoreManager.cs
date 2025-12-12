using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public int perfectCount { get; private set; }
    public int goodCount { get; private set; }
    public int missCount { get; private set; }
    public int combo { get; private set; }
    public int maxCombo { get; private set; }

    public void ResetScore()
    {
        perfectCount = goodCount = missCount = 0;
        combo = maxCombo = 0;
    }

    public void OnJudge(JudgeEvent e)
    {
        switch (e.result)
        {
            case JudgeResult.Perfect:
                perfectCount++; combo++; break;
            case JudgeResult.Good:
                goodCount++; combo++; break;
            case JudgeResult.Miss:
                missCount++; combo = 0; break;
        }
        if (combo > maxCombo) maxCombo = combo;

        // UI 갱신
        // ui.SetCounts(perfectCount, goodCount, missCount, combo, maxCombo);
        Debug.Log($"Judge: {e.result} noteId={e.noteId} dt={e.dt:F3} combo={combo} (P/G/M={perfectCount}/{goodCount}/{missCount})");
    }
}