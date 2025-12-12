using UnityEngine;

public class RhythmGameLoop : MonoBehaviour
{
    public AudioClock clock;
    public JudgementEngine engine;
    public ScoreManager score;
    public NoteLoader noteLoader;

    void OnEnable()
    {
        if (engine != null && score != null)
            engine.OnJudged += score.OnJudge;
    }

    void OnDisable()
    {
        if (engine != null && score != null)
            engine.OnJudged -= score.OnJudge;
    }

    void Start()
    {
        // 엔진 판정 이벤트 -> 점수 누적
        // engine.OnJudged += score.OnJudge;

        score.ResetScore();
        var notes = noteLoader.LoadNotes();
        engine.SetNotes(notes);
        
        clock.Play();
        Debug.Log("RhythmGameLoop started");
    }

    void Update()
    {
        float nowSec = clock.NowSec();
        engine.UpdateEngine(nowSec);
    }
}
