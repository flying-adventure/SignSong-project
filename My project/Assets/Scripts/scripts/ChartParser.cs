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
                if (parts.Length < 3) continue;

                string word = parts[1]; // keyword
                string lyricTimeStr = parts[2]; // lyric_time_sec

                if (!float.TryParse(lyricTimeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float startTime))
                {
                    Debug.LogWarning($"[ChartParser] Parse failed: {lyricTimeStr}");
                    continue;
                }

                // 일단 lane은 가운데(1)로
                result.Add(new NoteData
                {
                    time = startTime,
                    word = word,
                    lane = 1
                });
            }
        }

        return result;
    }
}