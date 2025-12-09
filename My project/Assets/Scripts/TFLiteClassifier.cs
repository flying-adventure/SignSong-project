using UnityEngine;
using TensorFlowLite;

public class TFLiteClassifier : MonoBehaviour
{
    Interpreter classifier;
    Interpreter embedder;

    float[,,] input = new float[1, 15, 63];
    float[] classOutput;
    float[] embedOutput;

    public void Init(byte[] classifierFile, byte[] embedFile, int classDim, int embedDim)
    {
        classifier = new Interpreter(classifierFile);
        classifier.ResizeInputTensor(0, new int[] { 1, 15, 63 });
        classifier.AllocateTensors();
        classOutput = new float[classDim];

        embedder = new Interpreter(embedFile);
        embedder.ResizeInputTensor(0, new int[] { 1, 15, 63 });
        embedder.AllocateTensors();
        embedOutput = new float[embedDim];
    }

    public float[] EvaluateClass(float[][] frames)
    {
        for (int t = 0; t < 15; t++)
            for (int i = 0; i < 63; i++)
                input[0, t, i] = frames[t][i];

        classifier.SetInputTensorData(0, input);
        classifier.Invoke();
        classifier.GetOutputTensorData(0, classOutput);
        return classOutput;
    }

    public float[] EvaluateEmbed(float[][] frames)
    {
        for (int t = 0; t < 15; t++)
            for (int i = 0; i < 63; i++)
                input[0, t, i] = frames[t][i];

        embedder.SetInputTensorData(0, input);
        embedder.Invoke();
        embedder.GetOutputTensorData(0, embedOutput);
        return embedOutput;
    }
}
