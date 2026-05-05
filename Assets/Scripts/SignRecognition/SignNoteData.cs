using UnityEngine;
using System;

[Serializable]
public class SignNoteData
{
    public string keyword;
    public float lyricTimeSec;
    public int beatIndex;
    public float timeSec;
    public string signId;
    public string difficulty;
    public bool judged;

    public SignNoteData(
        string keyword,
        float lyricTimeSec,
        int beatIndex,
        float timeSec,
        string signId,
        string difficulty
    )
    {
        this.keyword = keyword;
        this.lyricTimeSec = lyricTimeSec;
        this.beatIndex = beatIndex;
        this.timeSec = timeSec;
        this.signId = signId;
        this.difficulty = difficulty;
        this.judged = false;
    }
}
