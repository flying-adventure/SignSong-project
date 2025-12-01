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

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        label = GetComponentInChildren<TextMeshProUGUI>();
    }

    // 노트 한 개 초기화
    public void Init(NoteData data,
                     RectTransform spawnTop,
                     RectTransform hitLine,
                     float travelTime)
    {
        Data = data;
        startPoint = spawnTop;
        endPoint = hitLine;
        this.travelTime = travelTime;

        spawnTime = Time.time;                 // 생성된 시점
        rect.position = startPoint.position;   // 시작 위치

        if (label != null)
            label.text = data.word;            // 단어 표시
    }

    void Update()
    {
        float t = (Time.time - spawnTime) / travelTime;

        if (t >= 1f)
        {
            // 히트라인 지나가면 제거 (나중에 Miss 처리로 확장 가능)
            Destroy(gameObject);
            return;
        }

        // 위 → 아래로 부드럽게 이동
        rect.position = Vector3.Lerp(startPoint.position, endPoint.position, t);
    }
}