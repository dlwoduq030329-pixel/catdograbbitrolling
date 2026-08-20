using TMPro;
using UnityEngine;

/// <summary>
/// 카드 아트(코스트 숫자를 지운 "no number" 버전) 위에 코스트 숫자를 동적으로 표시한다.
/// 등급(rare)에 따라 원본 보석 색상과 어울리는 아웃라인 색을 입혀
/// 기존에 그림으로 그려져 있던 코스트 뱃지와 비슷한 느낌을 유지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CardCostLabelView : MonoBehaviour
{
    // 카드 아트(1146x1661 크롭 기준) 코스트 보석 중심의 정규화 좌표(카드 이미지 RectTransform 기준 anchor).
    // 11시 방향(좌상단) 보석 위치를 그대로 사용한다.
    private const float BadgeAnchorX = 0.1204f;
    private const float BadgeAnchorY = 0.9199f;

    // 보석 반지름을 카드 이미지 가로/세로 각각에 대한 비율로 표현한다(카드 자체가 정사각형이 아니라서
    // 가로/세로 비율이 다르다). 이렇게 anchor 사각형으로 라벨 크기를 잡으면, 손패처럼 큰 카드든
    // 인벤토리/상점의 작은 카드 썸네일이든 부모 RectTransform 크기에 비례해서 자동으로 맞는다.
    // 절대 크기(sizeDelta)로 고정하면 작은 썸네일에서는 텍스트가 카드보다 훨씬 크게 나온다.
    private const float BadgeHalfWidthFrac = 0.075f;
    private const float BadgeHalfHeightFrac = 0.052f;

    private static readonly Color FillColor = new Color(1f, 0.98f, 0.91f, 1f);
    private static readonly Color InsufficientMPColor = new Color(1f, 0.25f, 0.22f, 1f);

    private RectTransform rect;
    private TextMeshProUGUI label;

    /// <summary>등급 이름(rare 필드 값)에 대응하는 아웃라인 색.</summary>
    private static Color ResolveOutlineColor(string rare)
    {
        if (string.IsNullOrEmpty(rare))
        {
            return new Color(0.08f, 0.27f, 0.1f, 1f);
        }

        switch (rare.Trim().ToLowerInvariant())
        {
            case "rare":
                return new Color(0.05f, 0.12f, 0.27f, 1f);
            case "epic":
                return new Color(0.18f, 0.06f, 0.27f, 1f);
            case "legendary":
                return new Color(0.35f, 0.22f, 0.04f, 1f);
            case "common":
            default:
                return new Color(0.08f, 0.27f, 0.1f, 1f);
        }
    }

    /// <summary>카드 버튼 아래에 코스트 라벨을 만들거나 이미 있으면 재사용한다.</summary>
    public static CardCostLabelView Ensure(Transform parent)
    {
        if (parent == null) return null;

        Transform existing = parent.Find("CostLabel");
        if (existing != null)
        {
            CardCostLabelView existingView = existing.GetComponent<CardCostLabelView>();
            if (existingView != null) return existingView;
        }

        GameObject go = new GameObject(
            "CostLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        CardCostLabelView view = go.AddComponent<CardCostLabelView>();
        view.BuildView();
        return view;
    }

    private void BuildView()
    {
        rect = GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(BadgeAnchorX - BadgeHalfWidthFrac, BadgeAnchorY - BadgeHalfHeightFrac);
        rect.anchorMax = new Vector2(BadgeAnchorX + BadgeHalfWidthFrac, BadgeAnchorY + BadgeHalfHeightFrac);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        label = GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1f;
        label.fontSizeMax = 400f;
        label.fontStyle = FontStyles.Bold;
        label.color = FillColor;
        label.raycastTarget = false;
        label.outlineWidth = 0.28f;
        label.outlineColor = ResolveOutlineColor(null);
    }

    /// <summary>표시할 코스트 값과 등급(뱃지 색 결정용)을 설정한다.</summary>
    public void SetCost(int cost, string rare)
    {
        if (label == null) BuildView();
        label.text = cost.ToString();
        label.outlineColor = ResolveOutlineColor(rare);
        Debug.Log($"[CardCostLabelView] SetCost cost={cost} rare={rare} active={gameObject.activeInHierarchy} " +
                  $"anchoredPos={rect.anchoredPosition} sizeDelta={rect.sizeDelta} worldPos={rect.position}", this);
    }

    /// <summary>MP 부족 카드는 코스트 숫자도 빨간색으로 표시해 어두운 카드와 함께 즉시 구분한다.</summary>
    public void SetAffordable(bool affordable)
    {
        if (label == null) BuildView();
        label.color = affordable ? FillColor : InsufficientMPColor;
    }

    /// <summary>임시로 라벨을 숨긴다(빈 슬롯 등).</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }
}
