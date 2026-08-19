using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraphicsSettingsRuntime : MonoBehaviour
{
    
    [SerializeField] private GraphicsSettingsConfig config;

    Dictionary<string, float> currentValues = new();
    Dictionary<string, int> enumValues = new();
    public bool IsLoading { get; private set; }
    public bool IsInitialized { get; private set; }
    void Awake()
    {
        //if (Instance != null)
        //{
        //    Destroy(gameObject);
        //    return;
        //}

        //Instance = this;
        //DontDestroyOnLoad(gameObject);

        IsInitialized = false;
        LoadOrDefault();
        IsInitialized = true;
        //IsLoading = false;
    }

    void LoadOrDefault()
    {
        var saved = SettingsSaveSystem.Load();

        // ===== Float 可记 =====
        foreach (var option in config.options)
        {
            float value = option.defaultValue;

            if (saved != null)
            {
                var found = saved.options.Find(o => o.optionId == option.optionId);
                if (found != null)
                    value = found.value;
            }

            currentValues[option.optionId] = value;
            GraphicsApplier.Apply(option.optionId, value);
        }

        // ===== Enum 可记 =====
        foreach (var enumOption in config.enumOptions)
        {
            int value = enumOption.defaultValue;

            if (saved != null)
            {
                var found = saved.enumOptions.Find(e => e.key == enumOption.key);
                if (found != null)
                    value = found.value;
            }

            enumValues[enumOption.key] = value;
            GraphicsApplier.ApplyEnum(enumOption.key, value);
        }
    }
    public void SetEnum(string key, int value)
    {
        if (IsLoading)
            return;

        enumValues[key] = value;
        GraphicsApplier.ApplyEnum(key, value);
        Save();
    }

    public int GetEnum(string key)
    {
        return enumValues.TryGetValue(key, out var v) ? v : 0;
    }
    public void SetValue(string optionId, float value)
    {
        currentValues[optionId] = value;
        GraphicsApplier.Apply(optionId, value);
        Save();
    }

    void Save()
    {
        var data = new GraphicsSettingsSaveData();

        foreach (var pair in currentValues)
        {
            data.options.Add(new OptionSaveData
            {
                optionId = pair.Key,
                value = pair.Value
            });
        }

        foreach (var pair in enumValues)
        {
            data.enumOptions.Add(new EnumSaveData
            {
                key = pair.Key,
                value = pair.Value
            });
        }

        SettingsSaveSystem.Save(data);
    }

    public float GetValue(string optionId)
    {
        return currentValues.TryGetValue(optionId, out var v) ? v : 0f;
    }
}
