// Copyright (c) 2023 homuler
// MIT License

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Mediapipe;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
  public class HandLandmarkerRunner : VisionTaskApiRunner<HandLandmarker>
  {
    [SerializeField] private HandLandmarkerResultAnnotationController _handLandmarkerResultAnnotationController;
    [SerializeField] private MediaPipeResultFeeder feeder;

    [SerializeField] private bool swapHandednessLR = true; // 셀피/미러면 true
    [SerializeField] private bool debugHandedness = true;

    private Experimental.TextureFramePool _textureFramePool;
    public readonly HandLandmarkDetectionConfig config = new HandLandmarkDetectionConfig();

    // LIVE_STREAM(Async) 결과를 콜백에서 받아서 메인스레드(Update)에서만 그리기/Feed
    private readonly object _latestLock = new object();
    private HandLandmarkerResult _latestResult; // 내부 포인터/버퍼 포함 가능하니 CloneTo로 복사해서 보관
    private int _hasNew; // 0/1

    private bool _flipH = false;

    public override void Stop()
    {
      base.Stop();
      _textureFramePool?.Dispose();
      _textureFramePool = null;
    }

    private static float SafeGetFloat(object obj, params string[] names)
    {
        if (obj == null) return 0f;
        var t = obj.GetType();

        foreach (var n in names)
        {
            var p = t.GetProperty(n);
            if (p != null)
            {
                var v = p.GetValue(obj);
                if (v is float f) return f;
                if (v is double d) return (float)d;
            }
            var fInfo = t.GetField(n);
            if (fInfo != null)
            {
                var v = fInfo.GetValue(obj);
                if (v is float f) return f;
                if (v is double d) return (float)d;
            }
        }
        return 0f;
    }

    private static bool TryBuildListAndWristX(HandLandmarkerResult result, int handIdx,
                                            out NormalizedLandmarkList list,
                                            out float wristX)
    {
        list = null;
        wristX = 0f;

        if (result.handLandmarks == null || result.handLandmarks.Count == 0) return false;
        if (handIdx < 0 || handIdx >= result.handLandmarks.Count) return false;

        var src = result.handLandmarks[handIdx];
        if (src.landmarks == null || src.landmarks.Count == 0) return false;

        // wristX는 "원본 landmark"에서 먼저 안전하게 읽기
        var wrist = src.landmarks[0];
        wristX = SafeGetFloat(wrist, "x", "X"); // 버전 차이 대응

        // NormalizedLandmarkList 구성
        list = new NormalizedLandmarkList();
        for (int i = 0; i < src.landmarks.Count; i++)
        {
            var lm = src.landmarks[i];
            float x = SafeGetFloat(lm, "x", "X");
            float y = SafeGetFloat(lm, "y", "Y");
            float z = SafeGetFloat(lm, "z", "Z");
            list.Landmark.Add(new NormalizedLandmark { X = x, Y = y, Z = z });
        }
        return true;
    }

    private static NormalizedLandmarkList ToNormalizedLandmarkListFromHand(HandLandmarkerResult result, int handIdx)
    {
      // result 자체는 null 비교하지 말고, 내용만 체크
      if (result.handLandmarks == null || result.handLandmarks.Count == 0) return null;
      if (handIdx < 0 || handIdx >= result.handLandmarks.Count) return null;

      var src = result.handLandmarks[handIdx];
      if (src.landmarks == null || src.landmarks.Count == 0) return null;

      var list = new NormalizedLandmarkList();
      for (int i = 0; i < src.landmarks.Count; i++)
      {
        var lm = src.landmarks[i];
        list.Landmark.Add(new NormalizedLandmark { X = lm.x, Y = lm.y, Z = lm.z });
      }
      return list;
    }

    private void FeedHandsMainThread(HandLandmarkerResult result)
    {
        if (feeder == null) return;
        bool isSpellScene = SceneManager.GetActiveScene().name == "Game_1";

        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            // Game_1/2 공통: 손이 없어졌으면 손 데이터 비우기
            feeder.Feed(null, new NormalizedLandmarkList(), new NormalizedLandmarkList(), null);

            if (debugHandedness && (Time.frameCount % 30 == 0))
                Debug.Log("[HandPick] hands=0 -> CLEAR(LH,RH)");

            return;
        }

        if (isSpellScene)
            FeedHandsSpell_XPick(result);        // Game_1: x기반 (오른손 게임 강제)
        else
            FeedHandsWord_XPickBoth(result);    // Game_2: handedness 기반 (기존 유지)
    }

    private void FeedHandsSpell_XPick(HandLandmarkerResult result)
    {
        // 1) 후보 리스트 + wrist.x 수집
        var lists = new List<NormalizedLandmarkList>();
        var xs = new List<float>();

        for (int i = 0; i < result.handLandmarks.Count; i++)
        {
            if (!TryBuildListAndWristX(result, i, out var list, out var wristX))
                continue;

            lists.Add(list);
            xs.Add(wristX);
        }

        if (lists.Count == 0) return;

        NormalizedLandmarkList left = null;
        NormalizedLandmarkList right = null;

        // 2) 한 손만 보이면: 그 손을 RH로
        if (lists.Count == 1)
        {
            right = lists[0];
        }
        else
        {
            // 3) 두 손 이상이면: 화면 x 위치로 RH/LH 결정
            int idxRight = 0;
            float best = xs[0];

            for (int i = 1; i < xs.Count; i++)
            {
                bool take =
                    _flipH ? (xs[i] > best)   // flipH면 x 큰쪽이 RH
                          : (xs[i] < best);  // flipH 아니면 x 작은쪽이 RH

                if (take) { best = xs[i]; idxRight = i; }
            }

            right = lists[idxRight];

            for (int i = 0; i < lists.Count; i++)
            {
                if (i == idxRight) continue;
                left = lists[i];
                break;
            }
        }

        if (debugHandedness && (Time.frameCount % 30 == 0))
        {
            float rx = (right != null && right.Landmark.Count > 0) ? right.Landmark[0].X : -1f;
            float lx = (left  != null && left.Landmark.Count > 0) ? left.Landmark[0].X : -1f;
            Debug.Log($"[HandPick-SPELL] flipH={_flipH} RH_wristX={rx:F3} LH_wristX={lx:F3} hands={lists.Count}");
        }

        feeder.Feed(null, left, right, null);
    }

    private void FeedHandsWord_XPickBoth(HandLandmarkerResult result)
    {
        if (feeder == null) return;

        var lists = new List<NormalizedLandmarkList>();
        var xs = new List<float>();

        for (int i = 0; i < result.handLandmarks.Count; i++)
        {
            if (!TryBuildListAndWristX(result, i, out var list, out var wristX))
                continue;

            lists.Add(list);
            xs.Add(wristX);
        }

        // 손이 하나도 없으면: (선택) 손 클리어를 원하면 empty를 넣고, 아니면 그냥 return
        if (lists.Count == 0)
        {
            feeder.Feed(null, null, null, null);
            return;
        }

        NormalizedLandmarkList left = null;
        NormalizedLandmarkList right = null;

        if (lists.Count == 1)
        {
            // 한 손만 감지되면 일단 RH로만 넣어두고, LH는 명시적 클리어
            right = lists[0];
            left  = new NormalizedLandmarkList(); // empty
        }
        else
        {
            // x 2개 이상이면 min/max로 좌/우 결정
            int idxMin = 0, idxMax = 0;
            float minX = xs[0], maxX = xs[0];

            for (int i = 1; i < xs.Count; i++)
            {
                if (xs[i] < minX) { minX = xs[i]; idxMin = i; }
                if (xs[i] > maxX) { maxX = xs[i]; idxMax = i; }
            }

            // Spell에서 쓰던 규칙을 그대로 확장:
            // flipH=false이면 x 작은 쪽이 RH, x 큰 쪽이 LH
            // flipH=true이면 반대로
            if (_flipH)
            {
                right = lists[idxMax];
                left  = lists[idxMin];
            }
            else
            {
                right = lists[idxMin];
                left  = lists[idxMax];
            }
        }

        if (debugHandedness && (Time.frameCount % 30 == 0))
        {
            float rx = (right != null && right.Landmark.Count > 0) ? right.Landmark[0].X : -1f;
            float lx = (left  != null && left.Landmark.Count > 0) ? left.Landmark[0].X : -1f;
            Debug.Log($"[HandPick-WORD-X] flipH={_flipH} RH_wristX={rx:F3} LH_wristX={lx:F3} hands={lists.Count}");
        }

        feeder.Feed(null, left, right, null);
    }

    private void FeedHandsWord_Handedness(HandLandmarkerResult result)
    {
        NormalizedLandmarkList left = null;
        NormalizedLandmarkList right = null;

        for (int i = 0; i < result.handLandmarks.Count; i++)
        {
            // list 생성(안정적으로)
            if (!TryBuildListAndWristX(result, i, out var list, out _))
                continue;

            string handName = null;
            try
            {
                if (result.handedness != null && i < result.handedness.Count &&
                    result.handedness[i].categories != null && result.handedness[i].categories.Count > 0)
                {
                    handName = result.handedness[i].categories[0].categoryName; // "Left"/"Right"
                }
            }
            catch { }

            // 필요하면(셀피/미러 환경)만 swap: 기존에 쓰던 swapHandednessLR 그대로 활용
            // 보통: _flipH 환경이면 swap이 필요할 때가 많아서, 아래처럼 자동으로 두는 것도 가능
            bool swap = swapHandednessLR; // 또는: bool swap = _flipH;

            if (swap)
            {
                if (string.Equals(handName, "Left", System.StringComparison.OrdinalIgnoreCase)) handName = "Right";
                else if (string.Equals(handName, "Right", System.StringComparison.OrdinalIgnoreCase)) handName = "Left";
            }

            if (string.Equals(handName, "Left", System.StringComparison.OrdinalIgnoreCase))
                left = list;
            else if (string.Equals(handName, "Right", System.StringComparison.OrdinalIgnoreCase))
                right = list;
            else
            {
                // handedness가 비는 예외 케이스 fallback
                if (left == null) left = list;
                else if (right == null) right = list;
            }
        }

        if (debugHandedness && (Time.frameCount % 30 == 0))
        {
            float rx = (right != null && right.Landmark.Count > 0) ? right.Landmark[0].X : -1f;
            float lx = (left  != null && left.Landmark.Count > 0) ? left.Landmark[0].X : -1f;
            Debug.Log($"[HandPick-WORD] swap={swapHandednessLR} RH_wristX={rx:F3} LH_wristX={lx:F3}");
        }

        feeder.Feed(null, left, right, null);
    }

    private void Update()
    {
      if (Time.frameCount % 30 == 0) Debug.Log("[HLR] Update tick");

      // LIVE_STREAM(Async)에서만 사용되지만, 안전하게 "새 결과 있을 때만" 처리
      if (Interlocked.Exchange(ref _hasNew, 0) == 0) return;

      HandLandmarkerResult snapshot;
      lock (_latestLock)
      {
        snapshot = _latestResult;
      }

      _handLandmarkerResultAnnotationController.DrawNow(snapshot);
      FeedHandsMainThread(snapshot);
    }

    protected override IEnumerator Run()
    {
      yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

      var options = config.GetHandLandmarkerOptions(
        config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnHandLandmarkDetectionOutput : null);

      taskApi = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
      var imageSource = ImageSourceProvider.ImageSource;

      yield return imageSource.Play();
      if (!imageSource.isPrepared)
      {
        yield break;
      }

      _textureFramePool = new Experimental.TextureFramePool(
        imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

      screen.Initialize(imageSource);
      SetupAnnotationController(_handLandmarkerResultAnnotationController, imageSource);

      var transformationOptions = imageSource.GetTransformationOptions();
      var flipHorizontally = transformationOptions.flipHorizontally;
      var flipVertically = transformationOptions.flipVertically;

      _flipH = flipHorizontally;

      var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(
        rotationDegrees: (int)transformationOptions.rotationAngle);

      _latestResult = HandLandmarkerResult.Alloc(options.numHands);

      AsyncGPUReadbackRequest req = default;
      var waitUntilReqDone = new WaitUntil(() => req.done);
      var waitForEndOfFrame = new WaitForEndOfFrame();

      // VIDEO/IMAGE 모드에서만 쓰는 버퍼
      var result = HandLandmarkerResult.Alloc(options.numHands);

      var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
      using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

      while (true)
      {
        if (isPaused) yield return new WaitWhile(() => isPaused);

        if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
        {
          yield return new WaitForEndOfFrame();
          continue;
        }

        Image image;
        switch (config.ImageReadMode)
        {
          case ImageReadMode.GPU:
            if (!canUseGpuImage) throw new System.Exception("ImageReadMode.GPU is not supported");
            textureFrame.ReadTextureOnGPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            image = textureFrame.BuildGPUImage(glContext);
            yield return waitForEndOfFrame;
            break;

          case ImageReadMode.CPU:
            yield return waitForEndOfFrame;
            textureFrame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            image = textureFrame.BuildCPUImage();
            textureFrame.Release();
            break;

          case ImageReadMode.CPUAsync:
          default:
            req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            yield return waitUntilReqDone;
            if (req.hasError)
            {
              Debug.LogWarning("Failed to read texture from the image source");
              continue;
            }
            image = textureFrame.BuildCPUImage();
            textureFrame.Release();
            break;
        }

        switch (taskApi.runningMode)
        {
          case Tasks.Vision.Core.RunningMode.IMAGE:
            if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
            {
              _handLandmarkerResultAnnotationController?.DrawNow(result);
              FeedHandsMainThread(result);
            }
            else
            {
              _handLandmarkerResultAnnotationController?.DrawNow(default);
            }
            break;

          case Tasks.Vision.Core.RunningMode.VIDEO:
            if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result))
            {
              _handLandmarkerResultAnnotationController?.DrawNow(result);
              FeedHandsMainThread(result);
            }
            else
            {
              _handLandmarkerResultAnnotationController?.DrawNow(default);
            }
            break;

          case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
            taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
            break;
        }
      }
    }

    private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
    {
      // 콜백 스레드: Unity API 호출 금지 (DrawNow 금지)
      lock (_latestLock)
      {
        result.CloneTo(ref _latestResult);
      }
      Interlocked.Exchange(ref _hasNew, 1);
    }
  }
}