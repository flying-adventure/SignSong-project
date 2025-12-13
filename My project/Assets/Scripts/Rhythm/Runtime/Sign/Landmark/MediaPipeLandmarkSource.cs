using UnityEngine;
using Mediapipe; // NormalizedLandmarkList, NormalizedLandmark

public class MediaPipeLandmarkSource : MonoBehaviour, ILandmarkSource
{
    public const int NumPoints = 47;
    public const int FeatDim = NumPoints * 3;

    [Header("Optional")]
    [Tooltip("카메라 미러링이면 X 뒤집기 (정규화된 feature 기준)")]
    public bool flipX = false;

    [Header("Spell (63dim)")]
    [Tooltip("지화(오른손 63dim) 입력에서 X를 wrist 기준으로 미러링(좌우반전) 적용")]
    public bool spellMirrorX = false;

    [Tooltip("오른손이 없으면 왼손을 대신 사용")]
    public bool spellFallbackToLeft = true;

    // 디버그/외부 확인용: 현재 spell 입력이 왼손 fallback인지
    public bool SpellUsingLeftHand { get; private set; } = false;

    public enum Feature141Mode
    {
        StrictFaceRequired,
        HandFallback
    }

    [Header("Feature Mode")]
    public Feature141Mode feature141Mode = Feature141Mode.HandFallback;

    [Header("Debug")]
    public bool debugLog = false;
    public int logEveryNFrames = 30;

    // MediaPipe raw results
    private NormalizedLandmarkList _pose;
    private NormalizedLandmarkList _leftHand;
    private NormalizedLandmarkList _rightHand;
    private NormalizedLandmarkList _face;

    private bool _hasAnyHand;
    private bool _hasFace;

    public bool HasAnyHand => _hasAnyHand;
    public bool HasBothHands => HasLeftHand && HasRightHand;

    public bool HasRightHand =>
        (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count >= 21);

    public bool HasLeftHand =>
        (_leftHand != null && _leftHand.Landmark != null && _leftHand.Landmark.Count >= 21);

    public bool HasFaceNow =>
        (_face != null && _face.Landmark != null && _face.Landmark.Count > 0);

    public bool HasFace => _hasFace;

    private static readonly int[] FaceIndices = { 1, 33, 263, 61, 291 };
    private Vector3[] _lastFace5;
    private bool _lastFace5Valid;

    public void SetFromMediaPipe(
        NormalizedLandmarkList pose,
        NormalizedLandmarkList leftHand,
        NormalizedLandmarkList rightHand,
        NormalizedLandmarkList face)
    {
        if (pose != null) _pose = pose;
        if (leftHand != null) _leftHand = leftHand;
        if (rightHand != null) _rightHand = rightHand;

        if (face != null)
        {
            _face = face;

            if (_face.Landmark != null && _face.Landmark.Count > 0)
            {
                if (_lastFace5 == null || _lastFace5.Length != FaceIndices.Length)
                    _lastFace5 = new Vector3[FaceIndices.Length];

                bool ok = true;
                for (int i = 0; i < FaceIndices.Length; i++)
                {
                    int idx = FaceIndices[i];
                    if (idx < 0 || idx >= _face.Landmark.Count)
                    {
                        _lastFace5[i] = Vector3.zero;
                        ok = false;
                        continue;
                    }

                    var lm = _face.Landmark[idx];
                    _lastFace5[i] = new Vector3(lm.X, lm.Y, lm.Z);
                }
                _lastFace5Valid = ok;
            }
            else
            {
                _lastFace5Valid = false;
            }
        }

        _hasAnyHand =
            (_leftHand != null && _leftHand.Landmark != null && _leftHand.Landmark.Count >= 21) ||
            (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count >= 21);

        _hasFace =
            (feature141Mode == Feature141Mode.StrictFaceRequired)
            ? (_face != null && _face.Landmark != null && _face.Landmark.Count > 0)   // 캐시 사용 X
            : ((_face != null && _face.Landmark != null && _face.Landmark.Count > 0) ||
            (_lastFace5 != null && _lastFace5Valid));

        if (debugLog && (Time.frameCount % Mathf.Max(1, logEveryNFrames) == 0))
        {
            int lh = (_leftHand != null && _leftHand.Landmark != null) ? _leftHand.Landmark.Count : 0;
            int rh = (_rightHand != null && _rightHand.Landmark != null) ? _rightHand.Landmark.Count : 0;
            Debug.Log($"[SetFromMediaPipe] LH={lh} RH={rh} hasAnyHand={_hasAnyHand} hasFace={_hasFace}");
        }
    }

