using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class HapticCsvParser
{
    public static List<HapticEvent> ParseFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[HapticCsvParser] 파일 없음: {filePath}");
            return new List<HapticEvent>();
        }
        return Parse(File.ReadAllText(filePath));
    }

    public static List<HapticEvent> Parse(string csvText)
    {
        var result = new List<HapticEvent>();
        using var reader = new StringReader(csvText);
        string line;
        bool isHeader = true;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 헤더 스킵
            if (isHeader)
            {
                isHeader = false;
                if (line.StartsWith("time_sec")) continue;
            }

            var parts = line.Split(',');
            // 컬럼: time_sec, drum_type, motor_id, intensity, duration_ms, motor1, motor2
            if (parts.Length < 7) continue;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float timeSec)) continue;
            if (!int.TryParse(parts[4], out int durationMs)) continue;
            if (!int.TryParse(parts[5], out int motor1)) continue;
            if (!int.TryParse(parts[6], out int motor2)) continue;

            result.Add(new HapticEvent
            {
                timeSec   = timeSec,
                drumType  = parts[1],
                motor1    = motor1,
                motor2    = motor2,
                durationMs = durationMs
            });
        }

        return result;
    }
}
