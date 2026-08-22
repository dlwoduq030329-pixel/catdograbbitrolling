using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CardPanel이 소유한 카드 정보 UI에 전투 카드 데이터를 표시한다.
/// 원본 CardInfo 함수와 DataPool을 사용하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCardInfoPresenter : MonoBehaviour
{
    [Header("정보창 오브젝트")]
    [Tooltip("카드 정보를 표시할 전체 오브젝트입니다. 시작할 때 비활성화해 둡니다.")]
    [SerializeField] private GameObject infoRoot;

    [Header("표시 항목")]
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text cardNameText;
    [SerializeField] private TMP_Text cardDescriptionText;
    [InspectorName("카드 유형 텍스트")]
    [SerializeField] private TMP_Text cardTypeText;

    [Header("닫기")]
    [SerializeField] private Button closeButton;

    [Header("입력 우선순위")]
    [Tooltip("다른 HUD보다 카드 정보창 입력을 앞에 배치할 정렬 순서입니다.")]
    [SerializeField] private int inputSortingOrder = 100;

    private int openedFrame = -1;

    private void Awake()
    {
        Hide();
        ConfigureInfoInputLayer();
        ConfigureCloseButton();
    }

    /// <summary>기존 호출 호환을 유지하며 원본 카드 정보만 표시한다.</summary>
    public void Show(int cardIndex, CardDatabase originalDatabase)
    {
        Show(cardIndex, originalDatabase, null);
    }

    /// <summary>고유 카드 인덱스로 원본 데이터와 전투 확장 데이터를 찾아 표시한다.</summary>
    public void Show(
        int cardIndex,
        CardDatabase originalDatabase,
        BattleCardDatabase battleDatabase)
    {
        CardData originalCard = BattleCardConnector.FindOriginalCard(
            cardIndex,
            originalDatabase);
        if (originalCard == null)
        {
            Debug.LogWarning($"카드 정보 표시 실패: 원본 카드 인덱스 {cardIndex}", this);
            return;
        }

        BattleCardData battleCard = battleDatabase != null
            ? battleDatabase.FindByLegacyCardIndex(cardIndex)
            : null;

        if (cardImage != null)
        {
            cardImage.sprite = originalCard.myCardSprite;
            cardImage.enabled = originalCard.myCardSprite != null;
            cardImage.preserveAspect = true;
        }

        SetText(cardNameText, originalCard.name);
        SetText(cardDescriptionText, originalCard.cardInfo);
        SetText(cardTypeText, GetCardTypeText(battleCard));

        if (infoRoot != null)
        {
            infoRoot.SetActive(true);
        }

        ConfigureInfoInputLayer();
        ConfigureCloseButton();
        if (closeButton != null)
        {
            closeButton.transform.SetAsLastSibling();
        }

        openedFrame = Time.frameCount;
    }

    /// <summary>현재 열려 있는 카드 정보창을 닫는다.</summary>
    public void Hide()
    {
        if (infoRoot != null)
        {
            infoRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (infoRoot == null || !infoRoot.activeInHierarchy)
        {
            return;
        }

        if (Time.frameCount > openedFrame && Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }

    private void ConfigureCloseButton()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.enabled = true;
        closeButton.interactable = true;
        BattlePointerSelectionClearer.Ensure(closeButton.gameObject);
        if (closeButton.targetGraphic != null)
        {
            closeButton.targetGraphic.raycastTarget = true;
        }

        closeButton.onClick.RemoveListener(Hide);
        closeButton.onClick.AddListener(Hide);
    }

    private void ConfigureInfoInputLayer()
    {
        if (infoRoot == null)
        {
            return;
        }

        Canvas infoCanvas = infoRoot.GetComponent<Canvas>();
        if (infoCanvas == null)
        {
            infoCanvas = infoRoot.AddComponent<Canvas>();
        }

        infoCanvas.overrideSorting = true;
        infoCanvas.sortingOrder = inputSortingOrder;

        if (infoRoot.GetComponent<GraphicRaycaster>() == null)
        {
            infoRoot.AddComponent<GraphicRaycaster>();
        }

        CanvasGroup infoCanvasGroup = infoRoot.GetComponent<CanvasGroup>();
        if (infoCanvasGroup == null)
        {
            infoCanvasGroup = infoRoot.AddComponent<CanvasGroup>();
        }

        infoCanvasGroup.interactable = true;
        infoCanvasGroup.blocksRaycasts = true;
        infoCanvasGroup.ignoreParentGroups = true;
    }

    private static string GetCardTypeText(BattleCardData battleCard)
    {
        if (battleCard == null)
        {
            return string.Empty;
        }

        switch (battleCard.cardType)
        {
            case BattleCardType.PhysicalDamage:
                return "물리 피해";
            case BattleCardType.MagicDamage:
                return "마법 피해";
            case BattleCardType.Support:
                return "지원 카드";
            default:
                return string.Empty;
        }
    }
}
