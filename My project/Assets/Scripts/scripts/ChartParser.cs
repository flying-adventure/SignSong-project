using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class ChartParser
{
    public static List<NoteData> Parse(string text)
    {
        var result = new List<NoteData>();

        using (var reader = new StringReader(text))
        {
            string line;
            bool isFirst = true;

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // 헤더 라인 스킵
                if (isFirst)
                {
                    isFirst = false;
                    if (line.StartsWith("song_id")) // song_id,keyword,lyric_time_sec,...
                        continue;
                }

                var parts = line.Split(',');
                // lane 포함해서 최소 9개 컬럼 있어야 함
                if (parts.Length < 3)
                {
                    Debug.LogWarning($"[ChartParser] parts length < 3: {line}");
                    continue;
                }

                string word = parts[1];           // keyword
                string lyricTimeStr = parts[2];   // lyric_time_sec

                if (!float.TryParse(lyricTimeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float startTime))
                {
                    Debug.LogWarning($"[ChartParser] Parse failed (lyric_time_sec): {lyricTimeStr}");
                    continue;
                }

                //  lane 컬럼 읽기 (없으면 기본 1)
                int lane = 1; // default middle
                if (parts.Length >= 9)
                {
                    string laneStr = parts[8]; // lane 컬럼
                    if (!int.TryParse(laneStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out lane))
                    {
                        lane = 1;
                        Debug.LogWarning($"[ChartParser] lane parse failed, use default 1. laneStr={laneStr}");
                    }
                }

                result.Add(new NoteData
                {
                    time = startTime,
                    word = word,
                    lane = lane
                });
            }
        }

        return result;
    }
}