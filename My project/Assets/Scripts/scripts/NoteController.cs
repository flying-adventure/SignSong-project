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
    public TMP_FontAsset noteFont; // assigned via Inspector or loaded from Resources
    [Header("Editor Preview")]
    [Tooltip("Inspector에서 에디터 미리보기를 위해 사용할 텍스트입니다. 비어있으면 런타임 값이 사용됩니다.")]
    public string previewText;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        label = GetComponentInChildren<TextMeshProUGUI>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 에디터에서 previewText가 설정되어 있으면 바로 라벨에 적용해 미리보기 가능
        if (!string.IsNullOrEmpty(previewText))
        {
            label = GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = previewText;
            }
        }
    }
#endif

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
        {
            label.text = data.word;
            if (noteFont == null)
            {
                // Try to load the provided TMP asset from Resources
                noteFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/April16thTTF-Promise SDF");
            }

            if (noteFont != null)
            {
                label.font = noteFont;
            }
        }

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