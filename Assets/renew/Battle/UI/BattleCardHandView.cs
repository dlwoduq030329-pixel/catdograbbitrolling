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

    [Header("카드 슬롯")]
    [Tooltip("CardPanel에 직접 배치한 카드 버튼 5개를 순서대로 연결합니다.")]
    [SerializeField] private Button[] cardButtons = new Button[SlotCount];

    [Header("MP 부족 표시")]
    [Tooltip("현재 MP가 카드 비용보다 낮을 때 카드 이미지에 적용하는 색입니다.")]
    [SerializeField] private Color insufficientMPTint = new Color(0.32f, 0.32f, 0.38f, 0.58f);

    [Header("카드 정보")]
    [Tooltip("카드 정보창을 열기 위해 누르고 있어야 하는 시간입니다.")]
    [Min(0.1f)]
    [SerializeField] private float cardInfoHoldSeconds = 0.4f;

    private BattleCardDrawSystem drawSystem;
    private BattlePlayerActionController playerActionController;
    private BattleCardInfoPresenter cardInfoPresenter;
    private CharacterMP boundPlayerMP;
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
        ConnectPlayerMP();
        SubscribeCardAvailability();
        RefreshCurrentHand();
    }

    private void Start()
    {
        ConnectDrawSystem();
        ConnectPlayerActionController();
        ConnectPlayerMP();
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

        UnsubscribePlayerMP();
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
                CardCostLabelView.Ensure(button.transform)?.Hide();
                SetGeneratedHighlight(button, false);
                continue;
            }

            if (hand[i] < 0)
            {
                button.gameObject.SetActive(true);
                button.interactable = false;
                SetButtonArtworkTint(button, new Color(0.32f, 0.32f, 0.32f, 1f));
                CardCostLabelView.Ensure(button.transform)?.Hide();
                SetGeneratedHighlight(button, false);
                continue;
            }

            CardVisualData visual = ResolveVisualData(hand[i]);
            button.gameObject.SetActive(true);
            bool hasEnoughMP = HasEnoughMPForCard(hand[i]);
            button.interactable = BattleGameManager.Instance != null &&
                                  BattleGameManager.Instance.CanUsePlayerCards && hasEnoughMP;

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
                    cardImage.color = hasEnoughMP ? Color.white : insufficientMPTint;
                }
                else
                {
                    cardImage.sprite = null;
                    cardImage.enabled = false;
                }
            }

            CardCostLabelView costLabel = CardCostLabelView.Ensure(button.transform);
            if (costLabel != null)
            {
                if (visual.Artwork != null)
                {
                    costLabel.Show();
                    costLabel.SetCost(visual.Cost, visual.Rare);
                    costLabel.SetAffordable(hasEnoughMP);
                }
                else
                {
                    costLabel.Hide();
                }
            }

            SetGeneratedHighlight(button, drawSystem != null && drawSystem.IsGeneratedCardSlot(i));

        }
    }

    /// <summary>현재 Player의 MP가 이 카드를 사용하기에 충분한지 확인한다.
    /// 정보를 확인할 수 없을 때는(참조 미연결 등) 기존처럼 막지 않고 true를 반환한다.</summary>
    private bool HasEnoughMPForCard(int legacyCardIndex)
    {
        ConnectPlayerMP();
        GameObject playerObject = boundPlayerMP != null ? boundPlayerMP.gameObject :
            playerActionController != null ? playerActionController.player : null;
        if (playerObject == null)
        {
            return true;
        }

        CharacterMP playerMP = boundPlayerMP != null
            ? boundPlayerMP
            : playerObject.GetComponent<CharacterMP>();
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

        if (drawSystem == null || handIndex < 0 || handIndex >= drawSystem.Hand.Count ||
            !HasEnoughMPForCard(drawSystem.Hand[handIndex]))
        {
            Debug.Log("MP가 부족해 이 카드를 사용할 수 없습니다.", this);
            RefreshCurrentHand();
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

    /// <summary>현재 Player MP 변경 이벤트에 연결해 이동·공격·카드 사용 직후 손패 상태를 갱신한다.</summary>
    private void ConnectPlayerMP()
    {
        CharacterMP nextMP = BattleGameManager.Instance != null
            ? BattleGameManager.Instance.CurrentPlayerMP
            : null;
        if (nextMP == null)
        {
            ConnectPlayerActionController();
            nextMP = playerActionController != null && playerActionController.player != null
                ? playerActionController.player.GetComponent<CharacterMP>()
                : null;
        }

        if (nextMP == boundPlayerMP) return;
        UnsubscribePlayerMP();
        boundPlayerMP = nextMP;
        if (boundPlayerMP != null)
        {
            boundPlayerMP.MPChanged -= HandlePlayerMPChanged;
            boundPlayerMP.MPChanged += HandlePlayerMPChanged;
        }
    }

    private void UnsubscribePlayerMP()
    {
        if (boundPlayerMP != null)
            boundPlayerMP.MPChanged -= HandlePlayerMPChanged;
        boundPlayerMP = null;
    }

    private void HandlePlayerMPChanged(int current, int maximum)
    {
        RefreshCurrentHand();
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

    private const string NoNumberResourceFolder = "UI/Cards/NoNumber/";

    private CardVisualData ResolveVisualData(int cardIndex)
    {
        CardData originalCard = BattleCardConnector.FindOriginalCard(
            cardIndex,
            drawSystem != null ? drawSystem.OriginalDatabase : null);

        if (originalCard == null)
        {
            return new CardVisualData(null, 0, null);
        }

        Sprite artwork = ResolveNoNumberSprite(originalCard.myCardSprite) ?? originalCard.myCardSprite;
        return new CardVisualData(artwork, originalCard.cost, originalCard.rare);
    }

    /// <summary>
    /// 코스트 숫자를 지운 카드 아트가 있으면 그것을 사용하고, 없으면 null을 반환해
    /// 호출부가 원본 아트로 대체하도록 한다("no number" 폴더에 없는 equip/tribe 등은 자동으로 원본 유지).
    /// </summary>
    private static Sprite ResolveNoNumberSprite(Sprite originalSprite)
    {
        if (originalSprite == null) return null;

        string spriteName = originalSprite.name;
        const string multiSuffix = "_0";
        if (spriteName.EndsWith(multiSuffix))
        {
            spriteName = spriteName.Substring(0, spriteName.Length - multiSuffix.Length);
        }

        return Resources.Load<Sprite>(NoNumberResourceFolder + spriteName);
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
                CardCostLabelView.Ensure(button.transform)?.Hide();
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
        public readonly int Cost;
        public readonly string Rare;

        public CardVisualData(Sprite artwork, int cost, string rare)
        {
            Artwork = artwork;
            Cost = cost;
            Rare = rare;
        }
    }
}
