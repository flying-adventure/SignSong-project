using System;

[Serializable]
public class NoteData
{
    public float time;      // 시작 시간 (lyric_time_sec)
    public float endTime;   // 이 노트가 끝나는 시간 (= 다음 단어 시작시간)
    public string word;     // 단어
    public int lane;        // 0 = Left, 1 = Middle, 2 = Right (지금은 일단 1로 써도 됨)
}