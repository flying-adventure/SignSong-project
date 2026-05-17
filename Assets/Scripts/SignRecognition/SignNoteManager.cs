using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public class SignNoteManager : MonoBehaviour
{
    [Header("CSV")]
    public string csvPath = "Charts/final_cherryblossom_mapping_table_auto.csv";

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Judge Windows")]
    public float perfectWindow = 0.30f;
    public float goodWindow = 0.80f;
    public float missWindow = 1.20f;

    [Header("Timing Calibration")]
    public float noteTimeOffsetSec = 0.0f;
    public float modelLatencySec = 0.0f;

    private readonly List<SignNoteData> notes = new List<SignNoteData>();

    public IReadOnlyList<SignNoteData> Notes => notes;

    private void Start()
    {
        LoadCsv();
    }

    private void LoadCsv()
    {
        notes.Clear();

        string fullPath = Path.Combine(Application.streamingAssetsPath, csvPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[SignNoteManager] CSV not found: {fullPath}");
            return;
        }

        string[] lines = File.ReadAllLines(fullPath);

        if (lines.Length <= 1)
        {
            Debug.LogError("[SignNoteManager] CSV is empty or header only.");
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] cols = line.Split(',');

            if (cols.Length < 7)
            {
                Debug.LogWarning($"[SignNoteManager] Invalid row {i}: {line}");
                continue;
            }

            string keyword = cols[1].Trim();
            float lyricTimeSec = ParseFloat(cols[2]);
            int beatIndex = ParseInt(cols[3]);
            float timeSec = ParseFloat(cols[4]) + noteTimeOffsetSec;
            string signId = cols[5].Trim();
            string difficulty = cols[6].Trim();

            notes.Add(new SignNoteData(
                keyword,
                lyricTimeSec,
                beatIndex,
                timeSec,
                signId,
                difficulty
            ));
        }

        notes.Sort((a, b) => a.timeSec.CompareTo(b.timeSec));

        Debug.Log($"[SignNoteManager] Loaded notes: {notes.Count}");
    }

    public float GetCurrentTime()
    {
        if (audioSource == null)
            return Time.time;

        return audioSource.time;   // 현재 음악 시간
    }

    public float GetJudgeTime()
    {
        return GetCurrentTime() - modelLatencySec;   // 판정에 사용할 보정된 시간
        // modelLatencySec: 모델 인식(예측)이 실제 동작보다 늦게 들어오는 시간
    }

    public SignNoteData GetCurrentJudgeableNote()
    {
        float judgeTime = GetJudgeTime();

        foreach (SignNoteData note in notes)
        {
            if (note.judged)
                continue;

            // 아직 판정 구간에 들어오기 전이면 더 볼 필요 없음
            if (judgeTime < note.timeSec - missWindow)
                return null;

            // 현재 판정 가능한 노트
            if (judgeTime <= note.timeSec + missWindow)
                return note;

            // 이미 missWindow를 지나버린 노트도 반환
            // SignGameBridge에서 MISS 처리할 수 있게 함
            return note;
        }

        return null;
    }

    public string JudgeTiming(SignNoteData note)
    {
        if (note == null)
            return "MISS";

        float diff = Mathf.Abs(GetJudgeTime() - note.timeSec);

        if (diff <= perfectWindow)
            return "PERFECT";

        if (diff <= goodWindow)
            return "GOOD";

        return "MISS";
    }

    // MISS 확정용 함수 추가
    public bool ShouldMiss(SignNoteData note)
    {
        if (note == null)
            return false;

        float judgeTime = GetJudgeTime();

        return judgeTime > note.timeSec + missWindow;
    }

    // 디버그 로그 함수 추가하기
    public float GetTimingDiff(SignNoteData note)
    {
        if (note == null)
            return 999f;

        return GetJudgeTime() - note.timeSec;
    }

    public void MarkJudged(SignNoteData note)
    {
        if (note != null)
            note.judged = true;
    }

    private float ParseFloat(string value)
    {
        float.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float result
        );

        return result;
    }

    private int ParseInt(string value)
    {
        int.TryParse(value.Trim(), out int result);
        return result;
    }
}
