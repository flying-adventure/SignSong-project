using System;
using System.Collections.Generic;
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
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("#")) continue; // 주석

                // "0:05 안녕" → ["0:05", "안녕"]
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                float time = ParseTimeToSeconds(parts[0]);
                string word = parts[1];

                result.Add(new NoteData { time = time, word = word });
            }
        }

        return result;
    }

    private static float ParseTimeToSeconds(string timeStr)
    {
        // "1:31.5" 형태 지원
        var minSec = timeStr.Split(':');
        int min = int.Parse(minSec[0]);

        float sec = float.Parse(
            minSec[1],
            System.Globalization.CultureInfo.InvariantCulture
        );

        return min * 60f + sec;
    }
}