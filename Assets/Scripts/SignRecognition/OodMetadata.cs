using UnityEngine;
using System;

[Serializable]
public class OodMetadata
{
    public float confidence_threshold;
    public float distance_threshold;
    public float distance_threshold_std_factor;
    public string[] class_names;
}