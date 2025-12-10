using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 제스처 클립 JSON (frames 배열 포함)을 읽어서
/// XBotArmWristInspectorControl.SetFrameAngles()로 재생하는 플레이어.
///
/// JSON 예시:
/// {
///   "gesture_name": "ㄱ",
///   "fps": 30,
///   "frames": [
///     {
///       "frame_index": 1,
///       "finger_angles": {
///         "thumb":  [ ... 3개 ],
///         "index":  [ ... 3개 ],
///         "middle": [ ... 3개 ],
///         "ring":   [ ... 3개 ],
///         "pinky":  [ ... 3개 ]
///       }
///     },
///     ...
///   ]
/// }
///
/// XBotFingerAnglesSequencePlayer와 동일하게
///  - thumb/index/middle/ring/pinky 각각 3개씩 → 총 15개 float 배열을 만들어
///    XBotArmWristInspectorControl.SetFrameAngles(float[15])에 전달한다.
/// </summary>
public class XBotGestureClipPlayer : MonoBehaviour
{
    [Header("각도를 적용할 대상 (같은 오브젝트에 있다면 비워도 됨)")]
    public XBotArmWristInspectorControl target;

    [Header("Resources 폴더 기준 경로 (폴더/파일명)")]
    [Tooltip("예: Assets/Resources/gesture_clips/ㄱ.json 이면 여기에는 \"gesture_clips/ㄱ\"")]
    public string resourcePath = "gesture_clips/ㄱ";

    [Header("Inspector에서 강제로 쓸 FPS (useJsonFps=false 일 때만 사용)")]
    public float frameRate = 30f;

    [Header("JSON 안에 있는 fps 값을 쓸지 여부")]
    public bool useJsonFps = true;

    [Header("끝까지 재생 후 다시 처음부터 반복할지")]
    public bool loop = true;

    [Header("필요하면 180 - angle 로 뒤집기 (기존 SequencePlayer와 동일 옵션)")]
    public bool use180Minus = false;

    // ─────────────────────────────────────────────
    // JSON 구조 정의 (파싱용)
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
    private class ClipFrame
    {
        public int frame_index;
        public FingerAngles finger_angles;
    }

    [Serializable]
    private class GestureClip
    {
        public string gesture_name;
        public float fps;
        public ClipFrame[] frames;
    }

    // 내부 상태
    private readonly List<float[]> _frames = new List<float[]>();
    private int _frameCount;
    private float _time;
    private bool _isReady = false;
    private float _usedFrameRate = 30f;

    private void Awake()
    {
        if (target == null)
            target = GetComponent<XBotArmWristInspectorControl>();

        LoadClipFromResources();
    }

    private void LoadClipFromResources()
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            Debug.LogError("[XBotGestureClipPlayer] resourcePath가 비어 있습니다.");
            return;
        }

        TextAsset ta = Resources.Load<TextAsset>(resourcePath);
        if (ta == null)
        {
            Debug.LogError("[XBotGestureClipPlayer] JSON TextAsset을 찾지 못했습니다. 경로: " + resourcePath);
            return;
        }

        GestureClip clip = null;
        try
        {
            clip = JsonUtility.FromJson<GestureClip>(ta.text);
        }
        catch (Exception e)
        {
            Debug.LogError("[XBotGestureClipPlayer] JSON 파싱 실패: " + resourcePath + "\n" + e);
            return;
        }

        if (clip == null || clip.frames == null || clip.frames.Length == 0)
        {
            Debug.LogError("[XBotGestureClipPlayer] 클립에 프레임이 없습니다: " + resourcePath);
            return;
        }

        _frames.Clear();
        foreach (var cf in clip.frames)
        {
            if (cf == null || cf.finger_angles == null)
                continue;

            List<float> list = new List<float>(15);
            AppendFinger(list, cf.finger_angles.thumb);
            AppendFinger(list, cf.finger_angles.index);
            AppendFinger(list, cf.finger_angles.middle);
            AppendFinger(list, cf.finger_angles.ring);
            AppendFinger(list, cf.finger_angles.pinky);

            if (list.Count != 15)
            {
                Debug.LogWarning("[XBotGestureClipPlayer] angles 개수가 15가 아님: frame_index = " +
                                 cf.frame_index + " / count = " + list.Count);
                continue;
            }

            _frames.Add(list.ToArray());
        }

        _frameCount = _frames.Count;
        if (_frameCount == 0)
        {
            Debug.LogError("[XBotGestureClipPlayer] 유효한 프레임이 하나도 없습니다.");
            return;
        }

        // fps 결정
        if (useJsonFps && clip.fps > 0f)
            _usedFrameRate = clip.fps;
        else
            _usedFrameRate = Mathf.Max(1f, frameRate);

        Debug.Log($"[XBotGestureClipPlayer] \"{clip.gesture_name}\" 클립 로드 완료: " +
                  $"{_frameCount} 프레임, fps = {_usedFrameRate} (path={resourcePath})");

        _isReady = true;
        _time = 0f;
    }

    private void AppendFinger(List<float> list, float[] src)
    {
        if (src == null) return;

        for (int i = 0; i < src.Length; i++)
        {
            float v = src[i];
            if (use180Minus)
                v = 180f - v;

            list.Add(v);
        }
    }

    private void Update()
    {
        if (!_isReady || target == null || _frameCount == 0) return;

        _time += Time.deltaTime;
        int totalFramesPlayed = Mathf.FloorToInt(_time * _usedFrameRate);

        int frameIndex;
        if (loop)
            frameIndex = totalFramesPlayed % _frameCount;   // 0 ~ _frameCount-1 순환
        else
            frameIndex = Mathf.Clamp(totalFramesPlayed, 0, _frameCount - 1);

        ApplyFrame(frameIndex);
    }

    private void ApplyFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frameCount) return;

        float[] angles = _frames[frameIndex];
        target.SetFrameAngles(angles);   // ★ 기존에 잘 동작하던 로직 그대로 사용
    }
}