using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mp = Mediapipe;
using HandTasks = Mediapipe.Tasks.Vision.HandLandmarker;
using FaceTasks = Mediapipe.Tasks.Vision.FaceLandmarker;

public class MediaPipeResultFeeder : MonoBehaviour
{
    [Header("Target")]
    public MediaPipeLandmarkSource target;

    [Header("When landmarks missing")]
    public bool clearHandsWhenMissing = true;
    public bool clearFaceWhenMissing = false;

    [Header("Debug")]
    public bool debugLog = false;

    private bool _printedHandMembers = false;
    private bool _loggedHandEmptyOnce = false;
    private bool _printedFaceMembers = false;

    // =========================================================
    // 1) HandLandmarkerResult → 손 랜드마크 주입
    // =========================================================
    public void Feed(HandTasks.HandLandmarkerResult result)
    {
        if (target == null) target = GetComponent<MediaPipeLandmarkSource>();
        if (target == null) return;

        var t = result.GetType();

        if (debugLog && !_printedHandMembers)
        {
            foreach (var f in t.GetFields()) Debug.Log($"[HandResultField] {f.Name} / {f.FieldType}");
            foreach (var p in t.GetProperties()) Debug.Log($"[HandResultProp] {p.Name} / {p.PropertyType}");
            _printedHandMembers = true;
        }

        object handVal =
              t.GetProperty("HandLandmarks")?.GetValue(result)
           ?? t.GetProperty("handLandmarks")?.GetValue(result)
           ?? t.GetField("HandLandmarks")?.GetValue(result)
           ?? t.GetField("handLandmarks")?.GetValue(result);

        var handList = handVal as IList;

        if (handList == null || handList.Count == 0)
        {
            if (debugLog && !_loggedHandEmptyOnce)
            {
                Debug.Log("[HandFeeder] handLandmarks empty.");
                _loggedHandEmptyOnce = true;
            }

            if (clearHandsWhenMissing)
            {
                var empty = new Mp.NormalizedLandmarkList();
                target.SetFromMediaPipe(null, empty, empty, null);
            }
            else
            {
                // 유지: 손 값 갱신 안 함
                target.SetFromMediaPipe(null, null, null, null);
            }
            return;
        }

        // handedness : IList<...>
        object handedVal =
              t.GetProperty("Handedness")?.GetValue(result)
           ?? t.GetProperty("handedness")?.GetValue(result)
           ?? t.GetField("Handedness")?.GetValue(result)
           ?? t.GetField("handedness")?.GetValue(result);

        var handedList = handedVal as IList;

        Mp.NormalizedLandmarkList leftHand = null;
        Mp.NormalizedLandmarkList rightHand = null;

        for (int i = 0; i < handList.Count; i++)
        {
            bool isLeft = false;

            if (handedList != null && i < handedList.Count && handedList[i] != null)
            {
                var h = handedList[i];
                var ht = h.GetType();
                object catsVal =
                      ht.GetField("categories")?.GetValue(h)
                   ?? ht.GetProperty("Categories")?.GetValue(h)
                   ?? ht.GetProperty("categories")?.GetValue(h);

                if (catsVal is IEnumerable catsEnum)
                {
                    foreach (var c in catsEnum)
                    {
                        string label = SafeGetStringProp(c, "categoryName", "CategoryName", "displayName", "DisplayName");
                        if (!string.IsNullOrEmpty(label) &&
                            string.Equals(label, "Left", StringComparison.OrdinalIgnoreCase))
                        {
                            isLeft = true;
                        }
                        break;
                    }
                }
            }

            var handObj = handList[i];
            var pts = ExtractLandmarksXYZ(handObj);
            var proto = ToProtoList(pts);

            if (isLeft) leftHand = proto;
            else rightHand = proto;
        }

        target.SetFromMediaPipe(null, leftHand, rightHand, null);

        if (debugLog)
            Debug.Log($"[HandFeeder] hands={handList.Count}");
    }

