using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum QualityLevel
{
    Low,
    Medium,
    High,
    Ultra
}

public enum OverallQuality
{
    User,
    Low,
    Medium,
    High,
    Ultra
}

public class QualityButton : MonoBehaviour
{
    [SerializeField] string qualityKey = "Quality";
    [SerializeField] int qualityIndex;

    [SerializeField] private Image background;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    Button button;
    GraphicsSettingsRuntime runtime;

    void Awake()
    {
        runtime = FindObjectOfType<GraphicsSettingsRuntime>();
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    void Start()
    {
        StartCoroutine(DelayedRefresh());
    }

    IEnumerator DelayedRefresh()
    {
        // 1프레임 대기
        yield return null;

        while (!runtime.IsInitialized)
            yield return null;

        Refresh();
    }

    void OnClick()
    {
        runtime.SetEnum(qualityKey, qualityIndex);
        RefreshGroup();
    }

    public void Refresh()
    {
        int current = runtime.GetEnum(qualityKey);
        background.sprite = (current == qualityIndex)
            ? selectedSprite
            : normalSprite;
    }

    void RefreshGroup()
    {
        foreach (var btn in transform.parent.GetComponentsInChildren<QualityButton>())
            btn.Refresh();
    }
}
