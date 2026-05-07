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
    public RectTransform[] laneSpawnTops;
    public RectTransform[] laneHitLines;

    public GameObject notePrefab;

    [Header("Timing Offset")]
    public float globalOffset = 0f;

    [Header("Note Travel")]
    public float noteTravelTime = 5.0f;

    private AudioSource audioSource;
    private List<NoteData> notes = new List<NoteData>();
    private int nextNoteIndex = 0;
    private bool songStarted = false;
    private bool endingTriggered = false;

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
        endingTriggered = false;
    }

    void Update()
    {
        if (!songStarted || audioSource.clip == null || notes.Count == 0)
            return;

        float songTime = audioSource.time + globalOffset;

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

        if (!endingTriggered && audioSource.time >= audioSource.clip.length)
        {
            endingTriggered = true;
            StartCoroutine(GoToResultAfterDelay());
        }
    }

    System.Collections.IEnumerator GoToResultAfterDelay()
    {
        yield return new WaitForSeconds(2f);
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

        var controller = go.GetComponent<NoteController>();
        if (controller == null)
        {
            Debug.LogError("[GameManager] NotePrefab에 NoteController 없음");
            return;
        }

        controller.Init(data, spawnTop, hitLine, noteTravelTime);
    }
}