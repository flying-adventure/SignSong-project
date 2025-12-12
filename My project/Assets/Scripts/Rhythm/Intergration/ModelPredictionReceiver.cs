using System.Collections.Concurrent;

public class ModelPredictionReceiver
{
    private readonly ConcurrentQueue<Prediction> _q = new();

    public void Enqueue(Prediction p) => _q.Enqueue(p);

    public bool TryDequeue(out Prediction p) => _q.TryDequeue(out p);
}