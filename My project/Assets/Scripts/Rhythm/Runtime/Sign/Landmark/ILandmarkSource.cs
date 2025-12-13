using UnityEngine;

public interface ILandmarkSource
{
    // MediaPipe 결과 유무 플래그
    bool HasAnyHand { get; }
    bool HasFace { get; }

    bool TryGet(out Vector3[] a, out Vector3[] b, out Vector3[] c);

    // Sign 모델 입력(141 = 47 points * xyz) - 단어 모델용
    float[] GetFeature141();

    // Sign 모델 입력(63 = right hand 21 points * xyz, normalized) - 지화 모델용
    Vector3[] GetRightHandLandmarks();
}