using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using Unity.InferenceEngine;

public class SignTcnRecognizer : MonoBehaviour
{
    [Header("Model Files")]
    public string classifierModelPath = "Models/best_tcn.sentis";
    public string embeddingModelPath = "Models/tcn_embedding_model.sentis";
    public string metadataPath = "Models/ood_metadata.json";
    public string centroidsPath = "Models/centroids.json";

    [Header("Thresholds")]
    public bool useMetadataThresholds = false;
    public float confidenceThreshold = 0.80f;
    public float distanceThreshold = 22.0f;

    private Unity.InferenceEngine.Model classifierModel;
    private Unity.InferenceEngine.Model embeddingModel;

    private Unity.InferenceEngine.Worker classifierWorker;
    private Unity.InferenceEngine.Worker embeddingWorker;

    private OodMetadata metadata;
    private float[][] centroids;
    private string[] classNames;

    // 입력 피쳐에 맞춰 설정(문서 참조)
    private const int SequenceLength = 15;
    private const int FeatureDim = 141;

    private void Start()
    {
        LoadModels();
        LoadMetadata();
        DebugLoadedInfo();
    }

    private void LoadModels()
    {
        string classifierFullPath = Path.Combine(Application.streamingAssetsPath, classifierModelPath);
        string embeddingFullPath = Path.Combine(Application.streamingAssetsPath, embeddingModelPath);

        classifierModel = Unity.InferenceEngine.ModelLoader.Load(classifierFullPath);
        embeddingModel = Unity.InferenceEngine.ModelLoader.Load(embeddingFullPath);

        classifierWorker = new Unity.InferenceEngine.Worker(classifierModel, Unity.InferenceEngine.BackendType.GPUCompute);
        embeddingWorker = new Unity.InferenceEngine.Worker(embeddingModel, Unity.InferenceEngine.BackendType.GPUCompute);

        Debug.Log("[SignTcnRecognizer] Sentis models loaded.");
    }

    private void LoadMetadata()
    {
        string metadataFullPath = Path.Combine(Application.streamingAssetsPath, metadataPath);
        string centroidsFullPath = Path.Combine(Application.streamingAssetsPath, centroidsPath);

        string metadataJson = File.ReadAllText(metadataFullPath);
        metadata = JsonConvert.DeserializeObject<OodMetadata>(metadataJson);
        
        string centroidsJson = File.ReadAllText(centroidsFullPath);
        centroids = JsonConvert.DeserializeObject<float[][]>(centroidsJson);

        if (metadata != null)
        {
            classNames = metadata.class_names;

            if (useMetadataThresholds)
            {
                confidenceThreshold = metadata.confidence_threshold;
                distanceThreshold = metadata.distance_threshold;
            }
        }

        Debug.Log("[SignTcnRecognizer] Metadata and centroids loaded.");
    }

    private void DebugLoadedInfo()
    {
        Debug.Log($"[SignTcnRecognizer] confidenceThreshold = {confidenceThreshold}");
        Debug.Log($"[SignTcnRecognizer] distanceThreshold = {distanceThreshold}");

        if (metadata != null)
        {
            Debug.Log($"[SignTcnRecognizer] distancThresholdStdFactor = {metadata.distance_threshold_std_factor}");
        }
        if (classNames != null)
        {
            Debug.Log($"[SignTcnRecognizer] classNames count = {classNames.Length}");
        }
        if (centroids != null && centroids.Length > 0)
        {
            Debug.Log($"[SignTcnRecognizer] centroids count = {centroids.Length}, dim = {centroids[0].Length}");
        }
    }

    public SignRecognitionResult Predict(float[] sequenceInput)
    {
        if (sequenceInput == null || sequenceInput.Length != SequenceLength * FeatureDim)
        {
            Debug.LogError($"[SignTcnRecognizer] Invalid input length: {sequenceInput?.Length}");
            return SignRecognitionResult.Reject(-1, "InvalidInput", 0f, -1f);
        }

        float[] probabilities = RunClassifier(sequenceInput);

        int predIdx = ArgMax(probabilities);   // 매핑 예측한 수어 클래스 인덱스
        float confidence = probabilities[predIdx];   // 매핑 예측 확신도
        string label = GetLabel(predIdx);   // 매핑 예측한 수어 클래스 이름

        // 1차 OOD 판정: 예측 확신도가 기준 미만인 경우 거부
        if(confidence < confidenceThreshold)
        {
            return SignRecognitionResult.Reject(predIdx, label, confidence, -1f);
        }

        float[] embedding = RunEmbedding(sequenceInput);

        // 2차 OOD 판정: 예측 클래스의 중점과 임베딩 간 L2 거리 계산 후 기준 초과인 경우 거부
        float distance = L2Distance(embedding, centroids[predIdx]);

        if (distance > distanceThreshold)
        {
            return SignRecognitionResult.Reject(predIdx, label, confidence, distance);
        }
        // 최종 매핑
        return SignRecognitionResult.Accept(predIdx, label, confidence, distance);
    }

    // 분류기 실행 및 예측
    private float[] RunClassifier(float[] sequenceInput)
    {
        TensorShape shape = new TensorShape(1, SequenceLength, FeatureDim);

        using Tensor<float> inputTensor = new Tensor<float>(shape, sequenceInput);

        classifierWorker.Schedule(inputTensor);

        Tensor<float> outputTensor = classifierWorker.PeekOutput() as Tensor<float>;
        Tensor<float> cpuOutput = outputTensor.ReadbackAndClone();

        float[] result = new float[cpuOutput.shape.length];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = cpuOutput[i];
        }

        cpuOutput.Dispose();

        return result;
    }

    // 임베딩 실행 및 계산
    private float[] RunEmbedding(float[] sequenceInput)
    {
        TensorShape shape = new TensorShape(1, SequenceLength, FeatureDim);

        using Tensor<float> inputTensor = new Tensor<float>(shape, sequenceInput);

        embeddingWorker.Schedule(inputTensor);

        Tensor<float> outputTensor = embeddingWorker.PeekOutput() as Tensor<float>;
        Tensor<float> cpuOutput = outputTensor.ReadbackAndClone();

        float[] result = new float[cpuOutput.shape.length];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = cpuOutput[i];
        }

        cpuOutput.Dispose();

        return result;
    }

    private int ArgMax(float[] values)
    {
        int maxIdx = 0;
        float maxVal = values[0];

        for (int i=1; i<values.Length; i++)
        {
            if(values[i] > maxVal)
            {
                maxVal = values[i];
                maxIdx = i;
            }
        }
        return maxIdx;
    }

    private float L2Distance(float[] a, float[] b)
    {
        if (a==null || b==null || a.Length != b.Length)
        {
            Debug.LogError($"[SignTcnRecognizer] L2 distance dim mismatch.");
            return float.MaxValue;
        }
        float sum = 0f;
        for (int i=0; i<a.Length; i++)
        {
            float diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Mathf.Sqrt(sum);
    }

    private string GetLabel(int idx)
    {
        if (classNames == null || idx < 0 || idx >= classNames.Length)
        {
            return $"class_{idx}";
        }
        return classNames[idx];
    }

    private void OnDestroy()
    {
        classifierWorker?.Dispose();
        embeddingWorker?.Dispose();
    }
}
