using UnityEngine;
using System;

// 매 프레임의 feature 받아서 버퍼 저장 -> 버퍼 다 차면 추론 호출
public class SignPredictionProvider : MonoBehaviour
{
    public SignTcnRecognizer recognizer;

    [Header("Prediction Settings")]
    public int sequenceLength = 15;
    public int featureDim = 141;
    public float predictionInterval = 0.1f;

    public event Action<SignRecognitionResult> OnPrediction;

    private SignSequenceBuffer buffer;
    private float lastPredictionTime = 0f;

    private void Awake()
    {
        buffer = new SignSequenceBuffer(sequenceLength, featureDim);
    }
    public void AddLandmarkFrame(
        Vector3[] leftHand,
        Vector3[] rightHand,
        Vector3[] facePoints
    )
    {
        float[] feature = SignFeatureExtractor.ExtractFeature(
            leftHand,
            rightHand,
            facePoints
        );

        buffer.AddFrame(feature);
        if (!buffer.IsReady())
        {
            return;
        }
        if (Time.time - lastPredictionTime < predictionInterval)
        {
            return;
        }
        lastPredictionTime = Time.time;

        float[] sequenceInput = buffer.ToFlattenedArray();
        SignRecognitionResult result = recognizer.Predict(sequenceInput);

        Debug.Log(
            $"[SignPredictionProvider] " +
            $"label={result.label}, idx={result.classIndex}, " +
            $"conf={result.confidence:F4}, " +
            $"dist={result.distance:F4}, " +
            $"accepted={result.accepted}"
        );
        OnPrediction?.Invoke(result);
    }

    public void ClearBuffer()
    {
        buffer.Clear();
    }
}
