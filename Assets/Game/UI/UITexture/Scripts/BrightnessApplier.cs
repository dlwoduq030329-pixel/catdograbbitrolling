using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrightnessApplier : MonoBehaviour
{
    [SerializeField] private GraphicsSettingsData graphicsData;

    void Awake()
    {
        Apply();
    }

    void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        // 기존 Brightness 적용 로직 호출
        GraphicsApplier.Apply("Brightness", graphicsData.brightness);
    }
}
