using UnityEngine;

public class WebCamProbe : MonoBehaviour
{
    void Start()
    {
        var devs = WebCamTexture.devices;
        Debug.Log($"[WebCamProbe] devices={devs.Length}");
        for (int i = 0; i < devs.Length; i++)
            Debug.Log($"[WebCamProbe] #{i} name={devs[i].name} front={devs[i].isFrontFacing}");
    }
}
