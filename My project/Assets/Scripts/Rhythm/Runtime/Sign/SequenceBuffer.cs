using UnityEngine;

public sealed class SequenceBuffer
{
    private readonly float[,] buf; // [SeqLen, FeatDim]
    private readonly int featDim;
    private int count = 0;

    public int Count => count;
    public bool IsFull => count >= TFLiteSignRunner.SeqLen;

    /// <summary>
    /// featDim: 141(word) 또는 63(spell) 등
    /// </summary>
    public SequenceBuffer(int featDim = TFLiteSignRunner.FeatDim)
    {
        this.featDim = featDim;
        buf = new float[TFLiteSignRunner.SeqLen, featDim];
    }

    // feat: length = featDim (141 or 63)
    public void Push(float[] feat)
    {
        // shift up
        for (int t = 0; t < TFLiteSignRunner.SeqLen - 1; t++)
            for (int d = 0; d < featDim; d++)
                buf[t, d] = buf[t + 1, d];

        // write last
        int last = TFLiteSignRunner.SeqLen - 1;
        for (int d = 0; d < featDim; d++)
            buf[last, d] = feat[d];

        if (count < TFLiteSignRunner.SeqLen) count++;
    }

    public float[,] Snapshot() => buf; // 참조 반환(복사 없음)

    public void Clear()
    {
        count = 0;
        // Optionally zero buffer for safety
        for (int t = 0; t < TFLiteSignRunner.SeqLen; t++)
            for (int d = 0; d < featDim; d++)
                buf[t, d] = 0f;
    }
}