    public bool TryGet(out Vector3[] a, out Vector3[] b, out Vector3[] c)
    {
        a = ToVec3Array(_pose);
        b = ToVec3Array(_leftHand);
        c = ToVec3Array(_rightHand);
        return (a != null) || (b != null) || (c != null);
    }

    private static Vector3[] ToVec3Array(NormalizedLandmarkList list)
    {
        if (list == null || list.Landmark == null) return null;
        int n = list.Landmark.Count;
        if (n <= 0) return null;

        var arr = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var lm = list.Landmark[i];
            arr[i] = new Vector3(lm.X, lm.Y, lm.Z);
        }
        return arr;
    }

    private int _frameCount = 0;
    public float[] GetFeature141()
    {
        _frameCount++;

        // ===== (1) 얼굴 사용 가능 여부 판단 =====
        bool faceNow = (_face != null && _face.Landmark != null && _face.Landmark.Count > 0);
        bool faceCached = (_lastFace5 != null && _lastFace5Valid);
        bool faceAvailable = (feature141Mode == Feature141Mode.StrictFaceRequired) ? faceNow : (faceNow || faceCached);

        // StrictFaceRequired인데 얼굴이 "현재 프레임"에서 없으면 -> feature를 안 줘서 WORD 추론 자체를 스킵시키는 게 안전
        if (!faceAvailable && feature141Mode == Feature141Mode.StrictFaceRequired)
            return null;

        // 손도 얼굴도 아예 없으면 feature 만들 의미가 없음
        if (!faceAvailable && !_hasAnyHand)
            return null;

        // ===== (2) anchor/scale 결정 =====
        Vector3 anchor;
        float scale;

        if (faceAvailable)
        {
            var face5 = ExtractFace5(faceNow);
            anchor = face5[0];                 // nose(1)
            Vector3 eyeL = face5[1];           // left eye(33)
            Vector3 eyeR = face5[2];           // right eye(263)
            scale = (eyeL - eyeR).magnitude;   // 눈 간 거리로 스케일
        }
        else
        {
            // HandFallback: 손 기반 정규화
            Vector3 wrist = GetWristOrZero();
            Vector3 mcp = GetPointFromAnyHand(9);      // middle_mcp
            anchor = wrist;
            scale = (mcp - wrist).magnitude;
        }

        if (scale < 1e-6f) scale = 1e-6f;

        Vector3 Norm(Vector3 v)
        {
            var outv = (v - anchor) / scale;
            if (flipX) outv.x = -outv.x;
            return outv;
        }

        // ===== (3) 손 21+21 채우기 (없으면 0) =====
        var leftArr = new Vector3[21];
        var rightArr = new Vector3[21];
        FillHand(_leftHand, leftArr, Norm);
        FillHand(_rightHand, rightArr, Norm);

        // ===== (4) 얼굴 5개 채우기 (없으면 0) =====
        var faceArr = new Vector3[5];
        if (faceAvailable)
        {
            var face5 = ExtractFace5(faceNow);
            for (int i = 0; i < 5; i++) faceArr[i] = Norm(face5[i]);
        }
        else
        {
            for (int i = 0; i < 5; i++) faceArr[i] = Vector3.zero;
        }

        // ===== (5) flatten 141 =====
        var feat = new float[141];
        int k = 0;

        for (int i = 0; i < 21; i++) { feat[k++] = leftArr[i].x;  feat[k++] = leftArr[i].y;  feat[k++] = leftArr[i].z; }
        for (int i = 0; i < 21; i++) { feat[k++] = rightArr[i].x; feat[k++] = rightArr[i].y; feat[k++] = rightArr[i].z; }
        for (int i = 0; i < 5;  i++) { feat[k++] = faceArr[i].x;  feat[k++] = faceArr[i].y;  feat[k++] = faceArr[i].z; }

        if (debugLog && (_frameCount % Mathf.Max(1, logEveryNFrames) == 0))
        {
            Debug.Log($"[GetFeat141] faceAvail={faceAvailable} faceNow={faceNow} anyHand={_hasAnyHand} bothHands={HasBothHands} " +
                    $"LH={(HasLeftHand ? 21 : 0)} RH={(HasRightHand ? 21 : 0)}");
        }

        return feat;
    }

    // ================= Helpers =================

    // faceNow=true면 _face에서 뽑고, 아니면 cache(_lastFace5)에서 뽑는다.
    private Vector3[] ExtractFace5(bool faceNow)
    {
        var out5 = new Vector3[5];

        if (faceNow && _face != null && _face.Landmark != null && _face.Landmark.Count > 0)
        {
            // _face에서 FaceIndices 위치를 뽑음
            for (int i = 0; i < FaceIndices.Length; i++)
            {
                int idx = FaceIndices[i];
                if (idx >= 0 && idx < _face.Landmark.Count)
                {
                    var lm = _face.Landmark[idx];
                    out5[i] = new Vector3(lm.X, lm.Y, lm.Z);
                }
                else out5[i] = Vector3.zero;
            }
            return out5;
        }

        // cache fallback
        if (_lastFace5 != null && _lastFace5Valid)
        {
            for (int i = 0; i < out5.Length; i++) out5[i] = _lastFace5[i];
            return out5;
        }

        for (int i = 0; i < out5.Length; i++) out5[i] = Vector3.zero;
        return out5;
    }

    private Vector3 GetWristOrZero()
    {
        // 우선 RH wrist(0) -> 없으면 LH wrist(0)
        if (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count > 0)
        {
            var lm = _rightHand.Landmark[0];
            return new Vector3(lm.X, lm.Y, lm.Z);
        }
        if (_leftHand != null && _leftHand.Landmark != null && _leftHand.Landmark.Count > 0)
        {
            var lm = _leftHand.Landmark[0];
            return new Vector3(lm.X, lm.Y, lm.Z);
        }
        return Vector3.zero;
    }

    private Vector3 GetPointFromAnyHand(int idx)
    {
        if (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count > idx)
        {
            var lm = _rightHand.Landmark[idx];
            return new Vector3(lm.X, lm.Y, lm.Z);
        }
        if (_leftHand != null && _leftHand.Landmark != null && _leftHand.Landmark.Count > idx)
        {
            var lm = _leftHand.Landmark[idx];
            return new Vector3(lm.X, lm.Y, lm.Z);
        }
        return Vector3.zero;
    }

    private static void FillHand(NormalizedLandmarkList src, Vector3[] dst, System.Func<Vector3, Vector3> norm)
    {
        // dst는 길이 21 가정
        if (dst == null || dst.Length < 21) return;

        if (src == null || src.Landmark == null || src.Landmark.Count < 21)
        {
            for (int i = 0; i < 21; i++) dst[i] = Vector3.zero;
            return;
        }

        for (int i = 0; i < 21; i++)
        {
            var lm = src.Landmark[i];
            dst[i] = norm(new Vector3(lm.X, lm.Y, lm.Z));
        }
    }

    // ====== 핵심: Spell(63dim)용 RightHand landmarks ======
    public Vector3[] GetRightHandLandmarks()
    {
        SpellUsingLeftHand = false;

        bool hasRH = (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count >= 21);
        if (!hasRH) return null;  // 오른손만 허용

        var arr = new Vector3[21];
        for (int i = 0; i < 21; i++)
        {
            var lm = _rightHand.Landmark[i];
            arr[i] = new Vector3(lm.X, lm.Y, lm.Z);
        }
        return arr;
    }

    private static void MirrorXAboutWrist(Vector3[] pts)
    {
        if (pts == null || pts.Length < 1) return;
        float wx = pts[0].x; // wrist x
        for (int i = 0; i < pts.Length; i++)
        {
            pts[i].x = 2f * wx - pts[i].x;
        }
    }
}