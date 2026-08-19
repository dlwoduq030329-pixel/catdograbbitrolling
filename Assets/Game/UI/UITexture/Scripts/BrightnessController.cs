using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BrightnessController : MonoBehaviour
{
    public static BrightnessController Instance;

    [SerializeField] private Image overlay;

    void Awake()
    {
        Instance = this;
    }

    public void SetBrightness(float value)
    {
        // value: 0.6 ~ 1.4
        // 1.0 = Á¤»ó
        float alpha = Mathf.Clamp01(1f - value);
        overlay.color = new Color(0, 0, 0, alpha);
    }
}
