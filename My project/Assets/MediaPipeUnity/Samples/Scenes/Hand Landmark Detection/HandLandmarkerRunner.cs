// Copyright (c) 2023 homuler
// MIT License

using System.Collections;
using System.Threading;
using Mediapipe;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
  public class HandLandmarkerRunner : VisionTaskApiRunner<HandLandmarker>
  {
    [SerializeField] private HandLandmarkerResultAnnotationController _handLandmarkerResultAnnotationController;
    [SerializeField] private MediaPipeResultFeeder feeder;

    private Experimental.TextureFramePool _textureFramePool;
    public readonly HandLandmarkDetectionConfig config = new HandLandmarkDetectionConfig();

    // LIVE_STREAM(Async) 결과를 콜백에서 받아서 메인스레드(Update)에서만 그리기/Feed
    private readonly object _latestLock = new object();
    private HandLandmarkerResult _latestResult; // 내부 포인터/버퍼 포함 가능하니 CloneTo로 복사해서 보관
    private int _hasNew; // 0/1

    public override void Stop()
    {
      base.Stop();
      _textureFramePool?.Dispose();
      _textureFramePool = null;
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
      if (result.handLandmarks == null || result.handLandmarks.Count == 0) return;

      NormalizedLandmarkList left = null;
      NormalizedLandmarkList right = null;

      for (int i = 0; i < result.handLandmarks.Count; i++)
      {
        string handName = null;
        // 버전 차이 대응 (try/catch)
        try
        {
          if (result.handedness != null && i < result.handedness.Count &&
              result.handedness[i].categories != null && result.handedness[i].categories.Count > 0)
          {
            handName = result.handedness[i].categories[0].categoryName; // "Left"/"Right"
          }
        }
        catch { }

        var list = ToNormalizedLandmarkListFromHand(result, i);
        if (list == null) continue;

        if (handName == "Left") left = list;
        else if (handName == "Right") right = list;
        else
        {
          if (left == null) left = list;
          else if (right == null) right = list;
        }
      }

      // face는 유지하고 싶으니 face=null
      feeder.Feed(null, left, right, null);
    }

    private void Update()
    {
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