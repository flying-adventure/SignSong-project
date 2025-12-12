using System;
using System.Collections.Generic;
using UnityEngine;
 
public class JudgementEngine : MonoBehaviour
{
    [SerializeField] private JudgementConfig config;
    [SerializeField] private List<Note> notes = new(); // timeSec 오름차순 정렬
    [Header("Timing")]
    [SerializeField] private float chartOffsetSec = 0f;     // CSV(차트) 기준 보정(오디오 앞 무음 등)
    [SerializeField] private float modelLatencySec = 0f;    // 모델/추론 파이프라인 지연
    [SerializeField] private int expectedIdxOffset = 0;

    public enum JudgeType
    {
        Miss = 0,
        Good = 1,
        Perfect = 2,
    }
    
    public event Action<JudgeEvent> OnJudged;
    // public System.Action OnComboReset;

    private int _currentIndex;
    private PredictionRingBuffer _predBuf;
    private readonly List<Prediction> _candidates = new();

    private int _lastIdx = -1;
    private float _lastIdxTime = -999f;

    [Header("Debug meta (optional)")]
    public SignMeta debugMeta; 

    [Header("Debug")]
    public bool debugLogs = true;
    private readonly HashSet<int> _loggedMapNoteIds = new(); // noteId별 1회만

    private void Awake()
    {
        _predBuf = new PredictionRingBuffer(2.0f);
        _currentIndex = 0;
    }

    public void SetNotes(List<Note> newNotes)
    {
        notes = newNotes;
        notes.Sort((a, b) => a.timeSec.CompareTo(b.timeSec));
        _currentIndex = 0;
    }

    public void SetMeta(SignMeta meta)
    {
        debugMeta = meta;
    }

    private string GetLabel(int idx)
    {
        if (debugMeta == null || debugMeta.classNames == null) return $"#{idx}";
        if (idx < 0 || idx >= debugMeta.ClassCount) return $"#{idx}";
        return debugMeta.classNames[idx];
    }

    public void PushPrediction(Prediction p) => _predBuf.Add(p);

    public void UpdateEngine(float nowSec)
    {
        if (config == null || notes == null || _currentIndex >= notes.Count) return;

        _predBuf.Prune(nowSec);

        while (_currentIndex < notes.Count)
        {
            var note = notes[_currentIndex];
            if (note.judged) { _currentIndex++; continue; }

            float t0Raw = note.timeSec;
            float t0 = t0Raw + chartOffsetSec + modelLatencySec;
            
            // 너무 이르면 멈춤
            if (nowSec < t0 - config.goodWindow) break;

            // good window 범위 넘어 동작 -> Miss 판정
            if (nowSec > t0 + config.goodWindow)
            {
                if (debugLogs)
                    Debug.Log($"[MISS] noteId={note.noteId} expectedIdx={note.expectedIdx} t0={t0Raw:F2}(+{modelLatencySec:F2}=>{t0:F2}) now={nowSec:F2}");

                Emit(note, nowSec, -1, nowSec, 0f, 999f, nowSec - t0, JudgeResult.Miss);
                note.judged = true;
                _currentIndex++;
                continue;
            }

            // 윈도우 범위 내 후보 모으기
            _predBuf.GetBetween(t0 - config.goodWindow, t0 + config.goodWindow, _candidates);

            if (!TryPickBest(note, t0, out var best))
            {
                // 아직 확정 X -> 다음 프레임에 재평가
                break;
            }

            float dtAbs = Mathf.Abs(best.timeSec - t0);
            var result = (dtAbs <= config.perfectWindow) ? JudgeResult.Perfect : JudgeResult.Good;

            Emit(note, nowSec, best.idx, best.timeSec, best.prob, best.dist, best.timeSec - t0, result);
            note.judged = true;
            _currentIndex++;
        }
    }

    private bool TryPickBest(Note note, float t0, out Prediction best)
    {
        best = default;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < _candidates.Count; i++)
        {
            var p = _candidates[i];

            // 같은 idx 중복 방지
            if (p.idx == _lastIdx && (p.timeSec - _lastIdxTime) < config.sameSignCooldown)
                continue;

            int expected = note.expectedIdx + expectedIdxOffset;

            // 정답 idx 일치하는 경우에만 판정하도록
            if (p.idx != expected)
            {
                if (debugLogs && !_loggedMapNoteIds.Contains(note.noteId))
                {
                    _loggedMapNoteIds.Add(note.noteId);

                    string expectedLabel = GetLabel(note.expectedIdx);
                    string candLabel     = GetLabel(p.idx);
                    float mapDt          = Mathf.Abs(p.timeSec - t0);

                    Debug.Log(
                        $"[MAP] noteId={note.noteId} t0={t0:F2} " +
                        $"expected={note.expectedIdx}({expectedLabel}) " +
                        $"cand={p.idx}({candLabel}) prob={p.prob:F2} dt={mapDt:F3}"
                    );
                }
                continue;
            }

            // 최소 threshold
            if (p.prob < config.minProb) continue;
            if (p.dist > config.maxDist) continue;

            // 점수 구성 요소 계산(먼저 선언!)
            float dt = Mathf.Abs(p.timeSec - t0);
            float timeScore = Mathf.Exp(-dt / 0.08f);
            float distScore = 1f / (1f + p.dist);

            // locked 완화: locked면 가점, 아니면 감점
            float lockBonus = p.locked ? 0.15f : -0.15f;

            // score는 딱 한 번만 선언
            float score = lockBonus + 0.55f * p.prob + 0.25f * timeScore + 0.20f * distScore;

            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        if (bestScore == float.NegativeInfinity) return false;

        _lastIdx = best.idx;
        _lastIdxTime = best.timeSec;
        return true;
    }

    private void Emit(Note note, float nowSec, int predictedIdx, float hitTime,
                      float prob, float dist, float dt, JudgeResult result)
    {
        OnJudged?.Invoke(new JudgeEvent
        {
            noteId = note.noteId,
            noteTime = note.timeSec,
            hitTime = hitTime,
            expectedIdx = note.expectedIdx,
            predictedIdx = predictedIdx,
            dt = dt,
            prob = prob,
            dist = dist,
            result = result
        });
    }
}