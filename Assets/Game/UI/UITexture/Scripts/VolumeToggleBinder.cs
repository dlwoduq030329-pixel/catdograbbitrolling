using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VolumeToggleBinder : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private AudioSettingsData audioData;

    enum Target { Master, BGM, SFX }
    [SerializeField] private Target target;

    TMP_Text labelText;

    void Start()
    {
        labelText = dropdown.captionText;

        dropdown.value = GetMute() ? 1 : 0;
        UpdateTextAlpha(dropdown.value);

        dropdown.onValueChanged.AddListener(OnChanged);
    }

    void OnChanged(int value)
    {
        SetMute(value == 1);
        UpdateTextAlpha(value);

        FindObjectOfType<AudioSettingsApplier>()?.ApplyAll();
    }

    bool GetMute()
    {
        return target switch
        {
            Target.Master => audioData.masterMute,
            Target.BGM => audioData.bgmMute,
            Target.SFX => audioData.sfxMute,
            _ => false
        };
    }

    void SetMute(bool mute)
    {
        switch (target)
        {
            case Target.Master: audioData.masterMute = mute; break;
            case Target.BGM: audioData.bgmMute = mute; break;
            case Target.SFX: audioData.sfxMute = mute; break;
        }
    }

    void UpdateTextAlpha(int value)
    {
        if (labelText == null) return;

        Color c = labelText.color;
        c.a = value == 1 ? 0.6f : 1f;
        labelText.color = c;
    }
}
