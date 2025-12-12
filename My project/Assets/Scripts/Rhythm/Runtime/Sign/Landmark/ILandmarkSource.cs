using UnityEngine;

public interface ILandmarkSource
{
    // MediaPipe 결과 유무 플래그(필요 없으면 SignPredictionProvider에서 안 써도 됨)
    bool HasAnyHand { get; }
    bool HasFace { get; }

    // (컴파일 에러 원인) 기존 코드들이 기대하는 인터페이스 멤버
    // 3개 배열의 의미는 프로젝트마다 다를 수 있지만, 보통 (pose, leftHand, rightHand)로 씁니다.
    bool TryGet(out Vector3[] a, out Vector3[] b, out Vector3[] c);

    // Sign 모델 입력(141 = 47 points * xyz) - 단어 모델용
    float[] GetFeature141();

    // Sign 모델 입력(63 = right hand 21 points * xyz, normalized) - 지화 모델용
    Vector3[] GetRightHandLandmarks();
}