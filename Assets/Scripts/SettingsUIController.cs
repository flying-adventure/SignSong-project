using UnityEngine;
using TMPro;

public class SettingsUIController : MonoBehaviour
{
    [Header("Button Texts")]
    public TMP_Text hapticText;
    public TMP_Text bgmText;
    public TMP_Text effectSoundText;

    private bool isHapticHard = true;

    void Start()
    {
        UpdateUI();
    }

    public void ToggleHaptic()
    {
        isHapticHard = !isHapticHard;
        UpdateUI();
    }

    public void ToggleBGM()
    {
        if (BGMManager.Instance != null)
        {
            BGMManager.Instance.ToggleBGM();
        }

        UpdateUI();
    }

    public void ToggleEffectSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.ToggleSFX();
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        hapticText.text = isHapticHard ? "HARD" : "WEAK";

        if (BGMManager.Instance != null)
            bgmText.text = BGMManager.Instance.IsBgmOn() ? "ON" : "OFF";

        if (SoundManager.Instance != null)
            effectSoundText.text = SoundManager.Instance.IsSfxOn() ? "ON" : "OFF";
    }
}