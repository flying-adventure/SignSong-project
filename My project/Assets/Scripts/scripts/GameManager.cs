using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    public float defaultLastNoteDuration = 1.0f; // 마지막 단어 길이 기본값

    [Header("Timing Offset")]
    public float globalOffset = 0f;

    [Header("Guide Video")]
    public GuideVideoPlayer guideVideoPlayer;

    private AudioSource audioSource;
    private List<NoteData> notes = new List<NoteData>();
    private int nextNoteIndex = 0;
    private bool songStarted = false;

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

        // 1) CSV 파싱
        notes = ChartParser.Parse(chartFile.text)
                           .OrderBy(n => n.time)
                           .ToList();

        // 2) 각 노트의 endTime 채우기 (다음 노트의 time)
        for (int i = 0; i < notes.Count; i++)
        {
            if (i < notes.Count - 1)
            {
                notes[i].endTime = notes[i + 1].time;
            }
            else
            {
                // 마지막 노트는 기본 길이 사용
                notes[i].endTime = notes[i].time + defaultLastNoteDuration;
            }
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
    }

    void Update()
    {
        if (!songStarted || audioSource.clip == null || notes.Count == 0)
            return;

        float songTime = audioSource.time + globalOffset;

        // ✅ 이제 노트의 time을 "시작시간"으로 사용:
        // songTime이 해당 time을 넘었을 때 Spawn
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
            Debug.LogError($"[GameManager] laneSpawnTops[{lane}] 이(가) 설정되지 않음");
            return;
        }
        if (laneHitLines == null || laneHitLines.Length <= lane || laneHitLines[lane] == null)
        {
            Debug.LogError($"[GameManager] laneHitLines[{lane}] 이(가) 설정되지 않음");
            return;
        }

        RectTransform spawnTop = laneSpawnTops[lane];
        RectTransform hitLine  = laneHitLines[lane];

        GameObject go = Instantiate(notePrefab, noteArea);
        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
        {
            Debug.LogError("[GameManager] NotePrefab has NO RectTransform");
            return;
        }

        // 🔥 이 노트가 차지해야 하는 시간 (예: 16.14 ~ 17.18)
        float duration = Mathf.Max(0.01f, data.endTime - data.time);

        var controller = go.GetComponent<NoteController>();
        if (controller == null)
        {
            Debug.LogError("[GameManager] NotePrefab에 NoteController 없음");
            return;
        }

        controller.Init(data, spawnTop, hitLine, duration);

        // 🔥 가이드 영상도 같은 duration 안에 끝나도록 재생
        if (guideVideoPlayer != null)
        {
            guideVideoPlayer.PlayWord(data.word, duration);
        }
    }
}