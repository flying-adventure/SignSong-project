using System;
using System.IO;
using UnityEngine;
using TensorFlowLite;

public sealed class TFLiteSignRunner : IDisposable
{
    private Interpreter interpreter;
    private float[] input;   // size = 1*15*141
    private float[] output;  // size = 1*15

    public const int SeqLen = 15;
    public const int FeatDim = 141;
    public const int NumClasses = 15;

    public bool IsReady => interpreter != null;

    public void LoadFromStreamingAssets(string relPath, int threads = 2)
    {
        Dispose();

        var fullPath = Path.Combine(Application.streamingAssetsPath, relPath);
        var bytes = File.ReadAllBytes(fullPath);

        var options = new InterpreterOptions();
        options.threads = threads;

        interpreter = new Interpreter(bytes, options);
        interpreter.AllocateTensors();

        input  = new float[1 * SeqLen * FeatDim];
        output = new float[1 * NumClasses];
    }

    /// sequence: [15,141]
    public float[] Run(float[,] sequence)
    {
        if (!IsReady) throw new InvalidOperationException("Interpreter not loaded.");

        // flatten: [1,15,141]
        int k = 0;
        for (int t = 0; t < SeqLen; t++)
            for (int d = 0; d < FeatDim; d++)
                input[k++] = sequence[t, d];

        interpreter.SetInputTensorData(0, input);
        interpreter.Invoke();
        interpreter.GetOutputTensorData(0, output);

        return output; // length 15
    }

    public void Dispose()
    {
        interpreter?.Dispose();
        interpreter = null;
        input = null;
        output = null;
    }
}