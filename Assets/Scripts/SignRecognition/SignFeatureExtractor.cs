using UnityEngine;

// 입력받은 MediaPipe 랜드마크(vector3 형태)를 141차원 float[]로 변환
public static class SignFeatureExtractor
{
    private const int HandPointCount = 21;
    private const int FacePointCount = 5;
    private const int FeatureDim = 141;

    public static float[] ExtractFeature(
        Vector3[] leftHand,
        Vector3[] rightHand,
        Vector3[] facePoints
    )
    {
        float[] feature = new float[FeatureDim];
        // step1. 얼굴 탐지
        if (facePoints == null || facePoints.Length < FacePointCount)
        {
            return feature;
        }

        Vector3 nose = facePoints[0];   // 기준점(문서 참조)
        float faceWidth = Vector3.Distance(facePoints[1], facePoints[2]);   // 스케일(문서 참조)
        if (faceWidth < 1e-6f)
        {
            return feature;
        }
        // step2. 손 탐지 및 전체 채우기
        int offset = 0;
        FillHandFeature(feature, offset, leftHand, nose, faceWidth);
        offset += 63;
        FillHandFeature(feature, offset, rightHand, nose, faceWidth);
        offset += 63;
        FillFaceFeature(feature, offset, facePoints, nose, faceWidth);
        return feature;
    }

    private static void FillHandFeature(
        float[] feature,
        int offset,
        Vector3[] hand,
        Vector3 nose,
        float faceWidth
    )
    {
        if(hand == null || hand.Length < HandPointCount)
        {
            for (int i=0; i<63; i++)
            {
                feature[offset+i] = 0f;
            }
            return;
        }

        for (int i=0; i<HandPointCount; i++)
        {
            Vector3 p = NormalizePoint(hand[i], nose, faceWidth);
            int baseIdx = offset + i*3;
            feature[baseIdx] = p.x;
            feature[baseIdx + 1] = p.y;
            feature[baseIdx + 2] = p.z;
        }
    }
    private static void FillFaceFeature(
        float[] feature,
        int offset,
        Vector3[] facePoints,
        Vector3 nose,
        float faceWidth
    )
    {
        for (int i=0; i<FacePointCount; i++)
        {
            Vector3 p = NormalizePoint(facePoints[i], nose, faceWidth);
            int baseIdx = offset + i*3;
            feature[baseIdx] = p.x;
            feature[baseIdx+1] = p.y;
            feature[baseIdx+2] = p.z;
        }
    }

    // 추출 랜드마크 정규화(문서 참조)
    private static Vector3 NormalizePoint(Vector3 point, Vector3 nose, float faceWidth)
    {
        return (point - nose) / faceWidth;
    }
}
