using UnityEngine;
using UnityEngine.InputSystem;

public class SignRecognizerTest : MonoBehaviour
{
    public SignTcnRecognizer recognizer;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            float[] dummyInput = new float[15*141];
            for (int i=0; i<dummyInput.Length; i++)
            {
                dummyInput[i] = 0f;
            }
            SignRecognitionResult result = recognizer.Predict(dummyInput);

            Debug.Log(
                $"[Test] label={result.label}, " +
                $"idx={result.classIndex}, " +
                $"conf={result.confidence}, " +
                $"dist={result.distance}, " +
                $"accepted={result.accepted}"
            );
        }
    }
}
