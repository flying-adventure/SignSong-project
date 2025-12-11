using UnityEngine;

#if MEDIAPIPE
using Mediapipe;
#endif

/// MediaPipe에서 나온 pose/hand/face landmark를 모아서
/// MediaPipeResultFeeder로 한번에 Feed 해주는 라우터(브릿지).
public class MediaPipeLandmarkRouter : MonoBehaviour
{
    [Header("Wiring")]
    public MediaPipeResultFeeder feeder;

    [Header("Options")]
    public bool requireAtLeastOneHand = true;

#if MEDIAPIPE
    private NormalizedLandmarkList _pose;
    private NormalizedLandmarkList _left;
    private NormalizedLandmarkList _right;
    private NormalizedLandmarkList _face;

    // ↓↓↓ MediaPipe 쪽(샘플/솔루션)에서 랜드마크를 얻는 지점에서 이 메서드들을 호출하면 됨 ↓↓↓
    public void OnPose(NormalizedLandmarkList pose)  { _pose = pose; TryFeed(); }
    public void OnLeftHand(NormalizedLandmarkList l) { _left = l; TryFeed(); }
    public void OnRightHand(NormalizedLandmarkList r){ _right = r; TryFeed(); }
    public void OnFace(NormalizedLandmarkList face)  { _face = face; TryFeed(); }

    private void TryFeed()
    {
        if (feeder == null) return;

        if (requireAtLeastOneHand)
        {
            if (_left == null && _right == null) return;
        }

        // hands만 있어도 되고, pose/face는 null이어도 됨(너의 MediaPipeLandmarkSource가 null 처리하도록 만들었음)
        feeder.Feed(_pose, _left, _right, _face);
    }
#else
    private void Awake()
    {
        Debug.LogError("[MediaPipeLandmarkRouter] MEDIAPIPE define이 없거나 Mediapipe 네임스페이스를 못 찾습니다.");
    }
#endif
}