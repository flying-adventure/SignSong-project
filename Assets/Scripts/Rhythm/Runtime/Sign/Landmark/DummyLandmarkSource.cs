using UnityEngine;

public class DummyLandmarkSource : MonoBehaviour, ILandmarkSource
{
    public bool enableDummy = false;   // 켜면 랜드마크가 "있는 척"함
    public bool HasAnyHand => enableDummy;
    public bool HasFace => enableDummy;

     private float t;

    // 데모용: 141차원 벡터 생성
    public float[] GetFeature141()
    {
        t += Time.deltaTime;
        var feat = new float[TFLiteSignRunner.FeatDim];
        for (int i = 0; i < feat.Length; i++)
            feat[i] = Mathf.Sin(t * 2f + i * 0.01f);
        return feat;
    }

    public bool TryGet(out Vector3[] left21, out Vector3[] right21, out Vector3[] face5)
    {
        if (!enableDummy)
        {
            left21 = null; right21 = null; face5 = null;
            return false;
        }

        left21 = new Vector3[21];
        right21 = new Vector3[21];
        face5 = new Vector3[5];

        // 아주 단순한 더미(0~1 범위라고 가정)
        for (int i = 0; i < 21; i++)
        {
            left21[i] = new Vector3(0.3f, 0.4f, 0);
            right21[i] = new Vector3(0.7f, 0.4f, 0);
        }
        face5[0] = new Vector3(0.5f, 0.5f, 0); // nose
        face5[1] = new Vector3(0.45f, 0.5f, 0);
        face5[2] = new Vector3(0.55f, 0.5f, 0);
        face5[3] = new Vector3(0.48f, 0.55f, 0);
        face5[4] = new Vector3(0.52f, 0.55f, 0);

        return true;
    }
}