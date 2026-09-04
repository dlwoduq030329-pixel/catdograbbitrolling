using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Event_Store의 UI 참조를 역할별로 Inspector에서 직접 연결하고 BattleCardShopSystem에 제공한다.
/// 자식 오브젝트 이름을 검색하지 않으므로 Hierarchy 이름이 바뀌어도 연결이 유지된다.
/// 상품 생성, 구매·판매, 골드 계산은 하지 않는 순수 View 참조 모음이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleShopView : MonoBehaviour
{
    [Header("판매 상품 6칸")]
    [SerializeField] private BattleShopOfferSlotView[] offerSlots;

    [Header("상점 공통 UI")]
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollPriceText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button purchaseButton;

    [Header("선택 상품 상세 UI")]
    [SerializeField] private Image previewImage;
    [SerializeField] private GameObject selectedItemPanel;
    [SerializeField] private TMP_Text selectedItemNameText;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text propertyText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text equipmentInfoText;

    [Header("보유 목록")]
    [SerializeField] private ScrollRect ownedCardScroll;
    [SerializeField] private BattleShopOwnedCardSlotView[] ownedCardSlotPool;
    [SerializeField] private BattleShopOwnedEquipmentSlotView[] ownedEquipmentSlots;

    // 아래 항목은 delegate가 아니라 Inspector의 private 참조를 외부에서 읽게 해주는 Property다.
    // setter가 없으므로 BattleCardShopSystem은 연결 대상을 가져올 수 있지만 여기서 교체할 수는 없다.
    public BattleShopOfferSlotView[] OfferSlots => offerSlots;
    public TMP_Text GoldText => goldText;
    public Button RerollButton => rerollButton;
    public TMP_Text RerollPriceText => rerollPriceText;
    public Button CloseButton => closeButton;
    public Button PurchaseButton => purchaseButton;
    public Image PreviewImage => previewImage;
    public GameObject SelectedItemPanel => selectedItemPanel;
    public TMP_Text SelectedItemNameText => selectedItemNameText;
    public TMP_Text TargetText => targetText;
    public TMP_Text PropertyText => propertyText;
    public TMP_Text DamageText => damageText;
    public TMP_Text EquipmentInfoText => equipmentInfoText;
    public ScrollRect OwnedCardScroll => ownedCardScroll;
    public BattleShopOwnedCardSlotView[] OwnedCardSlotPool => ownedCardSlotPool;
    public BattleShopOwnedEquipmentSlotView[] OwnedEquipmentSlots => ownedEquipmentSlots;

    /// <summary>
    /// 상점 실행에 반드시 필요한 참조를 검사한다. 판매 슬롯은 요구 개수와 정확히 같아야 하며,
    /// 각 슬롯 내부의 버튼·상품 이미지·이름·가격 참조도 모두 연결돼야 한다.
    /// 상세 정보와 보유 목록은 부가 표시 기능이므로 비어 있어도 필수 검사에는 포함하지 않는다.
    /// false가 반환되면 BattleCardShopSystem이 View를 연결하지 않고 상점 진입도 중단한다.
    /// </summary>
    public bool HasRequiredReferences(int requiredSlotCount)
    {
        if (offerSlots == null || offerSlots.Length != requiredSlotCount) return false;
        foreach (BattleShopOfferSlotView slot in offerSlots)
            if (slot == null || !slot.HasAllRequiredReferences) return false;

        return goldText != null &&
               rerollButton != null &&
               rerollPriceText != null &&
               closeButton != null &&
               purchaseButton != null;
    }
}
