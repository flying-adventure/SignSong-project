using UnityEngine;
using Mediapipe; // NormalizedLandmarkList, NormalizedLandmark

public class MediaPipeLandmarkSource : MonoBehaviour, ILandmarkSource
{
    public const int NumPoints = 47;
    public const int FeatDim = NumPoints * 3;

    [Header("Optional")]
    [Tooltip("카메라 미러링이면 X 뒤집기 (정규화된 feature 기준)")]
    public bool flipX = false;

    public enum Feature141Mode
    {
        StrictFaceRequired, // face 없으면 141 전부 0 (파이썬과 동일)
        HandFallback        // face 없으면 손 기준으로 정규화 + face는 0
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
    public bool HasFace => _hasFace;

    // face 5pt indices (nose, left_eye, right_eye, mouth_l, mouth_r)
    private static readonly int[] FaceIndices = { 1, 33, 263, 61, 291 };
    private Vector3[] _lastFace5;
    private bool _lastFace5Valid;

    /// <summary>
    /// Runners/Feeder에서 들어오는 진입점
    /// </summary>
    public void SetFromMediaPipe(
        NormalizedLandmarkList pose,
        NormalizedLandmarkList leftHand,
        NormalizedLandmarkList rightHand,
        NormalizedLandmarkList face)
    {
        // null이면 유지 / non-null이면 갱신(빈 리스트도 “갱신”으로 취급해서 clear 가능)
        if (pose != null) _pose = pose;
        if (leftHand != null) _leftHand = leftHand;
        if (rightHand != null) _rightHand = rightHand;

        if (face != null)
        {
            _face = face;

            // 캐싱
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
            (_face != null && _face.Landmark != null && _face.Landmark.Count > 0) ||
            (_lastFace5 != null && _lastFace5Valid);

        if (debugLog && (Time.frameCount % Mathf.Max(1, logEveryNFrames) == 0))
        {
            Debug.Log($"[SetFromMediaPipe] hasAnyHand={_hasAnyHand} hasFace={_hasFace} faceNow={(face != null)} lastFace5Valid={_lastFace5Valid}");
        }
    }

    // ILandmarkSource 요구 멤버 (CS0535 해결 포인트)
    public bool TryGet(out Vector3[] a, out Vector3[] b, out Vector3[] c)
    {
        // 관례: (pose, leftHand, rightHand)
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

    public float[] GetFeature141()
    {
        bool faceAvailable =
            (_face != null && _face.Landmark != null && _face.Landmark.Count > 0) ||
            (_lastFace5 != null && _lastFace5Valid);

        if (!faceAvailable && feature141Mode == Feature141Mode.StrictFaceRequired)
        {
            // 파이썬과 동일: face 없으면 141 전부 0
            return new float[141];
        }

        // 1) 정규화 기준(앵커/스케일) 결정
        Vector3 anchor;
        float scale;

        if (faceAvailable)
        {
            Vector3[] face5 = ExtractFace5();
            anchor = face5[0]; // nose

            Vector3 eyeL = face5[1];
            Vector3 eyeR = face5[2];
            scale = (eyeL - eyeR).magnitude;
            if (scale < 1e-6f) scale = 1e-6f;
        }
        else
        {
            // HandFallback: 손 기준
            Vector3 wrist = GetWristOrZero();
            Vector3 mcp = GetPointFromAnyHand(9); // middle_mcp
            anchor = wrist;

            scale = (mcp - wrist).magnitude;
            if (scale < 1e-6f) scale = 1e-6f;
        }

        Vector3 Norm(Vector3 v)
        {
            var outv = (v - anchor) / scale;
            if (flipX) outv.x = -outv.x;
            return outv;
        }

        // 2) hands
        var leftArr = new Vector3[21];
        var rightArr = new Vector3[21];
        FillHand(_leftHand, leftArr, Norm);
        FillHand(_rightHand, rightArr, Norm);

        // 3) face 5pt (없으면 0)
        var faceArr = new Vector3[5];
        if (faceAvailable)
        {
            var face5 = ExtractFace5();
            for (int i = 0; i < 5; i++) faceArr[i] = Norm(face5[i]);
        }
        else
        {
            for (int i = 0; i < 5; i++) faceArr[i] = Vector3.zero;
        }

        // 4) flatten
        var feat = new float[141];
        int k = 0;

        for (int i = 0; i < 21; i++) { feat[k++] = leftArr[i].x; feat[k++] = leftArr[i].y; feat[k++] = leftArr[i].z; }
        for (int i = 0; i < 21; i++) { feat[k++] = rightArr[i].x; feat[k++] = rightArr[i].y; feat[k++] = rightArr[i].z; }
        for (int i = 0; i < 5; i++)  { feat[k++] = faceArr[i].x; feat[k++] = faceArr[i].y; feat[k++] = faceArr[i].z; }

        if (debugLog && (Time.frameCount % Mathf.Max(1, logEveryNFrames) == 0))
        {
            float absMean = 0f;
            for (int i = 0; i < feat.Length; i++) absMean += Mathf.Abs(feat[i]);
            absMean /= feat.Length;
            Debug.Log($"[Feat141] mode={feature141Mode} faceAvail={faceAvailable} hasAnyHand={_hasAnyHand} absMean={absMean:F4}");
        }

        return feat;
    }

    private Vector3[] ExtractFace5()
    {
        var face = new Vector3[FaceIndices.Length];

        if (_face != null && _face.Landmark != null && _face.Landmark.Count > 0)
        {
            for (int i = 0; i < FaceIndices.Length; i++)
            {
                int idx = FaceIndices[i];
                if (idx < 0 || idx >= _face.Landmark.Count) { face[i] = Vector3.zero; continue; }
                var lm = _face.Landmark[idx];
                face[i] = new Vector3(lm.X, lm.Y, lm.Z);
            }
        }
        else
        {
            for (int i = 0; i < FaceIndices.Length; i++) face[i] = _lastFace5[i];
        }

        return face;
    }

    private static void FillHand(NormalizedLandmarkList hand, Vector3[] dst, System.Func<Vector3, Vector3> norm)
    {
        if (hand != null && hand.Landmark != null && hand.Landmark.Count >= 21)
        {
            for (int i = 0; i < 21; i++)
            {
                var lm = hand.Landmark[i];
                dst[i] = norm(new Vector3(lm.X, lm.Y, lm.Z));
            }
        }
        else
        {
            for (int i = 0; i < 21; i++) dst[i] = Vector3.zero;
        }
    }

    private Vector3 GetWristOrZero()
    {
        // right wrist 우선
        if (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count >= 1)
        {
            var lm = _rightHand.Landmark[0];
            return new Vector3(lm.X, lm.Y, lm.Z);
        }
        if (_leftHand != null && _leftHand.Landmark != null && _leftHand.Landmark.Count >= 1)
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

    public Vector3[] GetRightHandLandmarks()
    {
        var rightArr = new Vector3[21];

        if (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count >= 21)
        {
            for (int i = 0; i < 21; i++)
            {
                var lm = _rightHand.Landmark[i];
                rightArr[i] = new Vector3(lm.X, lm.Y, lm.Z);

                // RightHandNormalizer가 "원본(0~1) 좌표"를 기대한다면, 좌우반전은 보통 1-x
                if (flipX) rightArr[i].x = 1f - rightArr[i].x;
            }
        }
        else
        {
            for (int i = 0; i < 21; i++) rightArr[i] = Vector3.zero;
        }

        return rightArr;
    }
}