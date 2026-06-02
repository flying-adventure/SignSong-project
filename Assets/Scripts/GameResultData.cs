public static class GameResultData
{
    public static int perfectCount;
    public static int goodCount;
    public static int missCount;
    public static int totalScore;
    public static int maxCombo;
    public static string grade;

    public static void SetResult(
        int perfect,
        int good,
        int miss,
        int score,
        int combo,
        string resultGrade
    )
    {
        perfectCount = perfect;
        goodCount = good;
        missCount = miss;
        totalScore = score;
        maxCombo = combo;
        grade = resultGrade;
    }
}