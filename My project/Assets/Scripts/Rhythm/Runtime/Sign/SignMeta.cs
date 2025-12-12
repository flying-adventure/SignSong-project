using System;
using UnityEngine;

[Serializable]
public class SignMeta
{
    public string[] classNames;
    public float distanceThreshold;
    public int centroidDim;
    public float[] centroidsFlat; // (classCount * centroidDim)
    public int ClassCount => classNames != null ? classNames.Length : 0;

    public void CopyCentroid(int classIdx, float[] dst)
    {
        if (dst == null || dst.Length != centroidDim) throw new ArgumentException("dst size");
        int off = classIdx * centroidDim;
        for (int i = 0; i < centroidDim; i++) dst[i] = centroidsFlat[off + i];
    }
}