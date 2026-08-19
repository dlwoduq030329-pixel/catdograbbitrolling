using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSliderBinder : MonoBehaviour
{
    [SerializeField] private AudioSettingsData audioData;

    enum Target { Master, BGM, SFX }
    [SerializeField] private Target target;

    [Header("UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text percentText;
    private void OnEnable()
    {
        float value = GetVolume();

        slider.SetValueWithoutNotify(value);
        UpdateText(value);
    }
    void Start()
    {
        slider.minValue = 0f;
        slider.maxValue = 1f;

        float value = GetVolume();
        slider.value = value;
        UpdateText(value);

        slider.onValueChanged.AddListener(OnChanged);
    }

    void OnChanged(float value)
    {
        SetVolume(value);
        UpdateText(value);

        FindObjectOfType<AudioSettingsApplier>()?.ApplyAll();
    }

    float GetVolume()
    {
        return target switch
        {
            Target.Master => audioData.masterVolume,
            Target.BGM => audioData.bgmVolume,
            Target.SFX => audioData.sfxVolume,
            _ => 1f
        };
    }

    void SetVolume(float value)
    {
        switch (target)
        {
            case Target.Master: audioData.masterVolume = value; break;
            case Target.BGM: audioData.bgmVolume = value; break;
            case Target.SFX: audioData.sfxVolume = value; break;
        }
    }

    void UpdateText(float value)
    {
        percentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
