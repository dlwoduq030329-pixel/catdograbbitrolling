using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 상품 한 칸의 화면 참조를 보관한다.
/// 상품 생성·구매 판단은 하지 않고, BattleCardShopSystem이 표시를 갱신할 때 필요한 UI만 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleShopOfferSlotView : MonoBehaviour
{
    [Header("상품 슬롯 UI")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text priceText;

    public GameObject Root => gameObject;
    public Button SelectButton => selectButton;
    public Image ItemImage => itemImage;
    public TMP_Text ItemNameText => itemNameText;
    public TMP_Text PriceText => priceText;

    /// <summary>Inspector에서 필수 UI가 모두 연결됐는지 검사한다.</summary>
    public bool HasAllRequiredReferences =>
        selectButton != null &&
        itemImage != null &&
        itemNameText != null &&
        priceText != null;
}
