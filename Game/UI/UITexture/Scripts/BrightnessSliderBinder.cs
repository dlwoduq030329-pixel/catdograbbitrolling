using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BrightnessSliderBinder : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private GraphicsSettingsData graphicsData;

    const float MIN = 0.6f;
    const float MAX = 1.4f;

    void Start()
    {
        slider.minValue = MIN;
        slider.maxValue = MAX;

        float value = graphicsData.brightness;
        slider.value = value;
        UpdateText(value);

        slider.onValueChanged.AddListener(OnChanged);
    }

    void OnChanged(float value)
    {
        graphicsData.brightness = value;
        UpdateText(value);

        FindObjectOfType<BrightnessApplier>()?.Apply();
    }

    void UpdateText(float value)
    {
        float percent = (value - MIN) / (MAX - MIN);
        percentText.text = $"{Mathf.RoundToInt(percent * 100f)}%";
    }
}
