using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Song Data")]
    public AudioClip songClip;
    public TextAsset chartFile;

    [Header("Note UI")]
    public RectTransform noteArea;

    [Tooltip("0 = Left, 1 = Middle, 2 = Right")]
    public RectTransform[] laneSpawnTops;   // 3개
    public RectTransform[] laneHitLines;    // 3개

    public GameObject notePrefab;

    [Header("Note Duration")]
    public float defaultLastNoteDuration = 1.0f;

    [Header("Timing Offset")]
    public float globalOffset = 0f;

    [Header("Guide Video")]
    public GuideVideoPlayer guideVideoPlayer;

    private AudioSource audioSource;
    private List<NoteData> notes = new List<NoteData>();
    private int nextNoteIndex = 0;
    private bool songStarted = false;
    private bool endingTriggered = false;   //  노래 종료 중복 방지

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        LoadSongAndChart();
        StartSong();
    }

    void LoadSongAndChart()
    {
        if (songClip == null)
        {
            Debug.LogError("[GameManager] songClip is NOT assigned.");
        }
        else
        {
            audioSource.clip = songClip;
        }

        if (chartFile == null)
        {
            Debug.LogError("[GameManager] chartFile is NOT assigned.");
            return;
        }

        notes = ChartParser.Parse(chartFile.text)
                           .OrderBy(n => n.time)
                           .ToList();

        for (int i = 0; i < notes.Count; i++)
        {
            if (i < notes.Count - 1)
                notes[i].endTime = notes[i + 1].time;
            else
                notes[i].endTime = notes[i].time + defaultLastNoteDuration;
        }

        Debug.Log($"[GameManager] Parsed notes count: {notes.Count}");
    }

    void StartSong()
    {
        if (audioSource.clip == null)
        {
            Debug.LogError("[GameManager] No AudioClip to play.");
            return;
        }

        audioSource.time = 0f;
        audioSource.Play();
        songStarted = true;
        nextNoteIndex = 0;
        endingTriggered = false;
    }

    void Update()
    {
        if (!songStarted || audioSource.clip == null || notes.Count == 0)
            return;

        float songTime = audioSource.time + globalOffset;

        // 노트 스폰
        while (nextNoteIndex < notes.Count)
        {
            var note = notes[nextNoteIndex];

            if (note.time <= songTime)
            {
                SpawnNote(note);
                nextNoteIndex++;
            }
            else
            {
                break;
            }
        }

        //  여기서 노래 끝났는지 체크
        if (!endingTriggered && audioSource.time >= audioSource.clip.length)
        {
            endingTriggered = true;
            StartCoroutine(GoToResultAfterDelay());
        }
    }

    System.Collections.IEnumerator GoToResultAfterDelay()
    {
        //  2초 기다림
        yield return new WaitForSeconds(2f);

        //  result2 씬으로 이동
        SceneManager.LoadScene("result2");
    }

    void SpawnNote(NoteData data)
    {
        if (notePrefab == null)
        {
            Debug.LogError("[GameManager] notePrefab is null");
            return;
        }
        if (noteArea == null)
        {
            Debug.LogError("[GameManager] noteArea is null");
            return;
        }

        int lane = Mathf.Clamp(data.lane, 0, 2);

        if (laneSpawnTops == null || laneSpawnTops.Length <= lane || laneSpawnTops[lane] == null)
        {
            Debug.LogError($"[GameManager] laneSpawnTops[{lane}] 이 설정되지 않음");
            return;
        }

        if (laneHitLines == null || laneHitLines.Length <= lane || laneHitLines[lane] == null)
        {
            Debug.LogError($"[GameManager] laneHitLines[{lane}] 이 설정되지 않음");
            return;
        }

        RectTransform spawnTop = laneSpawnTops[lane];
        RectTransform hitLine = laneHitLines[lane];

        GameObject go = Instantiate(notePrefab, noteArea);
        RectTransform rect = go.GetComponent<RectTransform>();

        if (rect == null)
        {
            Debug.LogError("[GameManager] NotePrefab has NO RectTransform");
            return;
        }

        float duration = Mathf.Max(0.01f, data.endTime - data.time);

        var controller = go.GetComponent<NoteController>();
        if (controller == null)
        {
            Debug.LogError("[GameManager] NotePrefab에 NoteController 없음");
            return;
        }

        controller.Init(data, spawnTop, hitLine, duration);

        if (guideVideoPlayer != null)
        {
            guideVideoPlayer.PlayWord(data.word, duration);
        }
    }
}
