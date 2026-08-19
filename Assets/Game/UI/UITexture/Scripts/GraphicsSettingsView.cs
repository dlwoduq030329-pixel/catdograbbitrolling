/*using System.Collections;
using System.Collections.Generic;*/
using UnityEngine;

public class GraphicsSettingsView : MonoBehaviour
{
    [SerializeField] private GraphicsSettingsConfig config;

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        foreach (var option in config.options)
        {
            Debug.Log($"[Graphics Option] {option.displayName}");
            // 실제 프로젝트에서는
            // Slider / Dropdown / ButtonGroup 프리팹 생성
        }
    }

    public void OnValueChanged(string optionId, float value)
    {
        GraphicsApplier.Apply(optionId, value);
    }
}
