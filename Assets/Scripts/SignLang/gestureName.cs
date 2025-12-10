using System;
using UnityEngine;

[Serializable]
public class FingerAnglesData
{
    public float[] thumb;
    public float[] index;
    public float[] middle;
    public float[] ring;
    public float[] pinky;
}

[Serializable]
public class GestureFrame
{
    public int frame_index;
    public FingerAnglesData finger_angles;
}

[Serializable]
public class GestureClip
{
    public string gesture_name;
    public float fps;
    public GestureFrame[] frames;
}