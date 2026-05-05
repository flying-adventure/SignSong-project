using UnityEngine;
using System.Collections.Generic;

// 최근 15프레임을 저장하는 버퍼
public class SignSequenceBuffer
{
    private readonly int sequenceLength;
    private readonly int featureDim;
    private readonly Queue<float[]> frames = new Queue<float[]>();

    public SignSequenceBuffer(int sequenceLength=15, int featureDim=141)
    {
        this.sequenceLength = sequenceLength;
        this.featureDim = featureDim;
    }
    public void AddFrame(float[] feature)
    {
        if (feature == null || feature.Length != featureDim)
        {
            return;
        }
        frames.Enqueue(feature);
        while (frames.Count > sequenceLength)
        {
            frames.Dequeue();
        }
    }

    public bool IsReady()
    {
        return frames.Count == sequenceLength;
    }

    public float[] ToFlattenedArray()
    {
        float[] result = new float[sequenceLength * featureDim];
        int frameIdx = 0;

        foreach(float[] frame in frames)
        {
            for (int j=0; j<featureDim; j++)
            {
                result[frameIdx*featureDim+j] = frame[j];
            }
            frameIdx++;
        }
        return result;
    }
    public void Clear()
    {
        frames.Clear();
    }
}
