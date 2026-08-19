using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraphicsQualityApplier : MonoBehaviour
{
    /*
    [SerializeField] private GraphicsSettingsData settings;

    const string OVERALL_KEY = "OverallQuality";
    const string RESOLUTION_KEY = "ResolutionQuality";
    const string TEXTURE_KEY = "TextureQuality";

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
        ApplyOverall();
        ApplyResolution();
        ApplyTexture();
    }

    void ApplyOverall()
    {
        var overall = (OverallQuality)settings.GetEnum(OVERALL_KEY);

        if (overall == OverallQuality.User)
            return;

        var q = ConvertOverallToQuality(overall);
        settings.SetEnum(RESOLUTION_KEY, (int)q);
        settings.SetEnum(TEXTURE_KEY, (int)q);
    }

    void ApplyResolution()
    {
        int qualityIndex = settings.GetEnum(RESOLUTION_KEY);
        QualitySettings.SetQualityLevel(qualityIndex, true);
    }

    void ApplyTexture()
    {
        int textureLevel = settings.GetEnum(TEXTURE_KEY);

        // Unity ����: 0 = Full, 1 = Half, 2 = Quarter ��
        QualitySettings.globalTextureMipmapLimit = textureLevel;
    }

    QualityLevel ConvertOverallToQuality(OverallQuality overall)
    {
        return overall switch
        {
            OverallQuality.Low => QualityLevel.Low,
            OverallQuality.Medium => QualityLevel.Medium,
            OverallQuality.High => QualityLevel.High,
            OverallQuality.Ultra => QualityLevel.Ultra,
            _ => QualityLevel.High
        };
    }*/
}
