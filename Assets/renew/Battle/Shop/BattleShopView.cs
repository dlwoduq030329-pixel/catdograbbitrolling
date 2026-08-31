using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Event_Store의 UI 참조를 Inspector에서 직접 연결하는 상점 화면 컴포넌트다.
/// 자식 오브젝트 이름을 검색하지 않으므로 Hierarchy 이름이 바뀌어도 연결이 유지된다.
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

    [Header("보유 목록 - 레거시 표시 컴포넌트 임시 사용")]
    [SerializeField] private ScrollRect ownedCardScroll;
    [SerializeField] private BattleShopOwnedCardSlotView[] ownedCardSlotPool;
    [SerializeField] private BattleShopOwnedEquipmentSlotView[] ownedEquipmentSlots;

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
    /// 상점 실행에 반드시 필요한 참조를 검사한다. 상세 정보나 보유 목록은 선택 기능이므로
    /// 비어 있어도 상점 진입 자체는 허용한다.
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
