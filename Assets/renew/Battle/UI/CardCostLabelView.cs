using TMPro;
using UnityEngine;

/// <summary>
/// 카드 아트(코스트 숫자를 지운 "no number" 버전) 위에 코스트 숫자를 동적으로 표시한다.
/// 1146x1661 카드 이미지에서 측정한 보석 위치와 크기를 정규화 좌표로 보관하므로,
/// 손패·인벤토리·상점처럼 카드 표시 크기가 달라도 같은 비율로 비용 숫자가 배치된다.
/// 등급에 따라 보석 색상과 어울리는 외곽선을 적용하고, MP가 부족하면 숫자를 붉게 표시한다.
/// 아래 위치·크기 상수는 디자인 이미지에서 측정한 값이므로 시각 QA 없이 임의 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CardCostLabelView : MonoBehaviour
{
    // 카드 아트(1146x1661 크롭 기준) 코스트 보석 중심의 정규화 좌표(카드 이미지 RectTransform 기준 anchor).
    // 11시 방향(좌상단) 보석 위치를 그대로 사용한다.
    private const float CostGemCenterNormalizedX = 0.1204f;
    private const float CostGemCenterNormalizedY = 0.9199f;

    // 보석 반지름을 카드 이미지 가로/세로 각각에 대한 비율로 표현한다(카드 자체가 정사각형이 아니라서
    // 가로/세로 비율이 다르다). 이렇게 anchor 사각형으로 라벨 크기를 잡으면, 손패처럼 큰 카드든
    // 인벤토리/상점의 작은 카드 썸네일이든 부모 RectTransform 크기에 비례해서 자동으로 맞는다.
    // 절대 크기(sizeDelta)로 고정하면 작은 썸네일에서는 텍스트가 카드보다 훨씬 크게 나온다.
    private const float CostTextHalfWidthRatio = 0.075f;
    private const float CostTextHalfHeightRatio = 0.052f;

    private static readonly Color AffordableCostTextColor = new Color(1f, 0.98f, 0.91f, 1f);
    private static readonly Color InsufficientManaCostTextColor = new Color(1f, 0.25f, 0.22f, 1f);

    private RectTransform costTextRect;
    private TextMeshProUGUI costText;

    /// <summary>CardData.rare 문자열을 비용 보석과 어울리는 TMP 외곽선 색상으로 변환한다.</summary>
    private static Color GetCostOutlineColorForRarity(string cardRarity)
    {
        if (string.IsNullOrEmpty(cardRarity))
        {
            return new Color(0.08f, 0.27f, 0.1f, 1f);
        }

        switch (cardRarity.Trim().ToLowerInvariant())
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

    /// <summary>
    /// 카드 이미지 아래에 이미 생성된 CostLabel이 있으면 재사용하고, 없을 때만 TMP 라벨을 생성한다.
    /// 손패·인벤토리·상점이 같은 배치 규칙을 공유하면서 중복 라벨을 만들지 않기 위한 공용 진입점이다.
    /// </summary>
    public static CardCostLabelView GetOrCreateCostLabel(Transform cardImageTransform)
    {
        if (cardImageTransform == null) return null;

        Transform existingCostLabel = cardImageTransform.Find("CostLabel");
        if (existingCostLabel != null)
        {
            CardCostLabelView existingCostLabelView = existingCostLabel.GetComponent<CardCostLabelView>();
            if (existingCostLabelView != null) return existingCostLabelView;
        }

        GameObject costLabelObject = new GameObject(
            "CostLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        costLabelObject.transform.SetParent(cardImageTransform, false);

        CardCostLabelView createdCostLabelView = costLabelObject.AddComponent<CardCostLabelView>();
        createdCostLabelView.ConfigureMeasuredCostTextLayout();
        return createdCostLabelView;
    }

    /// <summary>
    /// 카드 이미지에서 측정한 정규화 좌표를 RectTransform anchor에 적용하고 TMP 공통 서식을 구성한다.
    /// anchor 기반 크기이므로 부모 카드 이미지가 확대·축소되어도 숫자가 보석 영역과 같은 비율을 유지한다.
    /// </summary>
    private void ConfigureMeasuredCostTextLayout()
    {
        costTextRect = GetComponent<RectTransform>();
        costTextRect.anchorMin = new Vector2(
            CostGemCenterNormalizedX - CostTextHalfWidthRatio,
            CostGemCenterNormalizedY - CostTextHalfHeightRatio);
        costTextRect.anchorMax = new Vector2(
            CostGemCenterNormalizedX + CostTextHalfWidthRatio,
            CostGemCenterNormalizedY + CostTextHalfHeightRatio);
        costTextRect.pivot = new Vector2(0.5f, 0.5f);
        costTextRect.anchoredPosition = Vector2.zero;
        costTextRect.offsetMin = Vector2.zero;
        costTextRect.offsetMax = Vector2.zero;

        costText = GetComponent<TextMeshProUGUI>();
        costText.alignment = TextAlignmentOptions.Center;
        costText.enableAutoSizing = true;
        costText.fontSizeMin = 1f;
        costText.fontSizeMax = 400f;
        costText.fontStyle = FontStyles.Bold;
        costText.color = AffordableCostTextColor;
        costText.raycastTarget = false;
        costText.outlineWidth = 0.28f;
        costText.outlineColor = GetCostOutlineColorForRarity(null);
    }

    /// <summary>현재 카드의 MP 비용을 표시하고 CardData.rare에 맞춰 숫자 외곽선 색을 적용한다.</summary>
    public void DisplayCardCost(int manaCost, string cardRarity)
    {
        if (costText == null) ConfigureMeasuredCostTextLayout();
        costText.text = manaCost.ToString();
        costText.outlineColor = GetCostOutlineColorForRarity(cardRarity);
        Debug.Log($"[CardCostLabelView] SetCost cost={manaCost} rare={cardRarity} active={gameObject.activeInHierarchy} " +
                  $"anchoredPos={costTextRect.anchoredPosition} sizeDelta={costTextRect.sizeDelta} worldPos={costTextRect.position}", this);
    }

    /// <summary>MP 부족 카드는 코스트 숫자도 빨간색으로 표시해 어두운 카드와 함께 즉시 구분한다.</summary>
    public void SetManaAffordabilityColor(bool hasEnoughMana)
    {
        if (costText == null) ConfigureMeasuredCostTextLayout();
        costText.color = hasEnoughMana ? AffordableCostTextColor : InsufficientManaCostTextColor;
    }

    /// <summary>빈 슬롯이나 카드 이미지가 없는 슬롯에서는 비용 숫자를 숨긴다.</summary>
    public void HideCostLabel()
    {
        gameObject.SetActive(false);
    }

    /// <summary>유효한 카드 이미지 위에 비용 숫자를 다시 표시한다.</summary>
    public void ShowCostLabel()
    {
        gameObject.SetActive(true);
    }

    // 아래 네 함수는 renew/share의 기존 호출을 깨지 않기 위한 호환 API다.
    // 신규 Battle 코드는 위의 역할 중심 이름을 사용하고, share 정리 단계에서 호출부 이전 후 삭제한다.
    public static CardCostLabelView Ensure(Transform parent) => GetOrCreateCostLabel(parent);
    public void SetCost(int cost, string rare) => DisplayCardCost(cost, rare);
    public void SetAffordable(bool affordable) => SetManaAffordabilityColor(affordable);
    public void Hide() => HideCostLabel();
    public void Show() => ShowCostLabel();
}
