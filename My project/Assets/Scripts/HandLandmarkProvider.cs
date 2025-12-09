using UnityEngine;
using Mediapipe.Unity;
using Mediapipe;

public class HandLandmarkProvider : MonoBehaviour
{
    public Vector3[] CurrentLandmarks { get; private set; }
    public bool HasResult { get; private set; }

    void Start()
    {
        CurrentLandmarks = new Vector3[21];
        HasResult = false;
    }

    public void SetLandmarks(NormalizedLandmarkList landmarks)
    {
        if (landmarks == null)
        {
            HasResult = false;
            return;
        }

        for (int i = 0; i < 21; i++)
        {
            var lm = landmarks.Landmark[i];
            CurrentLandmarks[i] = new Vector3(lm.X, lm.Y, lm.Z);
        }

        HasResult = true;
    }
}
