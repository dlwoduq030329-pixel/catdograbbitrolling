using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기존 HUD의 ProfileImage를 변경하지 않고 런타임에 HP·보호막 원형 게이지를 덧붙인다.
/// HP는 초상화 안쪽 링, 보호막은 바깥쪽 링으로 표시하며 보호막이 없으면 외곽 링을 숨긴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerPortraitStatusView : MonoBehaviour
{
    private const string ProfileObjectName = "ProfileImage";
    private const string OverlayObjectName = "Battle Player Status Rings";

    [Header("원형 HUD 색상")]
    [SerializeField] private Color hpColor = new Color(0.2f, 0.9f, 0.32f, 1f);
    [SerializeField] private Color hpBackgroundColor = new Color(0.08f, 0.12f, 0.1f, 0.75f);
    [SerializeField] private Color shieldColor = new Color(0.15f, 0.72f, 1f, 1f);
    [SerializeField] private Color shieldBackgroundColor = new Color(0.05f, 0.18f, 0.28f, 0.7f);
    [SerializeField, Min(1f)] private float hpRingPadding = 8f;
    [SerializeField, Min(1f)] private float shieldRingPadding = 15f;

    private BattleHealth targetHealth;
    private RectTransform overlayRoot;
    private Image hpFill;
    private Image shieldRoot;
    private Image shieldFill;
    private Texture2D ringTexture;
    private Sprite ringSprite;
    private float nextAttachAttemptTime;

    public void Bind(BattleHealth health)
    {
        Unsubscribe();
        targetHealth = health;
        Subscribe();
        TryAttachToPortrait();
        Refresh();
    }

    private void Update()
    {
        if (overlayRoot != null || Time.unscaledTime < nextAttachAttemptTime)
        {
            return;
        }

        nextAttachAttemptTime = Time.unscaledTime + 0.5f;
        if (TryAttachToPortrait())
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (ringSprite != null)
        {
            Destroy(ringSprite);
        }

        if (ringTexture != null)
        {
            Destroy(ringTexture);
        }
    }

    private bool TryAttachToPortrait()
    {
        RectTransform portrait = FindProfileImage();
        if (portrait == null)
        {
            return false;
        }

        Transform existing = portrait.Find(OverlayObjectName);
        if (existing != null)
        {
            overlayRoot = existing as RectTransform;
            ResolveExistingImages();
            return overlayRoot != null;
        }

        EnsureRingSprite();
        overlayRoot = CreateRect(OverlayObjectName, portrait, Vector2.zero);
        Stretch(overlayRoot, Vector2.zero);

        CreateRing("HP Ring Background", overlayRoot, hpRingPadding, hpBackgroundColor, false);
        hpFill = CreateRing("HP Ring Fill", overlayRoot, hpRingPadding, hpColor, true);
        shieldRoot = CreateRing("Shield Ring Background", overlayRoot, shieldRingPadding, shieldBackgroundColor, false);
        shieldFill = CreateRing("Shield Ring Fill", overlayRoot, shieldRingPadding, shieldColor, true);
        return true;
    }

    private static RectTransform FindProfileImage()
    {
        RectTransform[] allRects = FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (RectTransform rect in allRects)
        {
            if (rect != null && rect.name == ProfileObjectName && rect.GetComponent<Image>() != null)
            {
                return rect;
            }
        }

        return null;
    }

    private Image CreateRing(
        string objectName,
        RectTransform parent,
        float padding,
        Color color,
        bool isFill)
    {
        RectTransform rect = CreateRect(objectName, parent, new Vector2(padding * 2f, padding * 2f));
        Stretch(rect, new Vector2(-padding, -padding));

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = ringSprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (isFill)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = false;
            image.fillAmount = 1f;
        }

        return image;
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 sizeDelta)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 offset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offset;
        rect.offsetMax = -offset;
    }

    private void EnsureRingSprite()
    {
        if (ringSprite != null)
        {
            return;
        }

        const int size = 128;
        const float outerRadius = 62f;
        const float innerRadius = 52f;
        ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Battle Runtime Portrait Ring",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outerAlpha = Mathf.Clamp01(outerRadius - distance + 1f);
                float innerAlpha = Mathf.Clamp01(distance - innerRadius + 1f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, outerAlpha * innerAlpha);
            }
        }

        ringTexture.SetPixels(pixels);
        ringTexture.Apply(false, true);
        ringSprite = Sprite.Create(
            ringTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        ringSprite.name = "Battle Runtime Portrait Ring Sprite";
    }

    private void ResolveExistingImages()
    {
        hpFill = FindImage("HP Ring Fill");
        shieldRoot = FindImage("Shield Ring Background");
        shieldFill = FindImage("Shield Ring Fill");
    }

    private Image FindImage(string childName)
    {
        Transform child = overlayRoot != null ? overlayRoot.Find(childName) : null;
        return child != null ? child.GetComponent<Image>() : null;
    }

    private void Refresh()
    {
        if (targetHealth == null)
        {
            if (hpFill != null)
            {
                hpFill.fillAmount = 0f;
            }
            SetShieldVisible(false);
            return;
        }

        if (hpFill != null)
        {
            hpFill.fillAmount = targetHealth.MaxHealth > 0f
                ? Mathf.Clamp01(targetHealth.CurrentHealth / targetHealth.MaxHealth)
                : 0f;
        }

        bool hasShield = targetHealth.CurrentShield > 0f;
        SetShieldVisible(hasShield);
        if (shieldFill != null)
        {
            // 현재 보호막에는 별도 최대치가 없으므로 존재 여부를 완전한 외곽 링으로 표시한다.
            shieldFill.fillAmount = hasShield ? 1f : 0f;
        }
    }

    private void SetShieldVisible(bool visible)
    {
        if (shieldRoot != null)
        {
            shieldRoot.gameObject.SetActive(visible);
        }
        if (shieldFill != null)
        {
            shieldFill.gameObject.SetActive(visible);
        }
    }

    private void Subscribe()
    {
        if (targetHealth == null)
        {
            return;
        }

        targetHealth.HealthChanged -= HandleHealthChanged;
        targetHealth.ShieldChanged -= HandleHealthChanged;
        targetHealth.Died -= HandleHealthChanged;
        targetHealth.HealthChanged += HandleHealthChanged;
        targetHealth.ShieldChanged += HandleHealthChanged;
        targetHealth.Died += HandleHealthChanged;
    }

    private void Unsubscribe()
    {
        if (targetHealth == null)
        {
            return;
        }

        targetHealth.HealthChanged -= HandleHealthChanged;
        targetHealth.ShieldChanged -= HandleHealthChanged;
        targetHealth.Died -= HandleHealthChanged;
    }

    private void HandleHealthChanged(BattleHealth health)
    {
        Refresh();
    }
}
