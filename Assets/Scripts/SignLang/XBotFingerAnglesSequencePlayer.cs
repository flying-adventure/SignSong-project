using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// arm_k 폴더 안에 있는 ㄱ_1 ~ ㄱ_4545.json 파일들을 순서대로 읽어서
/// XBotArmWristInspectorControl의 joints에 손가락 각도를 쭉 재생해주는 스크립트.
///
/// - JSON 예시:
///   {
///     "gesture_name": "ㄱ",
///     "frame_index": 1,
///     "finger_angles": {
///       "thumb":  [ ... 3개 ],
///       "index":  [ ... 3개 ],
///       "middle": [ ... 3개 ],
///       "ring":   [ ... 3개 ],
///       "pinky":  [ ... 3개 ]
///     }
///   }
///
/// - joints 배열은 다음 순서로 15개 넣어두면 됨:
///   0: thumb 관절 1
///   1: thumb 관절 2
///   2: thumb 관절 3
///   3: index 관절 1
///   4: index 관절 2
///   5: index 관절 3
///   6: middle 관절 1
///   7: middle 관절 2
///   8: middle 관절 3
///   9: ring 관절 1
///   10: ring 관절 2
///   11: ring 관절 3
///   12: pinky 관절 1
///   13: pinky 관절 2
///   14: pinky 관절 3
/// </summary>
public class XBotFingerAnglesSequencePlayer : MonoBehaviour
{
    [Header("각도를 실제로 적용해 줄 타겟 (같은 오브젝트에 있다면 비워도 됨)")]
    public XBotArmWristInspectorControl target;

    [Header("Assets/Resources/ 아래에 있는 폴더 이름")]
    [Tooltip("예: Assets/Resources/arm_k/ㄱ_1.json 이면 여기에는 \"arm_k\"")]
    public string resourcesFolder = "arm_k";

    [Header("초당 몇 프레임으로 재생할지")]
    public float frameRate = 30f;

    [Header("끝까지 재생 후 다시 처음부터 반복할지")]
    public bool loop = true;

    [Header("저장된 각도를 180 - angle로 뒤집을지 (필요하면 체크)")]
    public bool use180Minus = false;

    // ─────────────────────────────────────────────
    // JSON 파싱용 내부 클래스들
    // ─────────────────────────────────────────────
    [Serializable]
    private class FingerAngles
    {
        public float[] thumb;
        public float[] index;
        public float[] middle;
        public float[] ring;
        public float[] pinky;
    }

    [Serializable]
    private class GestureFrame
    {
        public string gesture_name;
        public int frame_index;
        public FingerAngles finger_angles;
    }

    private readonly List<float[]> _frames = new List<float[]>();
    private int _frameCount;
    private float _time;
    private bool _isReady = false;

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<XBotArmWristInspectorControl>();
        }

        LoadFramesFromResources();
    }

    private void LoadFramesFromResources()
    {
        if (string.IsNullOrEmpty(resourcesFolder))
        {
            Debug.LogError("[XBotFingerAnglesSequencePlayer] resourcesFolder가 비어 있습니다.");
            return;
        }

        TextAsset[] assets = Resources.LoadAll<TextAsset>(resourcesFolder);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[XBotFingerAnglesSequencePlayer] JSON TextAsset을 찾지 못했습니다. 폴더 경로 확인: " + resourcesFolder);
            return;
        }

        // 이름 순 정렬 (ㄱ_1, ㄱ_2, ... ㄱ_4545 순서대로)
        Array.Sort(assets, (a, b) =>
            StringComparer.OrdinalIgnoreCase.Compare(a.name, b.name));

        _frames.Clear();

        foreach (var ta in assets)
        {
            try
            {
                var gf = JsonUtility.FromJson<GestureFrame>(ta.text);
                if (gf == null || gf.finger_angles == null)
                {
                    Debug.LogWarning("[XBotFingerAnglesSequencePlayer] 잘못된 JSON 포맷: " + ta.name);
                    continue;
                }

                // thumb/index/middle/ring/pinky 순으로 3개씩 → 총 15개로 flat
                List<float> list = new List<float>(15);
                AppendFinger(list, gf.finger_angles.thumb);
                AppendFinger(list, gf.finger_angles.index);
                AppendFinger(list, gf.finger_angles.middle);
                AppendFinger(list, gf.finger_angles.ring);
                AppendFinger(list, gf.finger_angles.pinky);

                if (list.Count != 15)
                {
                    Debug.LogWarning("[XBotFingerAnglesSequencePlayer] angles 개수가 15가 아님: " + ta.name + " / count = " + list.Count);
                    continue;
                }

                _frames.Add(list.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogError("[XBotFingerAnglesSequencePlayer] JSON 파싱 실패: " + ta.name + "\n" + e);
            }
        }

        _frameCount = _frames.Count;
        if (_frameCount == 0)
        {
            Debug.LogError("[XBotFingerAnglesSequencePlayer] 유효한 프레임이 하나도 없습니다.");
            return;
        }

        Debug.Log("[XBotFingerAnglesSequencePlayer] 프레임 로드 완료: " + _frameCount + "개 (" + resourcesFolder + ")");
        _isReady = true;
        _time = 0f;
    }

    private void AppendFinger(List<float> list, float[] src)
    {
        if (src == null) return;

        for (int i = 0; i < src.Length; i++)
        {
            float v = src[i];

            // 필요 시 180 - angle 변환 (관절 각도 정의에 따라)
            if (use180Minus)
                v = 180f - v;

            list.Add(v);
        }
    }

    private void Update()
    {
        if (!_isReady || target == null || _frameCount == 0) return;

        _time += Time.deltaTime;
        int totalFramesPlayed = Mathf.FloorToInt(_time * frameRate);

        int frameIndex;
        if (loop)
        {
            frameIndex = totalFramesPlayed % _frameCount;  // 0 ~ _frameCount-1 순환
        }
        else
        {
            frameIndex = Mathf.Clamp(totalFramesPlayed, 0, _frameCount - 1);
        }

        ApplyFrame(frameIndex);
    }

    private void ApplyFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frameCount) return;

        float[] angles = _frames[frameIndex];
        target.SetFrameAngles(angles);
    }
}