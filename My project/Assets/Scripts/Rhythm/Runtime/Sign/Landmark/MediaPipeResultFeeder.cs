using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mp = Mediapipe;
using HandTasks = Mediapipe.Tasks.Vision.HandLandmarker;
using FaceTasks = Mediapipe.Tasks.Vision.FaceLandmarker;

/// <summary>
/// MediaPipe(Hands/Face/Holistic 등)에서 뽑은 landmark를
/// MediaPipeLandmarkSource로 "주입"해주는 브릿지.
/// </summary>
public class MediaPipeResultFeeder : MonoBehaviour
{
    [Header("Target")]
    public MediaPipeLandmarkSource target;

    private bool _printedHandMembers = false;
    private bool _loggedHandError = false;
    private bool _printedFaceMembers = false;

    // =========================================================
    // 1) HandLandmarkerResult → 손 랜드마크 주입
    // =========================================================
    public void Feed(HandTasks.HandLandmarkerResult result)
    {
        if (target == null) target = GetComponent<MediaPipeLandmarkSource>();
        if (target == null) return;   // result는 struct라 null 비교 X

        var t = result.GetType();

        // 한 번만 멤버 구조 찍어두기
        if (!_printedHandMembers)
        {
            foreach (var f in t.GetFields())
                Debug.Log($"[HandResultField] {f.Name} / {f.FieldType}");
            foreach (var p in t.GetProperties())
                Debug.Log($"[HandResultProp] {p.Name} / {p.PropertyType}");
            _printedHandMembers = true;
        }

        // handLandmarks : IList<...>
        object handVal =
              t.GetProperty("HandLandmarks")?.GetValue(result)
           ?? t.GetProperty("handLandmarks")?.GetValue(result)
           ?? t.GetField("HandLandmarks")?.GetValue(result)
           ?? t.GetField("handLandmarks")?.GetValue(result);

        var handList = handVal as IList;

        if (handList == null || handList.Count == 0)
        {
            if (!_loggedHandError)
            {
                Debug.Log("[HandFeeder] handLandmarks not found or empty; clearing.");
                _loggedHandError = true;
            }
            var empty = new Mp.NormalizedLandmarkList();
            target.SetFromMediaPipe(null, empty, empty, null);

            Debug.Log("hands: 0");
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

            // handedness[i].categories[0].categoryName / displayName 로 왼손/오른손 구분
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
                        string label = SafeGetStringProp(
                            c,
                            "categoryName", "CategoryName",
                            "displayName", "DisplayName"
                        );
                        if (!string.IsNullOrEmpty(label) &&
                            string.Equals(label, "Left", StringComparison.OrdinalIgnoreCase))
                        {
                            isLeft = true;
                        }
                        break; // 첫 카테고리만 사용
                    }
                }
            }

            var handObj = handList[i];
            var pts = ExtractLandmarksXYZ(handObj);
            var proto = ToProtoList(pts);

            if (isLeft) leftHand = proto;
            else        rightHand = proto;
        }

        if (leftHand != null && leftHand.Landmark.Count > 0)
        {
            var w = leftHand.Landmark[0];
            Debug.Log($"Left wrist: {w.X:F3}, {w.Y:F3}, {w.Z:F3}");
        }

        target.SetFromMediaPipe(null, leftHand, rightHand, null);
        Debug.Log($"hands: {handList.Count}");
    }

    // =========================================================
    // 2) FaceLandmarkerResult → 얼굴 랜드마크 주입
    // =========================================================
    public void Feed(FaceTasks.FaceLandmarkerResult result)
    {
        if (target == null) target = GetComponent<MediaPipeLandmarkSource>();
        if (target == null) return;

        var t = result.GetType();

        if (!_printedFaceMembers)
        {
            foreach (var f in t.GetFields())
                Debug.Log($"[FaceResultField] {f.Name} / {f.FieldType}");
            foreach (var p in t.GetProperties())
                Debug.Log($"[FaceResultProp] {p.Name} / {p.PropertyType}");
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
        }

        // pose/hand 유지, face만 갱신
        target.SetFromMediaPipe(null, null, null, face);
    }

    // =========================================================
    // 3) (옛 코드 호환용) proto 기반 Feed 오버로드
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

    // hand/face 컨테이너에서 (x,y,z) 리스트 뽑기
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

    // (x,y,z) 리스트 → Mp.NormalizedLandmarkList 로 변환
    private static Mp.NormalizedLandmarkList ToProtoList(List<(float x, float y, float z)> src)
    {
        var dst = new Mp.NormalizedLandmarkList();
        foreach (var (x, y, z) in src)
        {
            dst.Landmark.Add(new Mp.NormalizedLandmark
            {
                X = x,
                Y = y,
                Z = z,
            });
        }
        return dst;
    }

    // 리플렉션으로 float 프로퍼티/필드 읽기
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

    // 리플렉션으로 string 프로퍼티/필드 읽기
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