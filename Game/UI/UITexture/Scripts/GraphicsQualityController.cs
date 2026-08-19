using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraphicsQualityController : MonoBehaviour
{
    const string OVERALL_KEY = "OverallQuality";
    const string RESOLUTION_KEY = "ResolutionQuality";
    const string TEXTURE_KEY = "TextureQuality";

    GraphicsSettingsRuntime runtime;
    bool suppressUserOverride;

    void Awake()
    {
        runtime = FindObjectOfType<GraphicsSettingsRuntime>();

        if (runtime == null)
            Debug.LogError("GraphicsSettingsRuntime not found.");
    }

    // ===== 전체 품질 =====
    public void OnOverallSelected(int index)
    {
        suppressUserOverride = true;

        runtime.SetEnum(OVERALL_KEY, index);
        var overall = (OverallQuality)index;

        if (overall != OverallQuality.User && !runtime.IsLoading)
        {
            var quality = ConvertOverallToQuality(overall);

            runtime.SetEnum(RESOLUTION_KEY, (int)quality);
            runtime.SetEnum(TEXTURE_KEY, (int)quality);
        }

        suppressUserOverride = false;
    }

    // ===== 해상도 =====
    public void OnResolutionSelected(int index)
    {
        runtime.SetEnum(RESOLUTION_KEY, index);
        CheckUserOverride();
    }

    // ===== 텍스처 =====
    public void OnTextureSelected(int index)
    {
        runtime.SetEnum(TEXTURE_KEY, index);
        CheckUserOverride();
    }

    void CheckUserOverride()
    {
        if (suppressUserOverride)
            return;

        int overall = runtime.GetEnum(OVERALL_KEY);

        if ((OverallQuality)overall != OverallQuality.User)
        {
            runtime.SetEnum(OVERALL_KEY, (int)OverallQuality.User);
        }
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
    }
}
