using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 기존 HUD의 ProfileImage를 Scene에서 찾아 HP·보호막 원형 게이지를 런타임에 생성하고 연결한다.
/// Player BattleHealth의 체력·보호막·사망 이벤트를 구독하며, HP는 안쪽 링의 부드러운 증감으로,
/// 보호막은 바깥쪽 링의 표시 여부로 나타낸다. 새 Player 초상화 프리팹이 준비되면 이 런타임 생성 구현은
/// 제거하고 Inspector에 직접 연결된 이미지에 BattleHealth 데이터만 전달하는 View로 새로 작성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerPortraitStatusView : MonoBehaviour
{
    private const string ProfileObjectName = "ProfileImage";
    private const string OverlayObjectName = "Battle Player Status Rings";

    [Header("원형 HUD 색상")]
    [FormerlySerializedAs("hpColor")]
    [SerializeField] private Color healthRingColor = new Color(0.2f, 0.9f, 0.32f, 1f);
    [FormerlySerializedAs("hpBackgroundColor")]
    [SerializeField] private Color healthRingBackgroundColor = new Color(0.08f, 0.12f, 0.1f, 0.75f);
    [FormerlySerializedAs("shieldColor")]
    [SerializeField] private Color shieldRingColor = new Color(0.15f, 0.72f, 1f, 1f);
    [FormerlySerializedAs("shieldBackgroundColor")]
    [SerializeField] private Color shieldRingBackgroundColor = new Color(0.05f, 0.18f, 0.28f, 0.7f);
    [FormerlySerializedAs("hpRingPadding")]
    [SerializeField, Min(1f)] private float healthRingPadding = 8f;
    [FormerlySerializedAs("shieldRingPadding")]
    [SerializeField, Min(1f)] private float shieldRingPadding = 15f;

    [Header("체력 변화 연출")]
    [InspectorName("피해 감소 속도 (비율/초)")]
    [Tooltip("1이면 최대 체력 전체를 1초에 걸쳐 감소시킵니다.")]
    [SerializeField, Min(0.01f)] private float damageDecreaseSpeed = 1.5f;
    [InspectorName("회복 증가 속도 (비율/초)")]
    [Tooltip("회복은 피해보다 빠르게 보이도록 별도로 조절할 수 있습니다.")]
    [SerializeField, Min(0.01f)] private float healingIncreaseSpeed = 3f;

    private BattleHealth observedPlayerHealth;
    private RectTransform statusRingContainer;
    private Image healthFillRing;
    private Image shieldBackgroundRing;
    private Image shieldFillRing;
    private Texture2D runtimeRingTexture;
    private Sprite runtimeRingSprite;
    private float nextPortraitSearchTime;
    private float targetHealthRatio;
    private float displayedHealthRatio;
    private bool hasInitializedHealthRatio;

    /// <summary>런타임 자동 생성 시 BattleGameManager의 Inspector 값을 전달받는다.</summary>
    public void ConfigureHealthAnimationSpeeds(float decreaseSpeed, float increaseSpeed)
    {
        damageDecreaseSpeed = Mathf.Max(0.01f, decreaseSpeed);
        healingIncreaseSpeed = Mathf.Max(0.01f, increaseSpeed);
    }

    /// <summary>
    /// 표시할 Player BattleHealth를 교체한다. 이전 Player의 이벤트 구독을 해제하고 새 Player를 구독한 뒤,
    /// 초상화 링을 찾거나 생성하고 현재 체력·보호막 상태를 표시한다.
    /// </summary>
    public void BindPlayerHealth(BattleHealth playerHealth)
    {
        UnsubscribeFromPlayerHealth();
        observedPlayerHealth = playerHealth;
        SubscribeToPlayerHealth();
        TryCreateOrReusePortraitRings();
        RefreshHealthAndShieldTargets();
    }

    /// <summary>
    /// 표시 중인 HP 비율을 목표 체력 비율로 이동시킨다. 피해는 damageDecreaseSpeed, 회복은
    /// healingIncreaseSpeed를 사용한다. 초상화가 늦게 생성된 경우에는 0.5초 간격으로 다시 찾아 연결한다.
    /// </summary>
    private void Update()
    {
        if (hasInitializedHealthRatio && healthFillRing != null)
        {
            float healthChangeSpeed = targetHealthRatio < displayedHealthRatio
                ? damageDecreaseSpeed
                : healingIncreaseSpeed;
            displayedHealthRatio = Mathf.MoveTowards(
                displayedHealthRatio,
                targetHealthRatio,
                healthChangeSpeed * Time.unscaledDeltaTime);
            healthFillRing.fillAmount = displayedHealthRatio;
        }

        if (statusRingContainer != null || Time.unscaledTime < nextPortraitSearchTime)
        {
            return;
        }

        nextPortraitSearchTime = Time.unscaledTime + 0.5f;
        if (TryCreateOrReusePortraitRings())
        {
            RefreshHealthAndShieldTargets();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromPlayerHealth();
    }

    private void OnEnable()
    {
        SubscribeToPlayerHealth();
        RefreshHealthAndShieldTargets();
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayerHealth();
        if (runtimeRingSprite != null)
        {
            Destroy(runtimeRingSprite);
        }

        if (runtimeRingTexture != null)
        {
            Destroy(runtimeRingTexture);
        }
    }

    /// <summary>
    /// Scene의 ProfileImage를 찾고 기존 상태 링이 있으면 참조를 복구한다. 없으면 원형 Sprite와
    /// HP 배경/채움, 보호막 배경/채움 오브젝트를 런타임 생성한다.
    /// </summary>
    private bool TryCreateOrReusePortraitRings()
    {
        RectTransform portrait = FindProfileImage();
        if (portrait == null)
        {
            return false;
        }

        Transform existing = portrait.Find(OverlayObjectName);
        if (existing != null)
        {
            statusRingContainer = existing as RectTransform;
            ResolveExistingImages();
            return statusRingContainer != null;
        }

        EnsureRuntimeRingSprite();
        statusRingContainer = CreateRectTransform(OverlayObjectName, portrait, Vector2.zero);
        StretchToParent(statusRingContainer, Vector2.zero);

        CreateRing("HP Ring Background", statusRingContainer, healthRingPadding, healthRingBackgroundColor, false);
        healthFillRing = CreateRing("HP Ring Fill", statusRingContainer, healthRingPadding, healthRingColor, true);
        shieldBackgroundRing = CreateRing("Shield Ring Background", statusRingContainer, shieldRingPadding, shieldRingBackgroundColor, false);
        shieldFillRing = CreateRing("Shield Ring Fill", statusRingContainer, shieldRingPadding, shieldRingColor, true);
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
        RectTransform rect = CreateRectTransform(objectName, parent, new Vector2(padding * 2f, padding * 2f));
        StretchToParent(rect, new Vector2(-padding, -padding));

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = runtimeRingSprite;
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

    private static RectTransform CreateRectTransform(string objectName, Transform parent, Vector2 sizeDelta)
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

    private static void StretchToParent(RectTransform rect, Vector2 offset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offset;
        rect.offsetMax = -offset;
    }

    private void EnsureRuntimeRingSprite()
    {
        if (runtimeRingSprite != null)
        {
            return;
        }

        const int size = 128;
        const float outerRadius = 62f;
        const float innerRadius = 52f;
        runtimeRingTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
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

        runtimeRingTexture.SetPixels(pixels);
        runtimeRingTexture.Apply(false, true);
        runtimeRingSprite = Sprite.Create(
            runtimeRingTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        runtimeRingSprite.name = "Battle Runtime Portrait Ring Sprite";
    }

    private void ResolveExistingImages()
    {
        healthFillRing = FindRingImage("HP Ring Fill");
        shieldBackgroundRing = FindRingImage("Shield Ring Background");
        shieldFillRing = FindRingImage("Shield Ring Fill");
    }

    private Image FindRingImage(string childName)
    {
        Transform child = statusRingContainer != null ? statusRingContainer.Find(childName) : null;
        return child != null ? child.GetComponent<Image>() : null;
    }

    /// <summary>현재 Player 체력 비율을 목표값으로 갱신하고 보호막 링 표시 여부를 현재 보호막 수치와 맞춘다.</summary>
    private void RefreshHealthAndShieldTargets()
    {
        if (observedPlayerHealth == null)
        {
            targetHealthRatio = 0f;
            displayedHealthRatio = 0f;
            hasInitializedHealthRatio = true;
            if (healthFillRing != null)
            {
                healthFillRing.fillAmount = 0f;
            }
            SetShieldVisible(false);
            return;
        }

        targetHealthRatio = observedPlayerHealth.MaxHealth > 0f
            ? Mathf.Clamp01(observedPlayerHealth.CurrentHealth / observedPlayerHealth.MaxHealth)
            : 0f;
        if (!hasInitializedHealthRatio)
        {
            displayedHealthRatio = targetHealthRatio;
            hasInitializedHealthRatio = true;
        }
        if (healthFillRing != null)
        {
            healthFillRing.fillAmount = displayedHealthRatio;
        }

        bool hasShield = observedPlayerHealth.CurrentShield > 0f;
        SetShieldVisible(hasShield);
        if (shieldFillRing != null)
        {
            // 현재 보호막에는 별도 최대치가 없으므로 존재 여부를 완전한 외곽 링으로 표시한다.
            shieldFillRing.fillAmount = hasShield ? 1f : 0f;
        }
    }

    private void SetShieldVisible(bool visible)
    {
        if (shieldBackgroundRing != null)
        {
            shieldBackgroundRing.gameObject.SetActive(visible);
        }
        if (shieldFillRing != null)
        {
            shieldFillRing.gameObject.SetActive(visible);
        }
    }

    /// <summary>Player의 체력·보호막·사망 이벤트를 중복 없이 구독한다.</summary>
    private void SubscribeToPlayerHealth()
    {
        if (observedPlayerHealth == null)
        {
            return;
        }

        observedPlayerHealth.HealthChanged -= OnPlayerHealthOrShieldChanged;
        observedPlayerHealth.ShieldChanged -= OnPlayerHealthOrShieldChanged;
        observedPlayerHealth.Died -= OnPlayerHealthOrShieldChanged;
        observedPlayerHealth.HealthChanged += OnPlayerHealthOrShieldChanged;
        observedPlayerHealth.ShieldChanged += OnPlayerHealthOrShieldChanged;
        observedPlayerHealth.Died += OnPlayerHealthOrShieldChanged;
    }

    /// <summary>현재 Player 체력에서 이 View의 모든 이벤트 구독을 해제한다.</summary>
    private void UnsubscribeFromPlayerHealth()
    {
        if (observedPlayerHealth == null)
        {
            return;
        }

        observedPlayerHealth.HealthChanged -= OnPlayerHealthOrShieldChanged;
        observedPlayerHealth.ShieldChanged -= OnPlayerHealthOrShieldChanged;
        observedPlayerHealth.Died -= OnPlayerHealthOrShieldChanged;
    }

    /// <summary>Player 체력·보호막·사망 상태가 바뀌면 화면 목표값을 다시 계산한다.</summary>
    private void OnPlayerHealthOrShieldChanged(BattleHealth changedHealth)
    {
        RefreshHealthAndShieldTargets();
    }
}
