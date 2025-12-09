using UnityEngine;
using System.Collections.Generic;

public class SequenceBuilder : MonoBehaviour
{
    public List<float[]> Sequence = new List<float[]>();
    const int SEQ_LEN = 15;

    public float[] Normalize(Vector3[] lm)
    {
        float[] outArr = new float[63];

        Vector3 wrist = lm[0];
        Vector3 refPoint = lm[9];
        float scale = (refPoint - wrist).magnitude;

        for (int i = 0; i < 21; i++)
        {
            Vector3 v = (lm[i] - wrist) / scale;
            outArr[i * 3] = v.x;
            outArr[i * 3 + 1] = v.y;
            outArr[i * 3 + 2] = v.z;
        }

        return outArr;
    }

    public void AddFrame(float[] frame)
    {
        Sequence.Add(frame);
        if (Sequence.Count > SEQ_LEN)
            Sequence.RemoveAt(0);
    }

    public bool Ready()
    {
        return Sequence.Count == SEQ_LEN;
    }
}
