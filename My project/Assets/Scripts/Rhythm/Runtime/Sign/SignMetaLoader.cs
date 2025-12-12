using System.IO;
using UnityEngine;

public static class SignMetaLoader
{
    public static SignMeta LoadFromStreamingAssets(string relPath)
    {
        var path = Path.Combine(Application.streamingAssetsPath, relPath);
        var json = File.ReadAllText(path);
        return JsonUtility.FromJson<SignMeta>(json);
    }
}