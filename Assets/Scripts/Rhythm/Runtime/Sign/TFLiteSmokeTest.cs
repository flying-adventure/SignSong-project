using System;
using System.IO;
using UnityEngine;
using TensorFlowLite;

public class TFLiteSmokeTest : MonoBehaviour
{
    public string modelRelPath = "SignModels/best_cnn_gru2_model_word_split.tflite";

    void Start()
    {
        try
        {
            var fullPath = Path.Combine(Application.streamingAssetsPath, modelRelPath);
            Debug.Log($"[TFLiteSmokeTest] fullPath = {fullPath}");

            var bytes = File.ReadAllBytes(fullPath);
            Debug.Log($"[TFLiteSmokeTest] bytes.Length = {bytes.Length}");

            // 파일 헤더 확인: tflite가 맞는지 대충 체크
            Debug.Log($"[TFLiteSmokeTest] head(8) = {BitConverter.ToString(bytes, 0, Math.Min(8, bytes.Length))}");

            // 핵심: options를 직접 만들어서 넘기기
            var options = new InterpreterOptions();
            options.threads = 2;

            Debug.Log("[TFLiteSmokeTest] creating Interpreter(with options)...");
            using var interpreter = new Interpreter(bytes, options);

            Debug.Log("[TFLiteSmokeTest] allocating tensors...");
            interpreter.AllocateTensors();

            Debug.Log("[TFLiteSmokeTest] OK!");

            Debug.Log($"inputs={interpreter.GetInputTensorCount()}, outputs={interpreter.GetOutputTensorCount()}");
            for (int i=0;i<interpreter.GetInputTensorCount();i++)
            {
                var t = interpreter.GetInputTensorInfo(i);
                Debug.Log($"IN[{i}] name={t.name} type={t.type} shape=[{string.Join(",", t.shape)}]");
            }
            for (int i=0;i<interpreter.GetOutputTensorCount();i++)
            {
                var t = interpreter.GetOutputTensorInfo(i);
                Debug.Log($"OUT[{i}] name={t.name} type={t.type} shape=[{string.Join(",", t.shape)}]");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[TFLiteSmokeTest] FAILED\n" + e);
        }
    }
}
