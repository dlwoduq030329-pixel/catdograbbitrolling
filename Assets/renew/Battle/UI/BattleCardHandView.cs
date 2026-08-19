using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 직접 배치한 카드 버튼 5개에 현재 손패의 카드 이미지를 표시한다.
/// 현재 MP로 사용할 수 없는 카드는 어둡게 처리(블러 대체 표현)하고 클릭을 막는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCardHandView : MonoBehaviour
{
    private const int SlotCount = 5;

    /// <summary>MP가 부족해 사용할 수 없는 카드에 적용하는 어둡고 반투명한 톤(블러 대체 표현).</summary>
    private static readonly Color InsufficientMPTint = new Color(0.4f, 0.4f, 0.4f, 0.55f);

    [Header("카드 슬롯")]
    [Tooltip("CardPanel에 직접 배치한 카드 버튼 5개를 순서대로 연결합니다.")]
    [SerializeField] private Button[] cardButtons = new Button[SlotCount];

    [Header("카드 정보")]
    [Tooltip("카드 정보창을 열기 위해 누르고 있어야 하는 시간입니다.")]
    [Min(0.1f)]
    [SerializeField] private float cardInfoHoldSeconds = 0.4f;

    private BattleCardDrawSystem drawSystem;
    private BattlePlayerActionController playerActionController;
    private BattleCardInfoPresenter cardInfoPresenter;
    private readonly BattleCardLongPressHandler[] longPressHandlers =
        new BattleCardLongPressHandler[SlotCount];
    private float nextTargetRefreshTime;

    /// <summary>클릭한 카드의 행동 요청이 생성됐을 때 호출된다.</summary>
    public event System.Action<int, PendingBattleCardUse> CardSelected;

    private void Awake()
    {
        cardInfoPresenter = GetComponentInParent<BattleCardInfoPresenter>(true);
        ValidateReferences();
        RegisterButtonEvents();
        ClearSlots();
    }

    private void OnEnable()
    {
        ConnectDrawSystem();
        ConnectPlayerActionController();
        SubscribeCardAvailability();
        RefreshCurrentHand();
    }

    private void Start()
    {
        ConnectDrawSystem();
        ConnectPlayerActionController();
        SubscribeCardAvailability();
        RefreshCurrentHand();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextTargetRefreshTime) return;
        nextTargetRefreshTime = Time.unscaledTime + 0.2f;
        if (BattleGameManager.Instance != null && BattleGameManager.Instance.CanUsePlayerCards)
            RefreshCurrentHand();
    }

    private void OnDisable()
    {
        if (drawSystem != null)
        {
            drawSystem.HandChanged -= Refresh;
        }

        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.CardUseAvailabilityChanged -= HandleCardAvailabilityChanged;
        }
    }

    /// <summary>현재 손패를 연결된 카드 버튼에 다시 표시한다.</summary>
    private void Refresh(IReadOnlyList<int> hand)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (!TryGetButton(i, out Button button))
            {
                continue;
            }

            if (hand == null || i >= hand.Count)
            {
                // 빈 슬롯도 배치한 버튼 이미지는 유지하고 입력만 막는다.
                button.gameObject.SetActive(true);
                button.interactable = false;
                ClearButtonArtwork(button);
                SetGeneratedHighlight(button, false);
                continue;
            }

            if (hand[i] < 0)
            {
                button.gameObject.SetActive(true);
                button.interactable = false;
                SetButtonArtworkTint(button, new Color(0.32f, 0.32f, 0.32f, 1f));
                SetGeneratedHighlight(button, false);
                continue;
            }

            CardVisualData visual = ResolveVisualData(hand[i]);
            button.gameObject.SetActive(true);
            BattleCardData battleCard = drawSystem != null && drawSystem.Database != null
                ? drawSystem.Database.FindByLegacyCardIndex(hand[i]) : null;
            bool hasValidTarget = playerActionController == null ||
                                  playerActionController.HasValidTargetForCard(battleCard);
            bool hasEnoughMP = HasEnoughMPForCard(hand[i]);
            button.interactable = BattleGameManager.Instance != null &&
                                  BattleGameManager.Instance.CanUsePlayerCards && hasValidTarget && hasEnoughMP;

            Image cardImage = button.targetGraphic as Image;
            if (cardImage == null)
            {
                cardImage = button.GetComponent<Image>();
            }

            if (cardImage != null)
            {
                if (visual.Artwork != null)
                {
                    cardImage.sprite = visual.Artwork;
                    cardImage.enabled = true;
                    cardImage.preserveAspect = true;
                    cardImage.color = hasEnoughMP ? Color.white : InsufficientMPTint;
                }
                else
                {
                    cardImage.sprite = null;
                    cardImage.enabled = false;
                }
            }
            SetGeneratedHighlight(button, drawSystem != null && drawSystem.IsGeneratedCardSlot(i));

        }
    }

    /// <summary>현재 Player의 MP가 이 카드를 사용하기에 충분한지 확인한다.
    /// 정보를 확인할 수 없을 때는(참조 미연결 등) 기존처럼 막지 않고 true를 반환한다.</summary>
    private bool HasEnoughMPForCard(int legacyCardIndex)
    {
        ConnectPlayerActionController();
        GameObject playerObject = playerActionController != null ? playerActionController.player : null;
        if (playerObject == null)
        {
            return true;
        }

        CharacterMP playerMP = playerObject.GetComponent<CharacterMP>();
        if (playerMP == null)
        {
            return true;
        }

        if (!BattleCardConnector.TryCreateActionRequest(
                legacyCardIndex,
                drawSystem != null ? drawSystem.Database : null,
                out BattleActionRequest request,
                out BattleCardData resolvedCard))
        {
            return true;
        }

        int cardCost = request.MPCost;
        BattleStatusEffects status = playerObject.GetComponent<BattleStatusEffects>();
        if (status != null && resolvedCard != null && resolvedCard.category == BattleCardCategory.Attack)
        {
            cardCost = status.ModifyAttackCost(cardCost);
        }

        return playerMP.CanSpend(cardCost);
    }

    /// <summary>선택한 손패 카드의 행동 요청을 생성한다.</summary>
    private void SelectCard(int handIndex)
    {
        if (handIndex >= 0 && handIndex < longPressHandlers.Length &&
            longPressHandlers[handIndex] != null &&
            longPressHandlers[handIndex].ConsumeSuppressedClick())
        {
            return;
        }

        if (BattleGameManager.Instance == null || !BattleGameManager.Instance.CanUsePlayerCards)
        {
            Debug.Log("주사위를 굴린 뒤 카드를 사용할 수 있습니다.", this);
            return;
        }

        if (drawSystem == null || !drawSystem.BeginCardUse(handIndex, out PendingBattleCardUse pendingUse))
        {
            return;
        }

        ConnectPlayerActionController();
        if (playerActionController == null ||
            !playerActionController.BeginCardUseConfirmation(pendingUse, drawSystem))
        {
            Debug.LogWarning("카드 사용 확인 단계를 시작하지 못했습니다.", this);
            return;
        }

        CardSelected?.Invoke(handIndex, pendingUse);
        Debug.Log($"카드 선택: {pendingUse.ActionRequest.DisplayName}", this);
    }

    private void RegisterButtonEvents()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (!TryGetButton(i, out Button button))
            {
                continue;
            }

            int handIndex = i;
            BattlePointerSelectionClearer.Ensure(button.gameObject);
            button.onClick.RemoveListener(() => SelectCard(handIndex));
            button.onClick.AddListener(() => SelectCard(handIndex));

            BattleCardLongPressHandler handler =
                button.GetComponent<BattleCardLongPressHandler>();
            if (handler == null)
            {
                handler = button.gameObject.AddComponent<BattleCardLongPressHandler>();
            }

            handler.Configure(cardInfoHoldSeconds, () => ShowCardInfo(handIndex));
            longPressHandlers[i] = handler;
        }
    }

    private void ShowCardInfo(int handIndex)
    {
        if (cardInfoPresenter == null || drawSystem == null ||
            handIndex < 0 || handIndex >= drawSystem.Hand.Count)
        {
            return;
        }

        cardInfoPresenter.Show(
            drawSystem.Hand[handIndex],
            drawSystem.OriginalDatabase,
            drawSystem.Database);
    }

    private void ConnectDrawSystem()
    {
        BattleCardDrawSystem nextSystem = BattleGameManager.Instance != null
            ? BattleGameManager.Instance.CardDrawSystem
            : FindFirstObjectByType<BattleCardDrawSystem>();

        if (nextSystem == drawSystem)
        {
            return;
        }

        if (drawSystem != null)
        {
            drawSystem.HandChanged -= Refresh;
        }

        drawSystem = nextSystem;
        if (drawSystem != null)
        {
            drawSystem.HandChanged -= Refresh;
            drawSystem.HandChanged += Refresh;
        }
    }

    private void ConnectPlayerActionController()
    {
        if (playerActionController == null)
        {
            playerActionController = FindFirstObjectByType<BattlePlayerActionController>();
        }
    }

    private void SubscribeCardAvailability()
    {
        if (BattleGameManager.Instance == null)
        {
            return;
        }

        BattleGameManager.Instance.CardUseAvailabilityChanged -= HandleCardAvailabilityChanged;
        BattleGameManager.Instance.CardUseAvailabilityChanged += HandleCardAvailabilityChanged;
    }

    private void HandleCardAvailabilityChanged(bool canUseCards)
    {
        RefreshCurrentHand();
    }

    private void RefreshCurrentHand()
    {
        Refresh(drawSystem != null ? drawSystem.Hand : null);
    }

    private CardVisualData ResolveVisualData(int cardIndex)
    {
        CardData originalCard = BattleCardConnector.FindOriginalCard(
            cardIndex,
            drawSystem != null ? drawSystem.OriginalDatabase : null);
        return new CardVisualData(originalCard != null ? originalCard.myCardSprite : null);
    }

    private bool TryGetButton(int index, out Button button)
    {
        button = cardButtons != null && index < cardButtons.Length
            ? cardButtons[index]
            : null;
        return button != null;
    }

    private void ClearSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (TryGetButton(i, out Button button))
            {
                button.gameObject.SetActive(true);
                button.interactable = false;
                ClearButtonArtwork(button);
            }
        }
    }

    private static void ClearButtonArtwork(Button button)
    {
        if (button == null) return;
        Image cardImage = button.targetGraphic as Image;
        if (cardImage == null) cardImage = button.GetComponent<Image>();
        if (cardImage == null) return;
        cardImage.sprite = null;
        cardImage.enabled = false;
        cardImage.color = Color.white;
    }

    private static void SetButtonArtworkTint(Button button, Color color)
    {
        if (button == null) return;
        Image cardImage = button.targetGraphic as Image;
        if (cardImage == null) cardImage = button.GetComponent<Image>();
        if (cardImage == null) return;
        cardImage.enabled = cardImage.sprite != null;
        cardImage.color = color;
    }

    private static void SetGeneratedHighlight(Button button, bool highlighted)
    {
        if (button == null) return;
        Outline outline = button.GetComponent<Outline>();
        if (outline == null && highlighted) outline = button.gameObject.AddComponent<Outline>();
        if (outline == null) return;
        outline.effectColor = new Color(0.2f, 1f, 0.85f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.useGraphicAlpha = true;
        outline.enabled = highlighted;
    }

    private void ValidateReferences()
    {
        if (cardButtons == null || cardButtons.Length != SlotCount)
        {
            Debug.LogWarning("카드 버튼 배열에는 버튼 5개가 필요합니다.", this);
        }

        if (cardInfoPresenter == null)
        {
            Debug.LogWarning(
                "CardPanel 부모에 BattleCardInfoPresenter가 없습니다. 카드 길게 누르기 정보창을 사용할 수 없습니다.",
                this);
        }
    }

    private readonly struct CardVisualData
    {
        public readonly Sprite Artwork;

        public CardVisualData(Sprite artwork)
        {
            Artwork = artwork;
        }
    }
}
