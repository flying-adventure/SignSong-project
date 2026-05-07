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
    public float perfectWindow = 0.25f;
    public float goodWindow = 0.50f;
    public float missWindow = 0.80f;

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
            float timeSec = ParseFloat(cols[4]);
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

        return audioSource.time;
    }

    public SignNoteData GetCurrentJudgeableNote()
    {
        float currentTime = GetCurrentTime();

        SignNoteData bestNote = null;
        float bestDiff = float.MaxValue;

        foreach (SignNoteData note in notes)
        {
            if (note.judged)
                continue;

            float diff = Mathf.Abs(currentTime - note.timeSec);

            if (diff <= missWindow && diff < bestDiff)
            {
                bestDiff = diff;
                bestNote = note;
            }
        }

        return bestNote;
    }

    public string JudgeTiming(SignNoteData note)
    {
        if (note == null)
            return "MISS";

        float diff = Mathf.Abs(GetCurrentTime() - note.timeSec);

        if (diff <= perfectWindow)
            return "PERFECT";

        if (diff <= goodWindow)
            return "GOOD";

        return "MISS";
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
