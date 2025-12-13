using System.Collections.Generic;

public class PredictionRingBuffer
{
    private readonly LinkedList<Prediction> _buf = new();
    private readonly float _keepSec;

    public PredictionRingBuffer(float keepSec = 1.0f) => _keepSec = keepSec;

    public void Add(Prediction p) => _buf.AddLast(p);

    public void Prune(float nowSec)
    {
        float minTime = nowSec - _keepSec;
        while (_buf.First != null && _buf.First.Value.timeSec < minTime)
            _buf.RemoveFirst();
    }

    public void GetBetween(float tMin, float tMax, List<Prediction> outList)
    {
        outList.Clear();
        for (var node = _buf.First; node != null; node = node.Next)
        {
            float t = node.Value.timeSec;
            if (t >= tMin && t <= tMax) outList.Add(node.Value);
        }
    }
}