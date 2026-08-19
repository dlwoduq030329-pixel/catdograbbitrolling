using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DisplayModeDropdownBinder : MonoBehaviour
{

    [SerializeField] private TMP_Dropdown dropdown;

    GraphicsSettingsRuntime runtime;

    void Awake()
    {
        runtime = FindObjectOfType<GraphicsSettingsRuntime>();

        if (runtime == null)
        {
            Debug.LogError("GraphicsSettingsRuntime not found in scene.");
        }
    }

    void Start()
    {
        dropdown.value = (int)runtime.GetValue("DisplayMode");
        dropdown.onValueChanged.AddListener(OnChanged);
    }

    void OnChanged(int value)
    {
        runtime.SetValue("DisplayMode", value);
    }

}
