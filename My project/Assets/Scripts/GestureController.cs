using UnityEngine;

public class GestureController : MonoBehaviour
{
    public HandLandmarkProvider landmarkProvider;
    public SequenceBuilder sequenceBuilder;
    public TFLiteClassifier classifier;
    public OODChecker oodChecker;
    public GestureJudge judge;

    public string[] classes;

    void Update()
    {
        if (landmarkProvider.HasResult)
        {
            float[] frame = sequenceBuilder.Normalize(landmarkProvider.CurrentLandmarks);
            sequenceBuilder.AddFrame(frame);

            if (sequenceBuilder.Ready())
            {
                float[][] seq = sequenceBuilder.Sequence.ToArray();

                float[] prob = classifier.EvaluateClass(seq);
                int idx = oodChecker.Check(prob);

                float[] embed = classifier.EvaluateEmbed(seq);
                bool ok = oodChecker.DistanceOK(embed, idx);

                if (!ok || idx < 0)
                    judge.Judge("NONE");
                else
                    judge.Judge(classes[idx]);
            }
        }
    }
}
