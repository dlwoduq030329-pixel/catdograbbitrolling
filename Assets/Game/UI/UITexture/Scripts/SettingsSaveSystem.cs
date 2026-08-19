using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsSaveSystem : MonoBehaviour
{
    private const string GRAPHICS_KEY = "GRAPHICS_SETTINGS";

    public static void Save(GraphicsSettingsSaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(GRAPHICS_KEY, json);
        PlayerPrefs.Save();
    }

    public static GraphicsSettingsSaveData Load()
    {
        if (!PlayerPrefs.HasKey(GRAPHICS_KEY))
            return null;

        string json = PlayerPrefs.GetString(GRAPHICS_KEY);
        return JsonUtility.FromJson<GraphicsSettingsSaveData>(json);
    }
}
