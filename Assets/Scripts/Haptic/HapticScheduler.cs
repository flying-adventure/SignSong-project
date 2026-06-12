using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HapticScheduler : MonoBehaviour
{
    [SerializeField] private GloveBluetoothSender sender;

    private List<HapticEvent> events = new();
    private int nextIndex   = 0;
    private bool isRunning  = false;
    private AudioSource audioSource;

    private const string CSV_FILENAME = "벚꽃엔딩_drum_motor_2.csv";

    void Awake()
    {
        if (sender == null)
            sender = GetComponent<GloveBluetoothSender>();
    }

    // GameManager.StartSong() 에서 호출
    public void StartSchedule(AudioSource source)
    {
        LoadCsv();

        if (events.Count == 0)
        {
            Debug.LogWarning("[HapticScheduler] 이벤트가 없어 스케줄러를 시작하지 않음");
            return;
        }

        audioSource = source;
        nextIndex   = 0;
        isRunning   = true;

        sender?.Connect();

        Debug.Log($"[HapticScheduler] 시작 — 이벤트 {events.Count}개 로드됨");
    }

    private void LoadCsv()
    {
        // StreamingAssets/Charts 우선 탐색, 없으면 프로젝트 루트
        string streamingPath = Path.Combine(Application.streamingAssetsPath, "Charts", CSV_FILENAME);
        string rootPath      = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CSV_FILENAME));

        string path = File.Exists(streamingPath) ? streamingPath : rootPath;
        events = HapticCsvParser.ParseFromFile(path);
    }

    void Update()
    {
        if (!isRunning || audioSource == null) return;

        float songTime = audioSource.time;

        while (nextIndex < events.Count)
        {
            var e = events[nextIndex];
            if (e.timeSec > songTime) break;

            FireEvent(e, songTime);
            nextIndex++;
        }
    }

    private void FireEvent(HapticEvent e, float songTime)
    {
        // SettingsScene의 Hard(1) / Weak(0) 설정 반영
        float multiplier = PlayerPrefs.GetInt("HapticStrength", 1) == 1 ? 1.0f : 0.5f;
        int m1 = Mathf.RoundToInt(e.motor1 * multiplier);
        int m2 = Mathf.RoundToInt(e.motor2 * multiplier);

        if (sender != null && sender.IsConnected)
            sender.Send(m1, m2, e.durationMs);

        // 실시간 터미널 출력
        Debug.Log($"[진동] {songTime:F2}s | {e.drumType,-7} | 왼손:{m1,3}  오른손:{m2,3} | {e.durationMs}ms");
    }

    public void StopSchedule()
    {
        isRunning = false;
    }

    void OnDestroy() => sender?.Disconnect();
}
