using System;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections;

public class SignPredictionProvider : MonoBehaviour
{
    [Header("Models (StreamingAssets relative)")]
    [Tooltip("Classifier model (.tflite)")]
    public string clsModelRelPath = "SignModels/best_cnn_gru2_model_word_split.tflite";

    [Tooltip("Embedding model (.tflite). 비우면 OOD gate를 사용하지 않음")]
    public string embModelRelPath = "SignModels/embedding_model_word_split.tflite";

    public int threads = 2;

    [Header("Meta (StreamingAssets relative)")]
    public string metaJsonRelPath = "SignModels/sign_meta.json";

    [Header("Gating (Classifier)")]
    public float minProb = 0.2f;

    [Header("OOD Gating (Embedding + centroid distance)")]
    public bool useOodGate = false;

    [Tooltip("meta.distanceThreshold를 쓰고 싶으면 -1로 두세요.")]
    public float overrideDistanceThreshold = -1f;

    [Header("Voting (Stabilization)")]
    public int votingWindow = 3;
    public int minVotes = 2;

    [Header("Emit")]
    public float emitCooldownSec = 0.25f; // 같은 sign 연속 방지

    [Header("Refs")]
    public AudioClock clock;
    public JudgementEngine engine;

    [Header("Landmark Source")]
    public MonoBehaviour landmarkSourceBehaviour; // DummyLandmarkSource / MediaPipeLandmarkSource 등 드래그
    private ILandmarkSource landmarkSource;

    [Header("Debug")]
    public bool debugLogs = true;
    public float debugDropEverySec = 1.0f;   // Drop 로그는 1초에 1번만
    public float debugPredEverySec = 0.5f;   // Pred 로그는 0.5초에 1번만
    private float _nextDropLogTime = 0f;
    private float _nextPredLogTime = 0f;

    // Runners
    private TFLiteSignRunner clsRunner;
    private TFLiteSignRunner embRunner;

    // Sequence
    private SequenceBuffer seq;

    // Meta
    private SignMeta meta;
    private bool metaReady = false;

    // Emit state
    private float lastEmitTime = -999f;
    private int lastEmitLabel = -1;

    // Voting ring
    private int[] voteRing;
    private int votePos = 0;
    private int voteCount = 0;

    private const int FeatDim = 141;

    void Awake()
    {
        clsRunner = new TFLiteSignRunner();
        embRunner = new TFLiteSignRunner();
        seq = new SequenceBuffer();

        voteRing = new int[Mathf.Max(1, votingWindow)];
        for (int i = 0; i < voteRing.Length; i++) voteRing[i] = -1;

        landmarkSource = landmarkSourceBehaviour as ILandmarkSource;
        if (landmarkSource == null)
        {
            Debug.LogError("[SignPredictionProvider] landmarkSourceBehaviour가 ILandmarkSource를 구현하지 않았거나 비어있습니다.");
        }
    }

    void Start()
    {
        clsRunner.LoadFromStreamingAssets(clsModelRelPath, threads);

        // Embedding runner는 옵션
        if (!string.IsNullOrEmpty(embModelRelPath) && useOodGate)
        {
            embRunner.LoadFromStreamingAssets(embModelRelPath, threads);
        }

        StartCoroutine(LoadMetaCoroutine());
    }

    void OnDestroy()
    {
        clsRunner?.Dispose();
        embRunner?.Dispose();
    }

    IEnumerator LoadMetaCoroutine()
    {
        var metaPath = Path.Combine(Application.streamingAssetsPath, metaJsonRelPath);

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var req = UnityWebRequest.Get(metaPath))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Meta] load failed: {metaPath}\n{req.error}");
                yield break;
            }
            meta = JsonUtility.FromJson<SignMeta>(req.downloadHandler.text);
        }
#else
        if (!File.Exists(metaPath))
        {
            Debug.LogError($"[Meta] file not found: {metaPath}");
            yield break;
        }
        meta = JsonUtility.FromJson<SignMeta>(File.ReadAllText(metaPath));
