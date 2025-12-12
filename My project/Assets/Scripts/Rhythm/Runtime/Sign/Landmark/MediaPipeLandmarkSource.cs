using UnityEngine;
using Mediapipe; // NormalizedLandmarkList, NormalizedLandmark

public class MediaPipeLandmarkSource : MonoBehaviour, ILandmarkSource
{
    // 47 points * 3 = 141
    public const int NumPoints = 47;
    public const int FeatDim = NumPoints * 3;

    [Header("Optional (depends on your training setup)")]
    [Tooltip("21(LH) + 21(RH) + 5(POSE) = 47 구성으로 가정. 마지막 5개 pose index를 여기서 지정.")]
    public int[] pose5Indices = new int[] { 11, 12, 13, 14, 0 }; // (지금은 안 쓰지만 남겨둬도 무방)

    [Tooltip("카메라 미러링이면 X 뒤집기")]
    public bool flipX = false;

    // MediaPipe 원본 (Holistic / Hand / Face 결과)
    private NormalizedLandmarkList _pose;
    private NormalizedLandmarkList _leftHand;
    private NormalizedLandmarkList _rightHand;
    private NormalizedLandmarkList _face;

    private bool _hasAnyHand;
    private bool _hasFace;

    public bool HasAnyHand => _hasAnyHand;
    public bool HasFace => _hasFace;

    // 얼굴 5포인트 인덱스 (파이썬과 동일)
    private static readonly int[] FaceIndices = { 1, 33, 263, 61, 291 };
    private Vector3[] _lastFace5; 

    /// <summary>
    /// Holistic / Hand / Face 러너에서 호출하는 진입점
    /// </summary>
    public void SetFromMediaPipe(
        NormalizedLandmarkList pose,
        NormalizedLandmarkList leftHand,
        NormalizedLandmarkList rightHand,
        NormalizedLandmarkList face)
    {
        // null이면 기존 값 유지, non-null이면 갱신
        if (pose != null)
            _pose = pose;

        if (leftHand != null)
            _leftHand = leftHand;

        if (rightHand != null)
            _rightHand = rightHand;

        if (face != null)
        {
            _face = face;

            // 여기서 5포인트만 미리 캐싱해 두면, 다음 프레임 face가 없어도 사용 가능
            if (_face.Landmark != null && _face.Landmark.Count > 0)
            {
                if (_lastFace5 == null || _lastFace5.Length != FaceIndices.Length)
                    _lastFace5 = new Vector3[FaceIndices.Length];

                for (int i = 0; i < FaceIndices.Length; i++)
                {
                    int idx = FaceIndices[i];
                    if (idx < 0 || idx >= _face.Landmark.Count)
                    {
                        _lastFace5[i] = Vector3.zero;
                        continue;
                    }

                    var lm = _face.Landmark[idx];
                    _lastFace5[i] = new Vector3(lm.X, lm.Y, lm.Z);
                }
            }
        }

        _hasAnyHand =
            (_leftHand != null && _leftHand.Landmark != null && _leftHand.Landmark.Count >= 21) ||
            (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count >= 21);

        _hasFace =
            (_face != null && _face.Landmark != null && _face.Landmark.Count > 0) ||
            (_lastFace5 != null);

        Debug.Log($"[SetFromMediaPipe] hasFace={_hasFace} faceNow={(face!=null)} lastFace5={(_lastFace5!=null)}");
    }

