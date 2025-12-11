using UnityEngine;

public sealed class SequenceBuffer
{
    private readonly float[,] buf; // [15,141]
    private int count = 0;

    public int Count => count;
    public bool IsFull => count >= TFLiteSignRunner.SeqLen;

    public SequenceBuffer()
    {
        buf = new float[TFLiteSignRunner.SeqLen, TFLiteSignRunner.FeatDim];
    }

    // feat: length 141
    public void Push(float[] feat)
    {
        // shift up
        for (int t = 0; t < TFLiteSignRunner.SeqLen - 1; t++)
            for (int d = 0; d < TFLiteSignRunner.FeatDim; d++)
                buf[t, d] = buf[t + 1, d];

        // write last
        int last = TFLiteSignRunner.SeqLen - 1;
        for (int d = 0; d < TFLiteSignRunner.FeatDim; d++)
            buf[last, d] = feat[d];

        if (count < TFLiteSignRunner.SeqLen) count++;
    }

    public float[,] Snapshot() => buf; // 참조 반환(복사 없음)
}