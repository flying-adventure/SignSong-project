using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class NoteLoader : MonoBehaviour
{
    [Header("CSV in Resources (without extension)")]
    public string resourcePath = "Notes/dreamlovehope_notes"; // Resources/Notes/dreamlovehope_notes.csv

    [Header("Which column names to use")]
    public string timeColumn = "time_sec";
    public string idxColumn  = "model_class_idx";   // 수어 클래스 인덱스
    public string labelColumn = "keyword";
    public string fallbackLabelColumn = "keyword";

    public List<Note> LoadNotes()
    {
        var ta = Resources.Load<TextAsset>(resourcePath);
        if (ta == null)
        {
            Debug.LogError($"[NoteLoader] CSV not found: Resources/{resourcePath}.csv");
            return new List<Note>();
        }

        return ParseCsvToNotes(ta.text);
    }

    private List<Note> ParseCsvToNotes(string csv)
    {
        var notes = new List<Note>();

        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            Debug.LogError("[NoteLoader] CSV has no data rows.");
            return notes;
        }

        // 헤더 파싱
        var header = SplitCsvLine(lines[0]);
        var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Length; i++) col[header[i].Trim()] = i;

        int GetIdx(string name) => col.TryGetValue(name, out var idx) ? idx : -1;

        int timeIdx = GetIdx(timeColumn);
        if (timeIdx < 0) timeIdx = GetIdx("timeSec");
        int classIdx = GetIdx(idxColumn);

        int labelIdx = GetIdx(labelColumn);
        if (labelIdx < 0) labelIdx = GetIdx(fallbackLabelColumn);

        if (timeIdx < 0)
        {
            Debug.LogError($"[NoteLoader] time column not found. Tried: {timeColumn}");
            return notes;
        }
        if (classIdx < 0)
        {
            Debug.LogError($"[NoteLoader] idx column not found. Tried: {idxColumn}");
            return notes;
        }

        int noteId = 0;
        for (int r = 1; r < lines.Length; r++)
        {
            var row = SplitCsvLine(lines[r]);
            if (row.Length <= Math.Max(timeIdx, classIdx)) continue;

            if (!TryParseFloat(row[timeIdx], out float t)) continue;
            if (!TryParseInt(row[classIdx], out int expectedIdx)) continue;

            string label = (labelIdx >= 0 && labelIdx < row.Length) ? row[labelIdx].Trim() : "";

            notes.Add(new Note
            {
                noteId = noteId++,
                timeSec = t,
                expectedIdx = expectedIdx,
                expectedLabel = label,
                judged = false
            });
        }

        notes.Sort((a, b) => a.timeSec.CompareTo(b.timeSec));
        Debug.Log($"[NoteLoader] Loaded notes: {notes.Count}");
        return notes;
    }

    private static bool TryParseFloat(string s, out float v)
    {
        // CSV 구분자
        s = s.Trim().Replace(",", ".");
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    private static bool TryParseInt(string s, out int v)
        => int.TryParse(s.Trim(), out v);

    // 단순한 CSV split
    private static string[] SplitCsvLine(string line)
        => line.Split(',');
}