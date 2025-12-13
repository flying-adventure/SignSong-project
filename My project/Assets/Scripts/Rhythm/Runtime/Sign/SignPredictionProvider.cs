using System;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections;

public class SignPredictionProvider : MonoBehaviour
{
    [Header("Mode Selection")]
    [Tooltip("false = 단어 모델 (좌+우+얼굴, 141dim) | true = 지화 모델 (오른손만, 63dim)")]
    public bool useSpellMode = false;

    [Tooltip("체크하면 Scene 이름에 'Game_1'일 때 자동으로 지화 모드 활성화")]
    public bool autoDetectModeFromScene = true;
    
    [Header("Models (StreamingAssets relative)")]
    [Tooltip("Classifier model (.tflite)")]
    public string clsModelRelPath = "SignModels/best_cnn_gru2_model_word_split.tflite";

    [Tooltip("Spell/Right-hand-only classifier model (.tflite) - useSpellMode=true일 때 사용")]
    public string spellClsModelRelPath = "SignModels/best_cnn_gru_model_unity_spell.tflite";

    [Tooltip("Embedding model (.tflite). 비우면 OOD gate를 사용하지 않음")]
    public string embModelRelPath = "SignModels/embedding_model_word_split.tflite";

    [Tooltip("Spell embedding model (.tflite) - useSpellMode=true & useOodGate=true일 때 사용")]
    public string spellEmbModelRelPath = "SignModels/embedding_model_unity_spell.tflite";

    public int threads = 2;

    [Header("Meta (StreamingAssets relative)")]
    public string metaJsonRelPath = "SignModels/sign_meta.json";

    [Tooltip("Spell meta (지화/오른손) - useSpellMode=true일 때 사용")]
    public string spellMetaJsonRelPath = "SignModels/sign_meta_spell.json";

    [Header("Gating (Classifier)")]
    public float minProbWord = 0.03f;
    public float minProbSpell = 0.06f;

    // 현재 모드에 맞는 minProb 자동 선택
    private float MinProb => useSpellMode ? minProbSpell : minProbWord;

    [Header("OOD Gating (Embedding + centroid distance)")]
    public bool useOodGate = false;

    [Tooltip("meta.distanceThreshold를 쓰고 싶으면 -1로 두세요.")]
    public float overrideDistanceThreshold = -1f;

    [Header("Voting (Stabilization)")]
    public int votingWindow = 1;
    public int minVotes = 1;

    [Header("Emit")]
    public float emitCooldownSec = 0.05f; // 같은 sign 연속 방지

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

    [Header("Test Mode (카메라 없이 테스트)")]
    [Tooltip("체크하면 카메라 대신 더미 데이터로 테스트")]
    public bool useTestMode = false;
    private float testModeTimer = 0f;

    // Runners (generic to support word/spell dims)
    private TFLiteGenericRunner clsRunner;
    private TFLiteGenericRunner embRunner;

    // Sequences (word & spell 모드용 별도 버퍼)
    private SequenceBuffer seqWord;   // 141 dims
    private SequenceBuffer seqSpell;  // 63 dims
    private SequenceBuffer seq { get { return useSpellMode ? seqSpell : seqWord; } }

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
    [Header("Spell Mode")]
    [Tooltip("Decision interval (seconds) for spell mode")]
    public float decisionIntervalSec = 3.0f;
    private float decisionTimer = 0f;

    [Header("Spell Mode Runtime")]
    [Tooltip("true면 리듬게임용: 매 프레임(슬라이딩) 추론 + voting/cooldown 사용. false면 기존 3초 단발/lock 모드.")]
    public bool spellRealtimeForRhythm = true;

    [Tooltip("Realtime일 때 최소 누적 프레임 비율(SeqLen의 몇 %부터 추론 시작할지)")]
    [Range(0.1f, 1.0f)]
    public float spellMinBufferRatio = 0.5f;

    // spell lock: once a spell is accepted, hold until hand disappears
    private bool spellLocked = false;
    private int spellLockedLabel = -1;

    private bool clsReady = false;

    private int wordMissingFrames = 0;
    public int wordResetAfterMissingFrames = 10; // 약 10프레임 연속 누락되면 리셋(적당히 조절)

