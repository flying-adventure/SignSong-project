using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public string songFolderName = "test"; // Resources/Songs/song01

    private AudioSource audioSource;
    private List<NoteData> notes;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        LoadSongAndChart();
        StartSong();
    }

    void LoadSongAndChart()
    {
        // 1) 오디오 클립 로드
        string audioPath = $"Songs/{songFolderName}/test"; // 확장자 빼고
        AudioClip clip = Resources.Load<AudioClip>(audioPath);
        if (clip == null)
        {
            Debug.LogError($"[GameManager] AudioClip not found at Resources/{audioPath}");
        }
        else
        {
            audioSource.clip = clip;
            Debug.Log($"[GameManager] Loaded AudioClip: {clip.name}");
        }

        // 2) 차트 텍스트 로드
        string chartPath = $"Songs/{songFolderName}/test_chart";
        TextAsset chartText = Resources.Load<TextAsset>(chartPath);
        if (chartText == null)
        {
            Debug.LogError($"[GameManager] Chart file not found at Resources/{chartPath}");
        }
        else
        {
            notes = ChartParser.Parse(chartText.text);
            Debug.Log($"[GameManager] Parsed notes count: {notes.Count}");
            foreach (var n in notes)
            {
                Debug.Log($"Note: time={n.time}, word={n.word}");
            }
        }
    }

    void StartSong()
    {
        if (audioSource.clip == null)
        {
            Debug.LogError("[GameManager] No AudioClip to play.");
            return;
        }

        audioSource.Play();
        Debug.Log("[GameManager] Song started!");
    }
}