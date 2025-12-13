using UnityEngine;

public sealed class SequenceBuffer
{
    private readonly float[,] buf; // [seqLen, featDim]
    private readonly int seqLen;
    private readonly int featDim;
    private int count = 0;

    public int Count => count;
    public int SeqLen => seqLen;
    public int FeatDim => featDim;
    public bool IsFull => count >= seqLen;

    public SequenceBuffer(int seqLen, int featDim)
    {
        this.seqLen = Mathf.Max(1, seqLen);
        this.featDim = Mathf.Max(1, featDim);
        buf = new float[this.seqLen, this.featDim];
    }

    public void Push(float[] feat)
    {
        if (feat == null || feat.Length != featDim) return;

        // shift up
        for (int t = 0; t < seqLen - 1; t++)
            for (int d = 0; d < featDim; d++)
                buf[t, d] = buf[t + 1, d];

        // write last
        int last = seqLen - 1;
        for (int d = 0; d < featDim; d++)
            buf[last, d] = feat[d];

        if (count < seqLen) count++;
    }

    public float[,] Snapshot() => buf; // 참조 반환(복사 없음)

    public void Clear(bool zero = true)
    {
        count = 0;
        if (!zero) return;

        for (int t = 0; t < seqLen; t++)
            for (int d = 0; d < featDim; d++)
                buf[t, d] = 0f;
    }
}