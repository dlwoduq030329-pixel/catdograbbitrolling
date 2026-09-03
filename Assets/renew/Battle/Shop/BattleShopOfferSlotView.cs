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
    [Tooltip("상품 카드 전체의 호버·클릭 입력을 BattleCardShopSystem에 전달한다.")]
    [SerializeField] private BattleShopOfferHover pointerEvents;

    // Inspector 참조 필드는 private이라 외부 코드가 다른 UI로 교체할 수 없다.
    // 아래 읽기 전용 Property를 통해 BattleCardShopSystem은 연결된 참조를 가져오기만 한다.
    public GameObject Root => gameObject;
    public Button SelectButton => selectButton;
    public Image ItemImage => itemImage;
    public TMP_Text ItemNameText => itemNameText;
    public TMP_Text PriceText => priceText;
    public BattleShopOfferHover PointerEvents => pointerEvents;

    /// <summary>
    /// Inspector에서 상품 선택 버튼, 상품 이미지, 이름, 가격 텍스트가 모두 연결됐는지 계산한다.
    /// 값을 저장하는 bool 필드가 아니라, 조회할 때마다 네 참조의 null 여부를 다시 검사하는 읽기 전용 Property다.
    /// </summary>
    public bool HasAllRequiredReferences =>
        selectButton != null &&
        itemImage != null &&
        itemNameText != null &&
        priceText != null &&
        pointerEvents != null;
}
