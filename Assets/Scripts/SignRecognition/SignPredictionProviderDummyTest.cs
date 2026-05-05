using UnityEngine;

public class SignPredictionProviderDummyTest : MonoBehaviour
{
    public SignPredictionProvider provider;

    private void Update()
    {
        if (provider == null)
        {
            return;
        }
        
        Vector3[] leftHand = new Vector3[21];
        Vector3[] rightHand = new Vector3[21];
        Vector3[] facePoints = new Vector3[5];

        facePoints[0] = new Vector3(0f, 0f, 0f);
        facePoints[1] = new Vector3(-0.1f, 0f, 0f);
        facePoints[2] = new Vector3(0.1f, 0f, 0f);
        facePoints[3] = new Vector3(-0.05f, -0.1f, 0f);
        facePoints[4] = new Vector3(0.05f, -0.1f, 0f);

        for(int i=0; i<21; i++)
        {
            leftHand[i] = new Vector3(-0.2f + i*0.001f, 0.1f, 0f);
            rightHand[i] = new Vector3(0.2f + i*0.001f, 0.1f, 0f);
        }
        provider.AddLandmarkFrame(leftHand, rightHand, facePoints);
    }
}