    private void TryInitClsRunnerForCurrentMode()
    {
        if (clsRunner == null) clsRunner = new TFLiteGenericRunner();
        if (embRunner == null) embRunner = new TFLiteGenericRunner(); // OOD 켤 때 필요

        if (clsReady && clsRunner.IsReady) return;

        var modelPath = useSpellMode ? spellClsModelRelPath : clsModelRelPath;
        int feat = useSpellMode ? 63 : 141;
        int outDim = useSpellMode ? 40 : 15;

        clsRunner.LoadFromStreamingAssets(modelPath, threads, feat, outDim);
        clsReady = clsRunner.IsReady;

        if (debugLogs) Debug.Log($"[CLS] loaded mode={(useSpellMode ? "SPELL" : "WORD")} feat={feat} out={outDim} ready={clsReady}");
    }

    private void ResetWordBuffers()
    {
        // seqWord가 아직 안 만들어졌을 수 있음
        seqWord?.Clear();

        if (voteRing != null)
        {
            for (int i = 0; i < voteRing.Length; i++) voteRing[i] = -1;
        }
        votePos = 0;
        voteCount = 0;
    }

    void Awake()
    {
        // Word mode: 141 dims
        seqWord  = new SequenceBuffer(TFLiteGenericRunner.SeqLen, 141);
        // Spell mode: 63 dims (right-hand only)
        seqSpell = new SequenceBuffer(TFLiteGenericRunner.SeqLen, 63);

        voteRing = new int[Mathf.Max(1, votingWindow)];
        for (int i = 0; i < voteRing.Length; i++) voteRing[i] = -1;

        landmarkSource = landmarkSourceBehaviour as ILandmarkSource;
        if (landmarkSource == null)
        {
            Debug.LogError("[SignPredictionProvider] landmarkSourceBehaviour가 ILandmarkSource를 구현하지 않았거나 비어있습니다.");
        }

        // Scene 이름으로 자동 감지
        if (autoDetectModeFromScene)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "Game_1")
            {
                useSpellMode = true;
                Debug.Log($"[SignPredictionProvider] Scene '{sceneName}'에서 SPELL 모드 자동 활성화");
            }
            else
            {
                useSpellMode = false;
                Debug.Log($"[SignPredictionProvider] Scene '{sceneName}'에서 WORD 모드 사용");
            }
        }
    }

    void Start()
    {
        var modelPath = useSpellMode ? spellClsModelRelPath : clsModelRelPath;
        int clsFeat = useSpellMode ? 63 : 141;
        int clsOut = useSpellMode ? 40 : 15; // word=15 classes, spell=40 classes

        TryInitClsRunnerForCurrentMode();

        // Embedding runner will be loaded after meta is available (centroid dim known)
        StartCoroutine(LoadMetaCoroutine());
    }

    void OnDestroy()
    {
        clsRunner?.Dispose();
        embRunner?.Dispose();
    }

    IEnumerator LoadMetaCoroutine()
    {
        var metaPath = useSpellMode 
            ? Path.Combine(Application.streamingAssetsPath, spellMetaJsonRelPath)
            : Path.Combine(Application.streamingAssetsPath, metaJsonRelPath);

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
            Debug.Log($"[Meta] mode={( useSpellMode ? "SPELL" : "WORD")} classes={meta.ClassCount}, centroidDim={meta.centroidDim}, thr={meta.distanceThreshold}");

            if (engine != null)
                engine.SetMeta(meta);

            // Load embedding runner now that centroidDim is known
            if (useOodGate)
            {
                var embPath = useSpellMode ? spellEmbModelRelPath : embModelRelPath;
                if (!string.IsNullOrEmpty(embPath))
                {
                    int embFeat = useSpellMode ? 63 : 141;
                    int embOut = meta.centroidDim;
                    embRunner.LoadFromStreamingAssets(embPath, threads, embFeat, embOut);
                }
            }
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
        if (useTestMode)
        {
            UpdateTestMode();
            return;
        }

        if (landmarkSource == null || clock == null || engine == null) return;

        // 모드에 따라 feature 추출 및 inference 수행
        if (useSpellMode)
        {
            UpdateSpellMode();
        }
        else
        {
            UpdateWordMode();
        }
    }

    private void UpdateTestMode()
    {
        // 테스트 모드: 매 프레임 더미 데이터 축적 → 3초마다 판정
        if (useSpellMode)
        {
            UpdateTestModeSpell();
        }
        else
        {
            UpdateTestModeWord();
        }
    }

    private void UpdateTestModeSpell()
    {
        // 지화 테스트: 3초마다 판정 (engine 사용 안 함)
        // 1) 더미 오른손 데이터 생성 & 정규화
        Vector3[] dummyRightHand = new Vector3[21];
        for (int i = 0; i < 21; i++)
        {
            dummyRightHand[i] = new Vector3(
                UnityEngine.Random.Range(-0.1f, 0.1f),
                UnityEngine.Random.Range(-0.1f, 0.1f),
                UnityEngine.Random.Range(-0.05f, 0.05f)
            );
        }

        var feat = RightHandNormalizer.NormalizeLandmarks(dummyRightHand);
        if (feat == null || feat.Length != 63) return;

        // 2) 슬라이딩 윈도우에 push
        seq.Push(feat);

        // 3) 3초 타이머
        decisionTimer += Time.deltaTime;
        if (decisionTimer < decisionIntervalSec) return;
        decisionTimer = 0f;

        // 4) 최소 버퍼 확인
        if (seq.Count < (TFLiteGenericRunner.SeqLen / 2))
        {
            Debug.Log($"[TEST-SPELL] 버퍼 부족: {seq.Count}/{TFLiteGenericRunner.SeqLen}");
            return;
        }

        // 5) 추론
        var logits = clsRunner.Run(seq.Snapshot());
        if (logits == null || logits.Length == 0)
        {
            Debug.LogError("[TEST-SPELL] 모델 추론 실패");
            return;
        }

        int bestIdx = 0;
        float bestLogit = logits[0];
        for (int i = 1; i < logits.Length; i++)
        {
            if (logits[i] > bestLogit) { bestLogit = logits[i]; bestIdx = i; }
        }

        float bestProb = SoftmaxMaxProb(logits, bestIdx);

        Debug.Log($"[TEST-SPELL-Raw] idx={bestIdx} prob={bestProb:F3}");

        // 6) 확률 필터
        if (bestProb < MinProb)
        {
            Debug.Log($"[TEST-SPELL-DROP] LOW_PROB {bestProb:F3} < {MinProb:F3}");
            seq.Clear();
            return;
        }

        // 7) OOD 게이트 (optional)
        float dist = 0f;
        if (useOodGate && metaReady && embRunner != null && embRunner.IsReady)
        {
            var emb = embRunner.Run(seq.Snapshot());
            if (emb != null && emb.Length == meta.centroidDim)
            {
                dist = L2DistanceToCentroid(emb, bestIdx, meta);
                float thr = (overrideDistanceThreshold > 0f) ? overrideDistanceThreshold : meta.distanceThreshold;
                if (dist > thr)
                {
                    Debug.Log($"[TEST-SPELL-DROP] OOD dist={dist:F3} > thr={thr:F3}");
                    seq.Clear();
                    return;
                }
            }
        }

        // 8) 판정 완료
        string label = (metaReady && bestIdx >= 0 && bestIdx < meta.ClassCount && meta.classNames != null)
            ? meta.classNames[bestIdx]
            : $"#{bestIdx}";

        Debug.Log($"[TEST-SPELL-EMIT] PASS 판정 완료: idx={bestIdx} label='{label}' prob={bestProb:F2} dist={dist:F2}");
        seq.Clear();
    }

    private void UpdateTestModeWord()
    {
        // 단어 테스트: 버퍼 가득 차면 매 프레임 추론 (기존 로직 유지)
        float[] dummyFeat = new float[141];
        for (int i = 0; i < 141; i++)
        {
            dummyFeat[i] = UnityEngine.Random.Range(-0.1f, 0.1f);
        }

        seq.Push(dummyFeat);
        if (!seq.IsFull) return;

        var logits = clsRunner.Run(seq.Snapshot());
        if (logits == null || logits.Length == 0) return;

        int bestIdx = 0;
        float bestLogit = logits[0];
        for (int i = 1; i < logits.Length; i++)
        {
            if (logits[i] > bestLogit) { bestLogit = logits[i]; bestIdx = i; }
        }

        float bestProb = SoftmaxMaxProb(logits, bestIdx);

        string label = (metaReady && bestIdx >= 0 && bestIdx < meta.ClassCount && meta.classNames != null)
            ? meta.classNames[bestIdx]
            : $"#{bestIdx}";

        Debug.Log($"[TEST-WORD] idx={bestIdx} label='{label}' prob={bestProb:F3}");
    }

    private ILandmarkSource GetSrc()
    {
        return (landmarkSourceBehaviour as ILandmarkSource) ?? landmarkSource;
    }

    private bool EnsureWordReady(out ILandmarkSource src, out MediaPipeLandmarkSource mp)
    {
        src = GetSrc();
        mp = src as MediaPipeLandmarkSource;
        if (src == null) return false;

        // clsRunner만 보장하면 됨 (embRunner는 useOodGate일 때만)
        if (clsRunner == null || !clsRunner.IsReady)
            TryInitClsRunnerForCurrentMode();

        if (clsRunner == null || !clsRunner.IsReady) return false;

        // seqWord는 Awake에서 이미 만들고 있으니 사실상 필요 없음
        if (seqWord == null)
            seqWord = new SequenceBuffer(TFLiteGenericRunner.SeqLen, 141);

        if (voteRing == null || voteRing.Length == 0)
            voteRing = new int[Mathf.Max(1, votingWindow)];

        return true;
    }

    private void UpdateWordMode()
    {
        if (!EnsureWordReady(out var src, out var mp)) return;

        // ✅ feature가 유효할 때만 push (손/얼굴 판단은 GetFeature141() 결과로)
        var feat = src.GetFeature141();

        if (feat == null || feat.Length != 141)
        {
            wordMissingFrames++;

            // 너무 오래 비면 리셋 (한두 프레임 튐은 허용)
            if (wordMissingFrames >= wordResetAfterMissingFrames)
            {
                ResetWordBuffers();
                wordMissingFrames = 0;
            }

            if (debugLogs && Time.unscaledTime >= _nextDropLogTime)
            {
                _nextDropLogTime = Time.unscaledTime + debugDropEverySec;
                bool faceNow = (mp != null) ? mp.HasFaceNow : src.HasFace;
                bool anyHandNow = (mp != null) ? (mp.HasLeftHand || mp.HasRightHand) : src.HasAnyHand;
                Debug.Log($"[Drop-WORD] NO_FEAT141 missFrames={wordMissingFrames} faceNow={faceNow} anyHandNow={anyHandNow}");
            }
            return;
        }

        wordMissingFrames = 0;

        seqWord.Push(feat);
        if (!seqWord.IsFull) return;

        var logits = clsRunner.Run(seqWord.Snapshot());
        if (logits == null || logits.Length == 0) return;

        int bestIdx = 0;
        float bestLogit = logits[0];
        for (int i = 1; i < logits.Length; i++)
            if (logits[i] > bestLogit) { bestLogit = logits[i]; bestIdx = i; }

        float bestProb = SoftmaxMaxProb(logits, bestIdx);
        if (bestProb < MinProb)
        {
            if (debugLogs && Time.unscaledTime >= _nextDropLogTime)
            {
                _nextDropLogTime = Time.unscaledTime + debugDropEverySec;
                Debug.Log($"[Drop-WORD] LOW_PROB prob={bestProb:F3} < {MinProb:F3} bestIdx={bestIdx}");
            }
            return;
        }

        ProcessPrediction(bestIdx, bestProb, bestLogit);
    }

    private void ResetSpellBuffers()
    {
        spellLocked = false;
        spellLockedLabel = -1;
        decisionTimer = 0f;
        seqSpell.Clear();

        for (int i = 0; i < voteRing.Length; i++) voteRing[i] = -1;
        votePos = 0;
        voteCount = 0;
    }

    private void UpdateSpellMode()
    {
        // 0) 단일 진실(src)
        ILandmarkSource src =
            (landmarkSourceBehaviour as ILandmarkSource)
            ?? landmarkSource;

        if (src == null) return;

        var mp = src as MediaPipeLandmarkSource; // optional debug

        // 1) 오른손 랜드마크 가져오기 (오른손만 강제)
        var rightHand = src.GetRightHandLandmarks();

        if (rightHand == null || rightHand.Length != 21)
        {
            ResetSpellBuffers();

            if (debugLogs && Time.unscaledTime >= _nextDropLogTime)
            {
                _nextDropLogTime = Time.unscaledTime + debugDropEverySec;

                if (mp != null)
                    Debug.Log($"[SpellDrop] NO_RIGHT_HAND (LH={mp.HasLeftHand}, RH={mp.HasRightHand})");
                else
                    Debug.Log("[SpellDrop] NO_RIGHT_HAND");
            }
            return;
        }

        // 2) 정규화 → 63 feature
        var feat = RightHandNormalizer.NormalizeLandmarks(rightHand);
        if (feat == null || feat.Length != 63) return;

        // (옵션) 입력이 거의 0이면 스킵 (실제로 랜드마크가 안 들어오는 상황 방지)
        if (debugLogs && Time.unscaledTime >= _nextDropLogTime)
        {
            float absMean = 0f;
            for (int i = 0; i < feat.Length; i++) absMean += Mathf.Abs(feat[i]);
            absMean /= feat.Length;

            if (absMean < 1e-5f)
            {
                _nextDropLogTime = Time.unscaledTime + debugDropEverySec;
                Debug.Log($"[SpellDrop] FEAT_ZERO absMean={absMean:E3}");
                return;
            }
        }

        // 3) 버퍼 push
        seqSpell.Push(feat);

        // 4-A) 리듬게임용 실시간 모드
        if (spellRealtimeForRhythm)
        {
            int need = Mathf.Max(1, Mathf.FloorToInt(seqSpell.SeqLen * spellMinBufferRatio));
            if (seqSpell.Count < need) return;

            var logits = clsRunner.Run(seqSpell.Snapshot());
            if (logits == null || logits.Length == 0) return;

            int bestIdx = 0;
            float bestLogit = logits[0];
            for (int i = 1; i < logits.Length; i++)
                if (logits[i] > bestLogit) { bestLogit = logits[i]; bestIdx = i; }

            float bestProb = SoftmaxMaxProb(logits, bestIdx);

            if (debugLogs && Time.unscaledTime >= _nextPredLogTime)
            {
                _nextPredLogTime = Time.unscaledTime + debugPredEverySec;
                float nowT = clock != null ? clock.NowSec() : Time.time;
                Debug.Log($"[PredRaw-SPELL-RT] t={nowT:F2} idx={bestIdx} prob={bestProb:F3}");
            }

            if (bestProb < MinProb)
            {
                if (debugLogs && Time.unscaledTime >= _nextDropLogTime)
                {
                    _nextDropLogTime = Time.unscaledTime + debugDropEverySec;
                    Debug.Log($"[Drop-SPELL-RT] LOW_PROB prob={bestProb:F3} < {MinProb:F3} bestIdx={bestIdx}");
                }
                return;
            }

            // WORD와 동일 파이프라인
            ProcessPrediction(bestIdx, bestProb, bestLogit);
            return;
        }

        // 4-B) 기존 3초 단발 + lock 모드 유지 (필요 시)
        if (spellLocked) return;

        decisionTimer += Time.deltaTime;
        if (decisionTimer < decisionIntervalSec) return;
        decisionTimer = 0f;

        if (seqSpell.Count < (seqSpell.SeqLen / 2))
        {
            if (debugLogs) Debug.Log($"[Spell] 버퍼 부족: {seqSpell.Count}/{seqSpell.SeqLen}");
            return;
        }

        var logitsOnce = clsRunner.Run(seqSpell.Snapshot());
        if (logitsOnce == null || logitsOnce.Length == 0) return;

        int bestIdxOnce = 0;
        float bestLogitOnce = logitsOnce[0];
        for (int i = 1; i < logitsOnce.Length; i++)
            if (logitsOnce[i] > bestLogitOnce) { bestLogitOnce = logitsOnce[i]; bestIdxOnce = i; }

        float bestProbOnce = SoftmaxMaxProb(logitsOnce, bestIdxOnce);
        if (debugLogs) Debug.Log($"[SpellRaw] bestIdx={bestIdxOnce} prob={bestProbOnce:F3}");

        if (bestProbOnce < MinProb)
        {
            if (debugLogs) Debug.Log($"[SpellDrop] LOW_PROB {bestProbOnce:F3} < {MinProb:F3}");
            return;
        }

        // OOD/emit은 네 기존 로직 그대로 (ProcessPrediction으로 보내도 되고, 지금처럼 별도 emit도 OK)
        ProcessPrediction(bestIdxOnce, bestProbOnce, bestLogitOnce);

        spellLocked = true;
        spellLockedLabel = bestIdxOnce;
        seqSpell.Clear();
    }

    private void ProcessPrediction(int bestIdx, float bestProb, float bestLogit)
    {
        // OOD gating (embedding + centroid distance)
        float dist = 0f;
        bool oodEnabled = useOodGate && metaReady;
        bool locked = !oodEnabled;

        if (oodEnabled)
        {
            var embPath = useSpellMode ? spellEmbModelRelPath : embModelRelPath;
            if (string.IsNullOrEmpty(embPath))
            {
                oodEnabled = false;
            }
            else
            {
                if (oodEnabled && embRunner != null && embRunner.IsReady)
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
                                Debug.Log($"[Drop] OOD dist={dist:F2} > thr={thr:F2}");
                            }
                            return;
                        }

                        locked = true; // dist 통과했으면 "확신" 플래그로 써도 됨
                    }
                }
                else if (oodEnabled)
                {
                    oodEnabled = false;
                }
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
                $"[PredEmit-{(useSpellMode ? "SPELL" : "WORD")}] t={now:F2} idx={voted} label={label} " +
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