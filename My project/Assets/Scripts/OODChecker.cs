using UnityEngine;
using System.Collections.Generic;

public class OODChecker : MonoBehaviour
{
    public List<float[]> centroids;
    public float threshold;

    public int Check(float[] classProb)
    {
        float max = -1f;
        int idx = -1;
        for (int i = 0; i < classProb.Length; i++)
        {
            if (classProb[i] > max)
            {
                max = classProb[i];
                idx = i;
            }
        }

        if (max < 0.6f)
            return -1;

        return idx;
    }

    public bool DistanceOK(float[] embed, int idx)
    {
        float dist = 0;
        for (int i = 0; i < embed.Length; i++)
        {
            float d = embed[i] - centroids[idx][i];
            dist += d * d;
        }

        dist = Mathf.Sqrt(dist);
        return dist <= threshold;
    }
}
