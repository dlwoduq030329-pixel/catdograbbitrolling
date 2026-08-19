using System.IO;
using UnityEngine;

public class UIConfigManager : MonoBehaviour
{
    public static UIConfigManager Instance { get; private set; }

    public UIConfigData Config { get; private set; }

    private string configPath;

    private void Awake()
    {
        // 씬에 하나만 존재하도록
        Instance = this;

        configPath = Path.Combine(Application.persistentDataPath, "ui_config.json");
        LoadConfig();
    }

    private void LoadConfig()
    {
        if (File.Exists(configPath))
        {
            string json = File.ReadAllText(configPath);
            Config = JsonUtility.FromJson<UIConfigData>(json);
        }
        else
        {
            Config = new UIConfigData();
            SaveConfig();
        }
    }

    public void SaveConfig()
    {
        string json = JsonUtility.ToJson(Config, true);
        File.WriteAllText(configPath, json);
    }

    public void SetNickname(string nickname)
    {
        Config.nickname = nickname;
        SaveConfig();
    }
}
