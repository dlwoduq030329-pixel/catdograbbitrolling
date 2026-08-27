using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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
    private BattleUnitMP boundPlayerMP;
    private BattleStatusEffects boundPlayerStatusEffects;
    private readonly BattleCardLongPressHandler[] longPressHandlers =
        new BattleCardLongPressHandler[SlotCount];
    // UnityEvent에서 정확히 같은 클릭 콜백을 제거하려면 등록 당시의 delegate 인스턴스를 보관해야 한다.
    // `RemoveListener(() => SelectCard(index))`처럼 새 람다를 만들면 모양만 같을 뿐 다른 객체라 제거되지 않는다.
    private readonly UnityAction[] cardClickCallbacks = new UnityAction[SlotCount];

    /// <summary>클릭한 카드의 행동 요청이 생성됐을 때 호출된다.</summary>
    public event System.Action<int, SelectedCardUseInfo> CardSelected;

    private void Awake()
    {
        // Awake에서는 Scene/Prefab에 배치된 UI 구조처럼 인스턴스 생명 동안 한 번만 준비하면 되는 작업을 한다.
        // 런타임 Player나 DrawSystem은 아직 생성되지 않았을 수 있으므로 여기서 연결하지 않는다.
        cardInfoPresenter = GetComponentInParent<BattleCardInfoPresenter>(true);
        ValidateReferences();
        RegisterButtonEvents();
        ClearSlots();
    }

    private void OnEnable()
    {
        // CardPanel은 전투 중 꺼졌다 다시 켜질 수 있다. 그동안 Player나 DrawSystem이 교체됐을 수 있으므로
        // 활성화될 때 현재 런타임 객체에 다시 연결하고 마지막으로 최신 손패 상태를 화면에 그린다.
        ConnectDrawSystem();
        ConnectPlayerActionController();
        SubscribeGameManagerEvents();
        BindRegisteredPlayerResources(
            BattleGameManager.Instance != null ? BattleGameManager.Instance.CurrentPlayer : null);
        RefreshCurrentHand();
    }

    private void OnDisable()
    {
        // 비활성 UI가 계속 이벤트를 받으면 숨겨진 버튼을 매번 다시 그리거나 파괴된 참조를 만질 수 있다.
        // OnEnable에서 연결한 런타임 이벤트만 여기서 대칭적으로 해제한다.
        if (drawSystem != null)
        {
            drawSystem.HandCardsChanged -= Refresh;
        }

        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.CardUseAvailabilityChanged -= HandleCardAvailabilityChanged;
            BattleGameManager.Instance.PlayerRegistered -= HandlePlayerRegistered;
        }

        UnsubscribePlayerMP();
    }

    /// <summary>
    /// 현재 손패의 카드 인덱스 목록을 Inspector에 연결된 다섯 버튼에 다시 표시한다.
    /// 슬롯별로 빈칸, 카드 이미지, 현재 실제 MP 비용, 사용 가능 여부, 임시 생성 카드 강조를 한 번에 갱신한다.
    /// 카드 규칙이나 손패 자체는 변경하지 않으며 DrawSystem이 가진 현재 상태를 화면에 복사하기만 한다.
    /// </summary>
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
                // 손패 데이터 자체가 없거나 목록 길이 밖인 슬롯이다.
                // Prefab에 배치한 버튼 오브젝트는 유지하되 이미지/코스트를 지우고 입력만 막는다.
                button.gameObject.SetActive(true);
                button.interactable = false;
                ClearButtonArtwork(button);
                CardCostLabelView.GetOrCreateCostLabel(button.transform)?.HideCostLabel();
                SetGeneratedHighlight(button, false);
                continue;
            }

            if (hand[i] < 0)
            {
                // 손패 목록 안에서 -1은 "이 위치에는 카드가 없음"을 나타내는 빈 슬롯 표식이다.
                // 리스트 길이는 유지되므로 위의 i >= hand.Count 검사와 별도로 처리해야 한다.
                button.gameObject.SetActive(true);
                button.interactable = false;
                SetButtonArtworkTint(button, new Color(0.32f, 0.32f, 0.32f, 1f));
                CardCostLabelView.GetOrCreateCostLabel(button.transform)?.HideCostLabel();
                SetGeneratedHighlight(button, false);
                continue;
            }

            CardVisualData visual = ResolveVisualData(hand[i]);
            button.gameObject.SetActive(true);
            // 카드 기본 비용만 표시하지 않고 동상 등 현재 상태이상으로 바뀐 실제 지불 비용을 계산한다.
            bool cardCostResolved = TryGetCurrentCardCost(hand[i], out int currentCardCost);
            if (!cardCostResolved)
            {
                // 변환 실패 시 화면에는 원본 데이터의 숫자를 남겨 원인 추적을 돕는다.
                // 단, 실제 사용 가능 판정은 아래에서 false로 처리해 알 수 없는 카드를 실행하지 않는다.
                currentCardCost = visual.Cost;
            }
            bool hasEnoughMP = cardCostResolved &&
                               boundPlayerMP != null &&
                               boundPlayerMP.CanSpend(currentCardCost);
            // 카드 버튼은 Player 턴/주사위 규칙과 MP 조건을 모두 만족해야만 클릭할 수 있다.
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
                    // 비용을 지불할 수 없는 카드는 Inspector의 insufficientMPTint를 곱해 어둡게 표시한다.
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

            CardCostLabelView costLabel = CardCostLabelView.GetOrCreateCostLabel(button.transform);
            if (costLabel != null)
            {
                if (visual.Artwork != null)
                {
                    costLabel.ShowCostLabel();
                    // 원본 CardData 비용이 아니라 동상 등 현재 상태가 반영된 실제 사용 비용을 표시한다.
                    costLabel.DisplayCardCost(currentCardCost, visual.Rare);
                    costLabel.SetManaAffordabilityColor(hasEnoughMP);
                }
                else
                {
                    costLabel.HideCostLabel();
                }
            }

            // 이상한 버섯처럼 원래 장착 덱에 없던 임시 생성 카드는 청록색 외곽선으로 구분한다.
            SetGeneratedHighlight(button, drawSystem != null && drawSystem.IsTemporaryCardSlot(i));

        }
    }

    /// <summary>
    /// 카드 행동 데이터의 기본 MP에 현재 Player의 동상(Frostbite) 비용 증가를 반영한다.
    /// 손패 숫자 표시, 부족 색상과 클릭 차단이 모두 이 결과를 공유한다.
    /// </summary>
    private bool TryGetCurrentCardCost(int legacyCardIndex, out int currentCardCost)
    {
        currentCardCost = 0;

        if (!BattleCardConnector.TryCreateActionRequest(
                legacyCardIndex,
                drawSystem != null ? drawSystem.Database : null,
                out BattleActionRequest request,
                out BattleCardData resolvedCard))
        {
            return false;
        }

        currentCardCost = request.MPCost;
        if (boundPlayerStatusEffects != null && resolvedCard != null &&
            resolvedCard.category == BattleCardCategory.Attack)
        {
            currentCardCost = boundPlayerStatusEffects.ModifyAttackCost(currentCardCost);
        }

        return true;
    }

    /// <summary>
    /// 현재 Player MP가 상태이상까지 반영된 실제 카드 비용을 지불할 수 있는지 확인한다.
    /// 카드 데이터 변환에 실패하면 비용을 신뢰할 수 없으므로 안전하게 false를 반환해 사용을 막는다.
    /// </summary>
    private bool HasEnoughMPForCard(int legacyCardIndex)
    {
        if (boundPlayerMP == null)
        {
            return false;
        }

        return TryGetCurrentCardCost(legacyCardIndex, out int currentCardCost) &&
               boundPlayerMP.CanSpend(currentCardCost);
    }

    /// <summary>
    /// 손패 버튼 클릭을 카드 사용 흐름으로 전달한다.
    /// 이 View는 클릭·사용 가능 여부·MP만 확인하고, 대상 선택과 효과 실행은 PlayerActionController에 맡긴다.
    /// </summary>
    private void SelectCard(int handIndex)
    {
        if (handIndex >= 0 && handIndex < longPressHandlers.Length &&
            longPressHandlers[handIndex] != null &&
            longPressHandlers[handIndex].ShouldIgnoreClickAfterLongPress())
        {
            // 길게 눌러 정보창을 연 직후 같은 PointerUp이 일반 클릭까지 발생시키는 것을 한 번 차단한다.
            return;
        }

        if (BattleGameManager.Instance == null || !BattleGameManager.Instance.CanUsePlayerCards)
        {
            Debug.Log("주사위를 굴린 뒤 카드를 사용할 수 있습니다.", this);
            return;
        }

        if (drawSystem == null || handIndex < 0 || handIndex >= drawSystem.HandCards.Count ||
            !HasEnoughMPForCard(drawSystem.HandCards[handIndex]))
        {
            Debug.Log("MP가 부족해 이 카드를 사용할 수 없습니다.", this);
            RefreshCurrentHand();
            return;
        }

        if (drawSystem == null ||
            !drawSystem.TryGetCardUseInfoFromHandSlot(handIndex, out SelectedCardUseInfo selectedCardInfo))
        {
            // 클릭 후 손패가 바뀌었거나 해당 슬롯 데이터가 유효하지 않으면 행동을 만들지 않는다.
            return;
        }

        // 클릭 처리 중에는 Scene 검색이나 런타임 연결을 시도하지 않는다.
        // OnEnable 또는 Player 등록 이벤트에서 참조가 준비되지 않았다면 구성 오류로 보고 사용을 중단한다.
        if (playerActionController == null)
        {
            Debug.LogError(
                "카드 사용 불가: BattlePlayerActionController가 연결되지 않았습니다.",
                this);
            return;
        }

        if (!playerActionController.TryStartCardUseFromHand(selectedCardInfo, drawSystem))
        {
            Debug.LogWarning("카드 사용 확인 단계를 시작하지 못했습니다.", this);
            return;
        }

        CardSelected?.Invoke(handIndex, selectedCardInfo);
        Debug.Log($"카드 선택: {selectedCardInfo.ActionInfo.DisplayName}", this);
    }

    /// <summary>
    /// 다섯 카드 버튼에 슬롯 번호가 고정된 클릭 콜백과 길게 누르기 콜백을 연결한다.
    /// 클릭 콜백은 배열에 보관하여 다시 등록할 때 이전의 정확히 같은 delegate를 제거할 수 있게 한다.
    /// </summary>
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
            if (cardClickCallbacks[i] != null)
            {
                button.onClick.RemoveListener(cardClickCallbacks[i]);
            }

            cardClickCallbacks[i] = () => SelectCard(handIndex);
            button.onClick.AddListener(cardClickCallbacks[i]);

            BattleCardLongPressHandler handler =
                button.GetComponent<BattleCardLongPressHandler>();
            if (handler == null)
            {
                handler = button.gameObject.AddComponent<BattleCardLongPressHandler>();
            }

            handler.ConfigureLongPress(cardInfoHoldSeconds, () => ShowCardInfo(handIndex));
            longPressHandlers[i] = handler;
        }
    }

    /// <summary>길게 누른 슬롯의 현재 카드 데이터를 정보창 Presenter에 전달한다.</summary>
    private void ShowCardInfo(int handIndex)
    {
        if (cardInfoPresenter == null || drawSystem == null ||
            handIndex < 0 || handIndex >= drawSystem.HandCards.Count)
        {
            return;
        }

        cardInfoPresenter.Show(
            drawSystem.HandCards[handIndex],
            drawSystem.OriginalDatabase,
            drawSystem.Database);
    }

    /// <summary>
    /// GameManager가 보유한 현재 DrawSystem을 연결하고 손패 변경 이벤트를 구독한다.
    /// 연결 대상이 바뀌면 이전 시스템의 이벤트부터 해제하여 숨은 중복 Refresh를 방지한다.
    /// GameManager 연결 전 호출되는 예외 상황만 Scene 검색으로 보완한다.
    /// </summary>
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
            drawSystem.HandCardsChanged -= Refresh;
        }

        drawSystem = nextSystem;
        if (drawSystem != null)
        {
            drawSystem.HandCardsChanged -= Refresh;
            drawSystem.HandCardsChanged += Refresh;
        }
    }

    /// <summary>카드 클릭을 실제 Player 행동으로 넘길 ActionController 참조를 확보한다.</summary>
    private void ConnectPlayerActionController()
    {
        if (playerActionController == null)
        {
            playerActionController = FindFirstObjectByType<BattlePlayerActionController>();
        }
    }

    /// <summary>
    /// BattleGameManager가 등록한 Player의 MP와 상태이상 이벤트를 한 번 연결한다.
    /// 손패 카드별 비용 검사에서는 컴포넌트를 다시 찾지 않고 여기서 저장한 참조만 사용한다.
    /// </summary>
    private void BindRegisteredPlayerResources(GameObject registeredPlayer)
    {
        BattleUnitMP nextMP = BattleGameManager.Instance != null &&
            BattleGameManager.Instance.CurrentPlayer == registeredPlayer
                ? BattleGameManager.Instance.CurrentPlayerMP
                : registeredPlayer != null
                    ? registeredPlayer.GetComponent<BattleUnitMP>()
                    : null;

        // 같은 Player에 이미 연결됐다면 중복 구독과 불필요한 손패 갱신을 하지 않는다.
        if (nextMP == boundPlayerMP) return;
        // Player가 교체될 수 있으므로 기존 MP/상태이상 이벤트를 먼저 끊은 뒤 새 대상에 연결한다.
        UnsubscribePlayerMP();
        boundPlayerMP = nextMP;
        if (boundPlayerMP != null)
        {
            boundPlayerMP.MPChanged -= HandlePlayerMPChanged;
            boundPlayerMP.MPChanged += HandlePlayerMPChanged;

            boundPlayerStatusEffects = boundPlayerMP.GetComponent<BattleStatusEffects>();
            if (boundPlayerStatusEffects != null)
            {
                boundPlayerStatusEffects.Changed -= HandlePlayerStatusEffectsChanged;
                boundPlayerStatusEffects.Changed += HandlePlayerStatusEffectsChanged;
            }
        }
    }

    /// <summary>현재 Player의 MP·상태이상 변경 이벤트를 해제하고 저장한 참조를 비운다.</summary>
    private void UnsubscribePlayerMP()
    {
        if (boundPlayerMP != null)
            boundPlayerMP.MPChanged -= HandlePlayerMPChanged;
        if (boundPlayerStatusEffects != null)
            boundPlayerStatusEffects.Changed -= HandlePlayerStatusEffectsChanged;
        boundPlayerMP = null;
        boundPlayerStatusEffects = null;
    }

    /// <summary>MP가 변하면 각 카드의 지불 가능 여부와 부족 색상을 즉시 다시 계산한다.</summary>
    private void HandlePlayerMPChanged(int current, int maximum)
    {
        RefreshCurrentHand();
    }

    /// <summary>동상처럼 카드 MP 비용을 바꾸는 상태이상이 변경되면 MP 부족 표시를 다시 계산한다.</summary>
    private void HandlePlayerStatusEffectsChanged(BattleStatusEffects changedStatusEffects)
    {
        RefreshCurrentHand();
    }

    /// <summary>
    /// 카드 전체 사용 가능 상태와 Player 등록 변경을 받기 위해 GameManager 이벤트를 연결한다.
    /// 먼저 해제한 뒤 구독하여 OnEnable이 반복되어도 같은 콜백이 중복 등록되지 않게 한다.
    /// </summary>
    private void SubscribeGameManagerEvents()
    {
        if (BattleGameManager.Instance == null)
        {
            return;
        }

        BattleGameManager.Instance.CardUseAvailabilityChanged -= HandleCardAvailabilityChanged;
        BattleGameManager.Instance.CardUseAvailabilityChanged += HandleCardAvailabilityChanged;
        BattleGameManager.Instance.PlayerRegistered -= HandlePlayerRegistered;
        BattleGameManager.Instance.PlayerRegistered += HandlePlayerRegistered;
    }

    /// <summary>Player가 생성·교체되면 기존 자원 이벤트를 해제하고 새 Player 자원에 연결한다.</summary>
    private void HandlePlayerRegistered(GameObject registeredPlayer)
    {
        // CardPanel이 먼저 활성화되고 Player 행동 컨트롤러가 나중에 준비되는 실행 순서를 보완한다.
        // 실제 카드 클릭 시점에는 검색하지 않도록 등록 이벤트에서 참조를 한 번 갱신한다.
        ConnectPlayerActionController();
        BindRegisteredPlayerResources(registeredPlayer);
        RefreshCurrentHand();
    }

    /// <summary>
    /// 턴·주사위·행동 잠금 상태가 바뀌면 모든 카드 버튼의 활성 상태를 다시 계산한다.
    /// 임시 카드 생성 전용 이벤트가 아니라 Player가 현재 카드를 사용할 수 있는지를 알리는 공용 이벤트다.
    /// </summary>
    private void HandleCardAvailabilityChanged(bool canUseCards)
    {
        RefreshCurrentHand();
    }

    /// <summary>연결된 DrawSystem의 현재 손패를 읽어 전체 슬롯 표시를 즉시 갱신한다.</summary>
    private void RefreshCurrentHand()
    {
        Refresh(drawSystem != null ? drawSystem.HandCards : null);
    }

    private const string NoNumberResourceFolder = "UI/Cards/NoNumber/";

    /// <summary>
    /// 손패의 카드 인덱스를 UI 표시 전용 이미지·기본 비용·희귀도 묶음으로 변환한다.
    /// 임시 생성 카드도 실제 카드 인덱스를 가지므로 별도 예외 없이 같은 경로로 표시된다.
    /// </summary>
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

    /// <summary>Inspector 배열에서 지정 슬롯의 버튼을 안전하게 가져온다.</summary>
    private bool TryGetButton(int index, out Button button)
    {
        button = cardButtons != null && index < cardButtons.Length
            ? cardButtons[index]
            : null;
        return button != null;
    }

    /// <summary>전투 손패를 받기 전 다섯 슬롯의 입력·이미지·코스트 표시를 초기 상태로 만든다.</summary>
    private void ClearSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (TryGetButton(i, out Button button))
            {
                button.gameObject.SetActive(true);
                button.interactable = false;
                ClearButtonArtwork(button);
                CardCostLabelView.GetOrCreateCostLabel(button.transform)?.HideCostLabel();
            }
        }
    }

    /// <summary>버튼 오브젝트는 유지한 채 카드 이미지와 색상만 빈 슬롯 상태로 초기화한다.</summary>
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

    /// <summary>
    /// 카드 Sprite는 유지하면서 Image에 지정 색을 곱한다.
    /// 흰색은 원본 그대로, 어두운 색은 MP 부족·비활성 상태를 표현한다.
    /// </summary>
    private static void SetButtonArtworkTint(Button button, Color color)
    {
        if (button == null) return;
        Image cardImage = button.targetGraphic as Image;
        if (cardImage == null) cardImage = button.GetComponent<Image>();
        if (cardImage == null) return;
        cardImage.enabled = cardImage.sprite != null;
        cardImage.color = color;
    }

    /// <summary>
    /// 이상한 버섯 등으로 전투 중 임시 생성된 카드 슬롯에 청록색 Outline을 표시한다.
    /// 일반 장착 덱에서 뽑힌 카드는 highlighted=false이므로 외곽선을 끈다.
    /// </summary>
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

    /// <summary>필수 Inspector 구성 누락을 Play Mode 시작 시 Console 경고로 알려준다.</summary>
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

    /// <summary>
    /// 카드 한 장을 버튼에 그리는 동안만 사용하는 UI 전용 값 묶음이다.
    /// 외부 게임 규칙 데이터가 아니므로 추적 파일을 늘리지 않고 View 내부에 둔다.
    /// </summary>
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
