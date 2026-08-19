using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioSettingsApplier : MonoBehaviour
{
    [SerializeField] private AudioSettingsData audioData;
    [SerializeField] private AudioMixer audioMixer;

    const string MASTER = "MasterVolume";
    const string BGM = "BGMVolume";
    const string SFX = "SFXVolume";

    void Awake()
    {
        ApplyAll();
    }

    void OnEnable()
    {
        ApplyAll();
    }

    public void ApplyAll()
    {
        Apply(MASTER, audioData.masterVolume, audioData.masterMute);
        Apply(BGM, audioData.bgmVolume, audioData.bgmMute);
        Apply(SFX, audioData.sfxVolume, audioData.sfxMute);
    }

    void Apply(string param, float volume, bool mute)
    {
        if (mute)
        {
            audioMixer.SetFloat(param, -80f);
            return;
        }

        audioMixer.SetFloat(param, LinearToDecibel(volume));
    }

    float LinearToDecibel(float value)
    {
        if (value <= 0.0001f)
            return -80f;

        return Mathf.Log10(value) * 20f;
    }
}
