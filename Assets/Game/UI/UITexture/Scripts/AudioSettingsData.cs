using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Settings/Audio Settings")]
public class AudioSettingsData : ScriptableObject
{
    [Range(0, 1)] public float masterVolume = 1f;
    public bool masterMute;

    [Range(0, 1)] public float bgmVolume = 1f;
    public bool bgmMute;

    [Range(0, 1)] public float sfxVolume = 1f;
    public bool sfxMute;
}
