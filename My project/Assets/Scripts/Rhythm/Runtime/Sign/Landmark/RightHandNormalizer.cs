using UnityEngine;

/// <summary>
/// 오른손 21개 관절을 사용자 정규화 로직으로 정규화.
/// 정규화 방식:
/// 1. wrist(data[0])를 원점으로
/// 2. middle_mcp(data[9])의 노름으로 스케일
/// 3. 평탄화하여 반환 (63 = 21*3)
/// </summary>
public class RightHandNormalizer
{
    /// <summary>
    /// 21개 관절을 정규화.
    /// landmarks: Vector3[21], 원점은 손목
    /// 반환: float[63] = 21*3
    /// </summary>
    public static float[] NormalizeLandmarks(Vector3[] landmarks)
    {
        if (landmarks == null || landmarks.Length < 21)
        {
            Debug.LogWarning("[RightHandNormalizer] landmarks null or length < 21");
            return new float[63]; // zero array
        }

        // 1) wrist(index 0)를 기준으로 뺌
        Vector3[] data = new Vector3[21];
        Vector3 wrist = landmarks[0];
        for (int i = 0; i < 21; i++)
        {
            data[i] = landmarks[i] - wrist;
        }

        // 2) middle_mcp(index 9)의 노름으로 스케일
        float scale = data[9].magnitude;
        if (scale < 1e-6f) scale = 1e-6f;

        for (int i = 0; i < 21; i++)
        {
            data[i] /= scale;
        }

        // 3) 평탄화
        float[] result = new float[63];
        int k = 0;
        for (int i = 0; i < 21; i++)
        {
            result[k++] = data[i].x;
            result[k++] = data[i].y;
            result[k++] = data[i].z;
        }

        return result;
    }
}
