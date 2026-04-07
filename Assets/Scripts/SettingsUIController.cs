using UnityEngine;
using TMPro;

public class SettingsUIController : MonoBehaviour
{
    [Header("Button Texts")]
    public TMP_Text hapticText;
    public TMP_Text bgmText;
    public TMP_Text effectSoundText;

    private bool isHapticHard = true;
    private bool isBgmOn = true;
    private bool isEffectSoundOn = true;

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
        isBgmOn = !isBgmOn;
        UpdateUI();
    }

    public void ToggleEffectSound()
    {
        isEffectSoundOn = !isEffectSoundOn;
        UpdateUI();
    }

    private void UpdateUI()
    {
        hapticText.text = isHapticHard ? "HARD" : "WEAK";
        bgmText.text = isBgmOn ? "ON" : "OFF";
        effectSoundText.text = isEffectSoundOn ? "ON" : "OFF";
    }
}