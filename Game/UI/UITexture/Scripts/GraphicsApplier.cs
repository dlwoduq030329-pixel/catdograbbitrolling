/*using System.Collections;
using System.Collections.Generic;*/
using UnityEngine;
using UnityEngine.Audio;

public class GraphicsApplier
{
    static AudioMixer mixer;

    public static void Apply(string optionId, float value)
    {
        switch (optionId)
        {
            case "DisplayMode":
                ApplyDisplayMode((int)value);
                break;

            case "Brightness":
                ApplyBrightness(value);
                break;

            case "Quality":
                QualitySettings.SetQualityLevel((int)value);
                break;
            case "MasterVolume":
                SetVolume("MasterVolume", value);
                break;

            case "BGMVolume":
                SetVolume("BGMVolume", value);
                break;

            case "SFXVolume":
                SetVolume("SFXVolume", value);
                break;
        }
    }

    public static void ApplyEnum(string key, int value)
    {
       // Debug.Log($"[ApplyEnum] key={key}, value={value}");

        switch (key)
        {
            case "DisplayMode":
                ApplyDisplayMode(value);
                break;

            case "Quality":
                QualitySettings.SetQualityLevel(value);
                if (value != 0) return;
                break;
            case "ResolutionQuality":
                //Debug.Log("[ApplyEnum] Resolution case called");
                ApplyResolution(value);
                break;
            case "TextureQuality":
                ApplyTextureQuality(value);
                break;
        }
    }
    public static void SetAudioMixer(AudioMixer audioMixer)
    {
        mixer = audioMixer;
    }


    static void SetVolume(string param, float value)
    {
        if (mixer == null) return;

        if (value <= 0.0001f)
        {
            mixer.SetFloat(param, -80f);
            return;
        }

        mixer.SetFloat(param, Mathf.Log10(value) * 20f);
    }
    static void ApplyResolution(int index)
    {
        switch (index)
        {
            case 0: // ����
                Screen.SetResolution(1280, 720, true);
                break;

            case 1: // ����
                Screen.SetResolution(1600, 900, true);
                break;

            case 2: // ����
                Screen.SetResolution(1920, 1080, true);
                break;

            case 3: // �ֻ�
                Screen.SetResolution(2560, 1440, true);
                break;

        }
    }
    static void ApplyTextureQuality(int index)
    {
        switch (index)
        {
            case 0: // ����
                QualitySettings.globalTextureMipmapLimit = 3; // 1/8
                break;

            case 1: // ����
                QualitySettings.globalTextureMipmapLimit = 2; // 1/4
                break;

            case 2: // ����
                QualitySettings.globalTextureMipmapLimit = 1; // 1/2
                break;

            case 3: // �ֻ�
                QualitySettings.globalTextureMipmapLimit = 0; // ����
                break;
        }
    }
    static void ApplyDisplayMode(int mode)
    {
        // 0 = Fullscreen, 1 = Windowed
        Screen.fullScreenMode = mode == 0
            ? FullScreenMode.ExclusiveFullScreen
            : FullScreenMode.Windowed;
    }

    static void ApplyBrightness(float value)
    {
        if (BrightnessController.Instance != null)
            BrightnessController.Instance.SetBrightness(value);
    }
}
