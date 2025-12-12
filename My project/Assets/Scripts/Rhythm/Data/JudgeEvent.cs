public enum JudgeResult { Perfect, Good, Miss }

public struct JudgeEvent
{
    public int noteId;
    public float noteTime;
    public float hitTime;

    public int expectedIdx;
    public int predictedIdx;

    public float dt;
    public float prob;
    public float dist;

    public JudgeResult result;
}