using UnityEngine;
using UnityEngine.UI;

public class CameraFeed : MonoBehaviour
{
    public RawImage rawImage;
    private WebCamTexture camTexture;

    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i].isFrontFacing)
            {
                camTexture = new WebCamTexture(devices[i].name);
                break;
            }
        }

        if (camTexture == null)
        {
            Debug.LogError("전면 카메라를 찾을 수 없습니다.");
            return;
        }

        rawImage.texture = camTexture;

        camTexture.Play();
    }
}
