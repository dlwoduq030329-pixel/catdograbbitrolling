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

    /// <summary>카드 원본과 PlayerDeck 수량을 받아 이 슬롯의 이미지 상태를 갱신한다.</summary>
    public void Display(CardData card, int ownedCount, int equippedCount)
    {
        if (cardImage != null)
        {
            cardImage.sprite = card != null ? CardArtResolver.ResolveDisplaySprite(card.myCardSprite) : null;
            cardImage.enabled = cardImage.sprite != null;
            if (card != null)
                CardCostLabelView.Ensure(cardImage.transform)?.SetCost(card.cost, card.rare);
        }

        if (copyStateImages == null) return;
        int safeOwnedCount = Mathf.Max(0, ownedCount);
        int safeEquippedCount = Mathf.Clamp(equippedCount, 0, safeOwnedCount);
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
