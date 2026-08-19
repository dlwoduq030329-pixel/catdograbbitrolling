using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BackgroundAudioToggle : MonoBehaviour
{
    const string KEY = "PlayInBackgroundAudio";

    [SerializeField] TMP_Dropdown dropdown;
    [SerializeField, Range(0f, 1f)]
    float disabledAlpha = 0.6f;

    GraphicsSettingsRuntime runtime;

    void Awake()
    {
        runtime = FindObjectOfType<GraphicsSettingsRuntime>();

        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();
    }

    void Start()
    {
        if (runtime == null || dropdown == null)
            return;
        int value = runtime.GetEnum(KEY);
        dropdown.value = value;
        dropdown.RefreshShownValue();

        ApplyTextAlpha(value);

        dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    void OnDestroy()
    {
        if (dropdown != null)
            dropdown.onValueChanged.RemoveListener(OnValueChanged);
    }

    void OnValueChanged(int value)
    {
        if (runtime == null)
            return;

       runtime.SetEnum(KEY, value);
        ApplyTextAlpha(value);
    }

    void ApplyTextAlpha(int value)
    {
        if (dropdown.captionText == null)
            return;

        Color c = dropdown.captionText.color;
        c.a = (value == 1) ? 1f : disabledAlpha;
        dropdown.captionText.color = c;
    }
}
