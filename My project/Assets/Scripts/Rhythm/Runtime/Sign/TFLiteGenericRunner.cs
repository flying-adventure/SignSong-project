using System;
using System.IO;
using UnityEngine;
using TensorFlowLite;

/// <summary>
/// Generic TFLite runner that supports variable feature dims and output sizes.
/// SeqLen is fixed to 15 to match training.
/// </summary>
public sealed class TFLiteGenericRunner : IDisposable
{
    private Interpreter interpreter;
    private float[] input;
    private float[] output;

    public const int SeqLen = 15;

    private int featDim = 0;
    private int numOutputs = 0;

    public bool IsReady => interpreter != null;

    public void LoadFromStreamingAssets(string relPath, int threads, int featDim, int numOutputs)
    {
        Dispose();

        this.featDim = featDim;
        this.numOutputs = numOutputs;

        var fullPath = Path.Combine(Application.streamingAssetsPath, relPath);
        var bytes = File.ReadAllBytes(fullPath);

        var options = new InterpreterOptions();
        options.threads = threads;

        interpreter = new Interpreter(bytes, options);
        interpreter.AllocateTensors();

        input = new float[1 * SeqLen * featDim];
        output = new float[1 * numOutputs];
    }

    /// <summary>
    /// sequence: [SeqLen, featDim]
    /// returns: float[numOutputs]
    /// </summary>
    public float[] Run(float[,] sequence)
    {
        if (!IsReady) throw new InvalidOperationException("Interpreter not loaded.");

        int k = 0;
        for (int t = 0; t < SeqLen; t++)
            for (int d = 0; d < featDim; d++)
                input[k++] = sequence[t, d];

        interpreter.SetInputTensorData(0, input);
        interpreter.Invoke();
        interpreter.GetOutputTensorData(0, output);

        return output;
    }

    public void Dispose()
    {
        interpreter?.Dispose();
        interpreter = null;
        input = null;
        output = null;
    }
}