#endif
        metaReady = (meta != null);
        if (metaReady)
        {
            Debug.Log($"[Meta] classes={meta.ClassCount}, centroidDim={meta.centroidDim}, thr={meta.distanceThreshold}");

            if (engine != null)
                engine.SetMeta(meta);
        }
    }

    static float SoftmaxMaxProb(float[] x, int maxIdx)
    {
        float m = x[maxIdx];
        double sum = 0.0;
        for (int i = 0; i < x.Length; i++) sum += Math.Exp(x[i] - m);
        return (float)(1.0 / sum);
    }

    void Update()
    {
        if (landmarkSource == null || clock == null || engine == null) return;

        // 1) 141 feature
        var feat = landmarkSource.GetFeature141();
        if (feat == null || feat.Length != FeatDim) return;

        // 2) push sequence
        seq.Push(feat);
        if (!seq.IsFull) return;

        // 3) classifier infer
        var logits = clsRunner.Run(seq.Snapshot());
        if (logits == null || logits.Length == 0) return;

        int bestIdx = 0;
        float bestLogit = logits[0];
        for (int i = 1; i < logits.Length; i++)
        {
            if (logits[i] > bestLogit) { bestLogit = logits[i]; bestIdx = i; }
        }

        float bestProb = SoftmaxMaxProb(logits, bestIdx);

        if (debugLogs && Time.unscaledTime >= _nextPredLogTime)
        {
            _nextPredLogTime = Time.unscaledTime + debugPredEverySec;

            string rawLabel =
                (metaReady && meta.classNames != null &&
                bestIdx >= 0 && bestIdx < meta.classNames.Length)
                ? meta.classNames[bestIdx]
                : $"#{bestIdx}";

            float t = (clock != null) ? clock.NowSec() : 0f;
            Debug.Log(
                $"[PredRaw] t={t:F2} idx={bestIdx} label={rawLabel} prob={bestProb:F3}"
            );
        }

        if (bestProb < minProb)
        {
            if (debugLogs && Time.unscaledTime >= _nextDropLogTime)
            {
                _nextDropLogTime = Time.unscaledTime + debugDropEverySec;
                Debug.Log($"[Drop] LOW_PROB prob={bestProb:F3} < {minProb:F3} bestIdx={bestIdx}");
            }
            return;
        }

        // 4) embedding + distance gate (optional)
        float dist = 0f;
        bool oodEnabled = useOodGate && !string.IsNullOrEmpty(embModelRelPath) && metaReady;
        bool locked = !oodEnabled;

        if (oodEnabled)
        {
            var emb = embRunner.Run(seq.Snapshot());
            if (emb == null || emb.Length != meta.centroidDim)
            {
                locked = true;
            }
            else
            {
                dist = L2DistanceToCentroid(emb, bestIdx, meta);
                float thr = (overrideDistanceThreshold > 0f) ? overrideDistanceThreshold : meta.distanceThreshold;

                // dist gate: 멀면 reject
                if (dist > thr)
                {
                    if (debugLogs && Time.unscaledTime >= _nextDropLogTime)
                    {
                        _nextDropLogTime = Time.unscaledTime + 1.0f;
                        float absMean = 0f;
                        for (int i = 0; i < feat.Length; i++) absMean += Mathf.Abs(feat[i]);
                        absMean /= feat.Length;
                        Debug.Log($"[Feat] absMean={absMean:F4} first3=({feat[0]:F3},{feat[1]:F3},{feat[2]:F3})");
                    }
                    return;
                }

                locked = true; // dist 통과했으면 "확신" 플래그로 써도 됨
            }
        }

        // 5) voting (stabilize)
        PushVote(bestIdx);
        if (voteCount < voteRing.Length) return;

        int voted = MajorityVote(out int votedCount);
        if (voted < 0 || votedCount < minVotes) return;

        // 6) emit cooldown / anti-duplicate
        float now = clock.NowSec(); // AudioClock 구현에 맞춰 괄호 유무만 조정
        if (voted == lastEmitLabel && (now - lastEmitTime) < emitCooldownSec) return;

        lastEmitLabel = voted;
        lastEmitTime = now;

        string label = (metaReady && voted >= 0 && voted < meta.ClassCount && meta.classNames != null)
            ? meta.classNames[voted]
            : $"#{voted}";

        if (debugLogs)
        {
            Debug.Log(
                $"[PredEmit] t={now:F2} idx={voted} label={label} " +
                $"prob={bestProb:F2} dist={dist:F2} locked={locked} " +
                $"votes={votedCount}/{votingWindow}"
            );
        }

        // 7) Push once
        engine.PushPrediction(new Prediction
        {
            timeSec = now,
            idx = voted,
            signId = voted,
            label = label,
            prob = bestProb,
            score = bestLogit,
            dist = dist,
            locked = locked
        });
    }

    private void PushVote(int label)
    {
        if (voteRing == null || voteRing.Length == 0) return;

        voteRing[votePos] = label;
        votePos = (votePos + 1) % voteRing.Length;

        if (voteCount < voteRing.Length) voteCount++;
    }

    private int MajorityVote(out int bestCount)
    {
        bestCount = 0;
        int bestLabel = -1;

        // small window라 O(n^2)로 단순 처리해도 충분
        for (int i = 0; i < voteRing.Length; i++)
        {
            int a = voteRing[i];
            if (a < 0) continue;

            int c = 0;
            for (int j = 0; j < voteRing.Length; j++)
            {
                if (voteRing[j] == a) c++;
            }

            if (c > bestCount)
            {
                bestCount = c;
                bestLabel = a;
            }
        }

        return bestLabel;
    }

    private static float L2DistanceToCentroid(float[] emb, int clsIdx, SignMeta meta)
    {
        int dim = meta.centroidDim;
        int baseOff = clsIdx * dim;

        // meta.centroidsFlat: [classCount * dim]
        if (meta.centroidsFlat == null || meta.centroidsFlat.Length < baseOff + dim) return 0f;

        double sum = 0.0;
        for (int i = 0; i < dim; i++)
        {
            float d = emb[i] - meta.centroidsFlat[baseOff + i];
            sum += d * d;
        }
        return (float)Math.Sqrt(sum);
    }
}