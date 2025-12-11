using UnityEngine;
using TMPro;

public class NoteController : MonoBehaviour
{
    public NoteData Data { get; private set; }

    private RectTransform rect;
    private RectTransform startPoint;
    private RectTransform endPoint;
    private float travelTime;
    private float spawnTime;

    private TextMeshProUGUI label;
    private bool initialized = false;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        label = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Init(NoteData data,
                     RectTransform spawnTop,
                     RectTransform hitLine,
                     float travelTime)
    {
        Data = data;
        startPoint = spawnTop;
        endPoint = hitLine;
        this.travelTime = travelTime;

        spawnTime = Time.time;                 // 실제 게임 시간 기준
        if (rect == null)
            rect = GetComponent<RectTransform>();

        rect.position = startPoint.position;

        if (label != null)
            label.text = data.word;

        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;
        if (rect == null || startPoint == null || endPoint == null) return;

        float elapsed = Time.time - spawnTime;
        float t = elapsed / travelTime;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        rect.position = Vector3.Lerp(startPoint.position, endPoint.position, t);
    }
}