    // =========================================================
    // 2) FaceLandmarkerResult → 얼굴 랜드마크 주입
    // =========================================================
    public void Feed(FaceTasks.FaceLandmarkerResult result)
    {
        if (target == null) target = GetComponent<MediaPipeLandmarkSource>();
        if (target == null) return;

        var t = result.GetType();

        if (debugLog && !_printedFaceMembers)
        {
            foreach (var f in t.GetFields()) Debug.Log($"[FaceResultField] {f.Name} / {f.FieldType}");
            foreach (var p in t.GetProperties()) Debug.Log($"[FaceResultProp] {p.Name} / {p.PropertyType}");
            _printedFaceMembers = true;
        }

        object faceVal =
              t.GetProperty("FaceLandmarks")?.GetValue(result)
           ?? t.GetProperty("faceLandmarks")?.GetValue(result)
           ?? t.GetField("FaceLandmarks")?.GetValue(result)
           ?? t.GetField("faceLandmarks")?.GetValue(result);

        var faceList = faceVal as IList;

        Mp.NormalizedLandmarkList face = null;

        if (faceList != null && faceList.Count > 0 && faceList[0] != null)
        {
            var pts = ExtractLandmarksXYZ(faceList[0]);
            face = ToProtoList(pts);
            target.SetFromMediaPipe(null, null, null, face);
        }
        else
        {
            if (clearFaceWhenMissing)
            {
                var empty = new Mp.NormalizedLandmarkList();
                target.SetFromMediaPipe(null, null, null, empty); // 명시적 clear
            }
            else
            {
                // 유지: lastFace5 캐시를 계속 쓰고 싶으면 갱신하지 않음
                target.SetFromMediaPipe(null, null, null, null);
            }
        }
    }

    // =========================================================
    // 3) proto 기반 Feed 오버로드
    // =========================================================
    public void Feed(
        Mp.NormalizedLandmarkList pose,
        Mp.NormalizedLandmarkList leftHand,
        Mp.NormalizedLandmarkList rightHand,
        Mp.NormalizedLandmarkList face = null)
    {
        if (target == null) target = GetComponent<MediaPipeLandmarkSource>();
        if (target == null) return;

        target.SetFromMediaPipe(pose, leftHand, rightHand, face);
    }

    public void OnHolisticOutput(
        Mp.NormalizedLandmarkList pose,
        Mp.NormalizedLandmarkList leftHand,
        Mp.NormalizedLandmarkList rightHand,
        Mp.NormalizedLandmarkList face)
    {
        Feed(pose, leftHand, rightHand, face);
    }

    // =========================================================
    // Helpers
    // =========================================================

    private static List<(float x, float y, float z)> ExtractLandmarksXYZ(object container)
    {
        var list = new List<(float, float, float)>();
        if (container == null) return list;

        var t = container.GetType();

        object lmVal =
              t.GetProperty("Landmarks")?.GetValue(container)
           ?? t.GetProperty("landmarks")?.GetValue(container)
           ?? t.GetField("Landmarks")?.GetValue(container)
           ?? t.GetField("landmarks")?.GetValue(container);

        if (lmVal is not IEnumerable lmEnum) return list;

        foreach (var lmObj in lmEnum)
        {
            float x = SafeGetFloatProp(lmObj, "X", "x");
            float y = SafeGetFloatProp(lmObj, "Y", "y");
            float z = SafeGetFloatProp(lmObj, "Z", "z");
            list.Add((x, y, z));
        }

        return list;
    }

    private static Mp.NormalizedLandmarkList ToProtoList(List<(float x, float y, float z)> src)
    {
        var dst = new Mp.NormalizedLandmarkList();
        foreach (var (x, y, z) in src)
        {
            dst.Landmark.Add(new Mp.NormalizedLandmark { X = x, Y = y, Z = z });
        }
        return dst;
    }

    private static float SafeGetFloatProp(object obj, params string[] names)
    {
        if (obj == null) return 0f;
        var t = obj.GetType();

        foreach (var name in names)
        {
            var p = t.GetProperty(name);
            if (p != null)
            {
                try
                {
                    var val = p.GetValue(obj);
                    if (val is float f) return f;
                    if (val is double d) return (float)d;
                    if (val is IConvertible conv) return Convert.ToSingle(conv);
                }
                catch { }
            }

            var fInfo = t.GetField(name);
            if (fInfo != null)
            {
                try
                {
                    var val = fInfo.GetValue(obj);
                    if (val is float f) return f;
                    if (val is double d) return (float)d;
                    if (val is IConvertible conv) return Convert.ToSingle(conv);
                }
                catch { }
            }
        }
        return 0f;
    }

    private static string SafeGetStringProp(object obj, params string[] names)
    {
        if (obj == null) return null;
        var t = obj.GetType();

        foreach (var name in names)
        {
            var p = t.GetProperty(name);
            if (p != null)
            {
                try
                {
                    var val = p.GetValue(obj);
                    if (val != null) return val.ToString();
                }
                catch { }
            }

            var fInfo = t.GetField(name);
            if (fInfo != null)
            {
                try
                {
                    var val = fInfo.GetValue(obj);
                    if (val != null) return val.ToString();
                }
                catch { }
            }
        }
        return null;
    }
}