    /// <summary>
    /// 옛 코드 호환용. 현재는 SignPredictionProvider가 GetFeature141만 쓰지만,
    /// 다른 곳에서 포즈/손 좌표를 보고 싶을 때를 위해 유지.
    /// </summary>
    public bool TryGet(out Vector3[] pose, out Vector3[] left, out Vector3[] right)
    {
        pose = ToVec3Array(_pose);
        left = ToVec3Array(_leftHand);
        right = ToVec3Array(_rightHand);

        return (pose != null) || (left != null) || (right != null);
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

    /// <summary>
    /// 파이썬 normalize_features(left_hand, right_hand, face)에 해당.
    /// face가 없으면 141 전부 0.
    /// </summary>
    public float[] GetFeature141()
    {
        Debug.Log($"[GetFeat141] hasFace={_hasFace} face={( _face!=null)} lastFace5={(_lastFace5!=null)}");
        // 0) face가 없으면 전체 0 → 파이썬과 동일한 동작
        if ((_face == null || _face.Landmark == null || _face.Landmark.Count == 0) 
            && _lastFace5 == null)
        {
            Debug.Log("[Feat141] face missing, return ZERO");
            return new float[141];
        }

        // 1) 얼굴 5포인트 추출 (nose, left_eye, right_eye, mouth_l, mouth_r)
        var face = new Vector3[FaceIndices.Length];
        if (_face != null && _face.Landmark != null && _face.Landmark.Count > 0)
        {
            // 현재 프레임 face 사용
            for (int i = 0; i < FaceIndices.Length; i++)
            {
                int idx = FaceIndices[i];
                if (idx < 0 || idx >= _face.Landmark.Count)
                {
                    face[i] = Vector3.zero;
                    continue;
                }

                var lm = _face.Landmark[idx];
                face[i] = new Vector3(lm.X, lm.Y, lm.Z);
            }
        }
        else
        {
            // 현재 프레임 face 없으면 마지막 얼굴(_lastFace5) 사용
            for (int i = 0; i < FaceIndices.Length; i++)
                face[i] = _lastFace5[i];
        }

        // nose = 얼굴 첫 포인트
        Vector3 nose = face[0];

        // face_width = 왼눈(33) - 오른눈(263) 거리
        Vector3 eyeL = face[1];
        Vector3 eyeR = face[2];
        float faceWidth = (eyeL - eyeR).magnitude;
        if (faceWidth < 1e-6f) faceWidth = 1e-6f;

        // 파이썬 norm: (x - nose) / face_width
        Vector3 Norm(Vector3 v) => (v - nose) / faceWidth;

        // 2) 왼손/오른손 21개 정규화
        var leftArr  = new Vector3[21];
        var rightArr = new Vector3[21];

        if (_leftHand != null && _leftHand.Landmark != null && _leftHand.Landmark.Count >= 21)
        {
            for (int i = 0; i < 21; i++)
            {
                var lm = _leftHand.Landmark[i];
                leftArr[i] = Norm(new Vector3(lm.X, lm.Y, lm.Z));
            }
        }
        else
        {
            for (int i = 0; i < 21; i++) leftArr[i] = Vector3.zero;
        }

        if (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count >= 21)
        {
            for (int i = 0; i < 21; i++)
            {
                var lm = _rightHand.Landmark[i];
                rightArr[i] = Norm(new Vector3(lm.X, lm.Y, lm.Z));
            }
        }
        else
        {
            for (int i = 0; i < 21; i++) rightArr[i] = Vector3.zero;
        }

        // 3) 얼굴 5포인트도 정규화
        for (int i = 0; i < face.Length; i++)
            face[i] = Norm(face[i]);

        // 4) flatten: left(21*3) + right(21*3) + face(5*3) = 141
        var feat = new float[141];
        int k = 0;

        for (int i = 0; i < 21; i++)
        {
            feat[k++] = leftArr[i].x;
            feat[k++] = leftArr[i].y;
            feat[k++] = leftArr[i].z;
        }

        for (int i = 0; i < 21; i++)
        {
            feat[k++] = rightArr[i].x;
            feat[k++] = rightArr[i].y;
            feat[k++] = rightArr[i].z;
        }

        for (int i = 0; i < 5; i++)
        {
            feat[k++] = face[i].x;
            feat[k++] = face[i].y;
            feat[k++] = face[i].z;
        }

        if (Application.isEditor)
        {
            float absMean = 0f;
            for (int i = 0; i < feat.Length; i++) absMean += Mathf.Abs(feat[i]);
            absMean /= feat.Length;
            Debug.Log($"[Feat141] absMean={absMean:F4}");
        }

        return feat;
    }

    /// <summary>
    /// 오른손 21개 포인트만 추출 (지화/spell 모델용)
    /// </summary>
    public Vector3[] GetRightHandLandmarks()
    {
        var rightArr = new Vector3[21];
        
        if (_rightHand != null && _rightHand.Landmark != null && _rightHand.Landmark.Count >= 21)
        {
            for (int i = 0; i < 21; i++)
            {
                var lm = _rightHand.Landmark[i];
                rightArr[i] = new Vector3(lm.X, lm.Y, lm.Z);
            }
        }
        else
        {
            for (int i = 0; i < 21; i++) rightArr[i] = Vector3.zero;
        }

        return rightArr;
    }
}