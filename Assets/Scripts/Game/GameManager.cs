using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Song Data")]
    public AudioClip songClip;      // mp3 파일 드래그
    public TextAsset chartFile;     // chart txt 드래그

    [Header("Note UI")]
    public RectTransform noteArea;      // NoteArea
    public RectTransform noteSpawnTop;  // NoteSpawnTop
    public RectTransform noteHitLine;   // NoteHitline
    public GameObject notePrefab;       // NotePrefab 프리팹
    public float noteTravelTime = 2f;   // 위→아래 이동 시간

    [Header("Timing Offset")]
    public float globalOffset = 0f;     // 전체 타이밍 미세 조정용

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

        notes = ChartParser.Parse(chartFile.text)
                           .OrderBy(n => n.time)
                           .ToList();

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

        // 노트 도착 시각(time)에 맞게 스폰
        // chart의 time = HitLine에 도착해야 하는 시각(초)
        while (nextNoteIndex < notes.Count)
        {
            var note = notes[nextNoteIndex];
            float spawnTime = note.time - noteTravelTime;

            if (spawnTime <= songTime)
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
    if (noteSpawnTop == null)
    {
        Debug.LogError("[GameManager] noteSpawnTop is null");
        return;
    }
    if (noteHitLine == null)
    {
        Debug.LogError("[GameManager] noteHitLine is null");
        return;
    }

    GameObject go = Instantiate(notePrefab, noteArea);
    if (go == null)
    {
        Debug.LogError("[GameManager] Instantiate returned null");
        return;
    }

    RectTransform rect = go.GetComponent<RectTransform>();
    if (rect == null)
    {
        Debug.LogError("[GameManager] NotePrefab has NO RectTransform (UI 오브젝트 아님)");
        return;
    }

    rect.position = noteSpawnTop.position;

    var controller = go.GetComponent<NoteController>();
    if (controller == null)
    {
        Debug.LogError("[GameManager] NotePrefab에 NoteController 컴포넌트가 없음");
        return;
    }

    controller.Init(data, noteSpawnTop, noteHitLine, noteTravelTime);
    }
}