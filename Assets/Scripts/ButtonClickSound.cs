using UnityEngine;

public class ButtonClickSound : MonoBehaviour
{
    public void PlayClickSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayClick();
        }
    }
}