// Copyright (c) 2023 homuler
// MIT License

using System.Collections;
using System.Threading;
using Mediapipe;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mediapipe.Unity.Sample.FaceLandmarkDetection
{
  public class FaceLandmarkerRunner : VisionTaskApiRunner<FaceLandmarker>
  {
    [SerializeField] private FaceLandmarkerResultAnnotationController _faceLandmarkerResultAnnotationController;
    [SerializeField] private MediaPipeResultFeeder feeder;

    private Experimental.TextureFramePool _textureFramePool;
    public readonly FaceLandmarkDetectionConfig config = new FaceLandmarkDetectionConfig();

    private readonly object _latestLock = new object();
    private FaceLandmarkerResult _latestResult;
    private int _hasNew;

    public override void Stop()
    {
      base.Stop();
      _textureFramePool?.Dispose();
      _textureFramePool = null;
    }

    private static NormalizedLandmarkList ToNormalizedLandmarkListFromFace(FaceLandmarkerResult result)
    {
      if (result.faceLandmarks == null || result.faceLandmarks.Count == 0) return null;

      var src = result.faceLandmarks[0];
      if (src.landmarks == null || src.landmarks.Count == 0) return null;

      var list = new NormalizedLandmarkList();
      for (int i = 0; i < src.landmarks.Count; i++)
      {
        var lm = src.landmarks[i];
        list.Landmark.Add(new NormalizedLandmark { X = lm.x, Y = lm.y, Z = lm.z });
      }
      return list;
    }

    private void FeedFaceMainThread(FaceLandmarkerResult result)
    {
      if (feeder == null) return;

      var faceList = ToNormalizedLandmarkListFromFace(result);
      if (faceList != null)
      {
        feeder.Feed(null, null, null, faceList);
      }
      // face가 없으면 Feed 안 함: MediaPipeLandmarkSource에서 lastFace5 캐시로 유지하도록 설계했으니 OK
    }

    private void Update()
    {
      if (Interlocked.Exchange(ref _hasNew, 0) == 0) return;

      FaceLandmarkerResult snapshot;
      lock (_latestLock)
      {
        snapshot = _latestResult;
      }

      _faceLandmarkerResultAnnotationController?.DrawNow(snapshot);
      FeedFaceMainThread(snapshot);
    }

    protected override IEnumerator Run()
    {
      yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

      var options = config.GetFaceLandmarkerOptions(
        config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnFaceLandmarkDetectionOutput : null);

      taskApi = FaceLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
      var imageSource = ImageSourceProvider.ImageSource;

      yield return imageSource.Play();
      if (!imageSource.isPrepared)
      {
        yield break;
      }

      _textureFramePool = new Experimental.TextureFramePool(
        imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

      screen.Initialize(imageSource);
      SetupAnnotationController(_faceLandmarkerResultAnnotationController, imageSource);

      var transformationOptions = imageSource.GetTransformationOptions();
      var flipHorizontally = transformationOptions.flipHorizontally;
      var flipVertically = transformationOptions.flipVertically;
      var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(
        rotationDegrees: (int)transformationOptions.rotationAngle);

      _latestResult = FaceLandmarkerResult.Alloc(options.numFaces);

      AsyncGPUReadbackRequest req = default;
      var waitUntilReqDone = new WaitUntil(() => req.done);
      var waitForEndOfFrame = new WaitForEndOfFrame();

      var result = FaceLandmarkerResult.Alloc(options.numFaces);

      var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
      using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

      while (true)
      {
        if (isPaused) yield return new WaitWhile(() => isPaused);

        if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
        {
          yield return null;
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
              _faceLandmarkerResultAnnotationController?.DrawNow(result);
              FeedFaceMainThread(result);
            }
            else
            {
              _faceLandmarkerResultAnnotationController?.DrawNow(default);
            }
            break;

          case Tasks.Vision.Core.RunningMode.VIDEO:
            if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result))
            {
              _faceLandmarkerResultAnnotationController?.DrawNow(result);
              FeedFaceMainThread(result);
            }
            else
            {
              _faceLandmarkerResultAnnotationController?.DrawNow(default);
            }
            break;

          case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
            taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
            break;
        }
      }
    }

    private void OnFaceLandmarkDetectionOutput(FaceLandmarkerResult result, Image image, long timestamp)
    {
      // 콜백 스레드: Unity API 호출 금지
      lock (_latestLock)
      {
        result.CloneTo(ref _latestResult);
      }
      Interlocked.Exchange(ref _hasNew, 1);
    }
  }
}