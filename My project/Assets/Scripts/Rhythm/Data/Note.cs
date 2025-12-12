using System;
using UnityEngine;

[Serializable]
public class Note
{
    public int noteId;
    public float timeSec;

    public int expectedIdx;
    public string expectedLabel;

    [NonSerialized] public bool judged;
}