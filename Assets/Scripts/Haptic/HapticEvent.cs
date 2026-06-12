public struct HapticEvent
{
    public float timeSec;
    public string drumType;
    public int motor1;      // 왼손 진동 세기 (0~255)
    public int motor2;      // 오른손 진동 세기 (0~255)
    public int durationMs;
}
