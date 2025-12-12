using System;

[Serializable]
public struct Prediction
{
    public float timeSec;
    public string label;
    public float prob;
    public float dist;
    public float score;
    public int idx;
    public int signId;
    public bool locked;   // locked를 모델 쪽에서 넘겨주도록
}
