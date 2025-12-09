using UnityEngine;
using System.Collections.Generic;

public static class JsonLoader
{
    public static List<float[]> Load2D(string path)
    {
        string json = System.IO.File.ReadAllText(path);
        return JsonUtility.FromJson<FloatArrayList>(json).ToList();
    }

    public static float LoadThreshold(string path)
    {
        string json = System.IO.File.ReadAllText(path);
        return JsonUtility.FromJson<Threshold>(json).threshold;
    }

    [System.Serializable]
    public class FloatArrayList
    {
        public List<float[]> list;
        public List<float[]> ToList() => list;
    }

    [System.Serializable]
    public class Threshold
    {
        public float threshold;
    }
}
