using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점의 보유 카드 한 칸을 표시한다. 카드 소유권은 보관하지 않고,
/// PlayerDeck에서 전달받은 보유 수량과 장착 수량을 화면에 그리기만 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleShopOwnedCardSlotView : MonoBehaviour
{
    [Header("카드 표시")]
    [SerializeField] private Image cardImage;
    [SerializeField] private Image[] copyStateImages;

    [Header("카드 한 장의 상태 이미지")]
    [SerializeField] private Sprite ownedSprite;
    [SerializeField] private Sprite equippedSprite;
    [SerializeField] private Sprite emptySprite;

    /// <summary>
    /// 카드 원본과 PlayerDeck에서 계산된 보유·장착 수량을 받아 보유 카드 슬롯 하나를 그린다.
    /// 실제 카드 수량은 변경하지 않으며, 전달받은 숫자를 안전한 범위로 보정한 뒤 상태 이미지만 갱신한다.
    /// </summary>
    public void Display(CardData card, int ownedCount, int equippedCount)
    {
        // 카드가 있으면 앞면 Sprite와 비용 표시를 적용하고, 빈 슬롯이면 카드 이미지를 숨긴다.
        if (cardImage != null)
        {
            cardImage.sprite = card != null ? CardArtResolver.ResolveDisplaySprite(card.myCardSprite) : null;
            cardImage.enabled = cardImage.sprite != null;
            if (card != null)
                CardCostLabelView.Ensure(cardImage.transform)?.SetCost(card.cost, card.rare);
        }

        if (copyStateImages == null) return;

        // 음수 보유량을 0으로 막고, 장착 수량은 실제 보유 수량을 넘지 못하게 보정한다.
        int safeOwnedCount = Mathf.Max(0, ownedCount);
        int safeEquippedCount = Mathf.Clamp(equippedCount, 0, safeOwnedCount);

        // 앞쪽 칸부터 장착 → 보유 → 빈 상태 순서로 Sprite를 배치한다.
        // 예: 보유 2장, 장착 1장이면 [equippedSprite, ownedSprite]가 된다.
        for (int copyIndex = 0; copyIndex < copyStateImages.Length; copyIndex++)
        {
            Image stateImage = copyStateImages[copyIndex];
            if (stateImage == null) continue;
            stateImage.sprite = copyIndex < safeEquippedCount
                ? equippedSprite
                : copyIndex < safeOwnedCount ? ownedSprite : emptySprite;
        }
    }
}
