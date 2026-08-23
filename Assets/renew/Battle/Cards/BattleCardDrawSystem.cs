using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택한 손패 카드가 행동 확정 전까지 유지되도록 묶어 둔 요청이다.
/// 확인 시에만 카드가 버림 더미로 이동하며 취소 시에는 아무 변화가 없다.
/// </summary>
public sealed class CardUseWaitingForConfirmation
{
    public int HandIndex { get; }
    public int CardIndex { get; }
    public BattleCardData CardData { get; }
    public BattleActionRequest ActionRequest { get; }

    internal CardUseWaitingForConfirmation(
        int handIndex,
        int cardIndex,
        BattleCardData cardData,
        BattleActionRequest actionRequest)
    {
        HandIndex = handIndex;
        CardIndex = cardIndex;
        CardData = cardData;
        ActionRequest = actionRequest;
    }
}

/// <summary>
/// 플레이어 전투 덱, 손패, 버림 더미와 턴 시작 드로우를 관리한다.
/// 카드 UI는 HandChanged 이벤트와 카드 사용 대기 생성/확정 API만 사용한다.
/// </summary>
public class BattleCardDrawSystem : MonoBehaviour
{
    [Header("전투 카드 데이터")]
    [InspectorName("전투 카드 데이터베이스")]
    [SerializeField] private BattleCardDatabase battleCardDatabase;
    [InspectorName("원본 카드 데이터베이스")]
    [SerializeField] private CardDatabase originalCardDatabase;

    [Header("드로우 규칙")]
    [InspectorName("최대 손패 수")]
    [SerializeField, Min(1)] private int handLimit = 5;
    [InspectorName("전투 시작 시 덱 섞기")]
    [SerializeField] private bool shuffleAtBattleStart = true;

    [Header("디버그 설정")]
    [Tooltip("활성화하면 판매 QA를 위해 전투 카드 데이터베이스에 등록된 모든 카드를 두 장씩 소지합니다.")]
    [InspectorName("모든 카드 소지")]
    [SerializeField] private bool ownAllCardsInDebugMode = true;

    private readonly List<int> cardsWaitingToBeDrawn = new List<int>();
    private readonly List<int> cardsInCurrentHand = new List<int>();
    private readonly List<int> mpDiscountByHandSlot = new List<int>();
    private readonly List<int> usedCardsWaitingForReshuffle = new List<int>();
    private readonly HashSet<int> temporarilyGeneratedCardSlots = new HashSet<int>();
    private bool battleCardCycleInitialized;

    public IReadOnlyList<int> CardsWaitingToBeDrawn => cardsWaitingToBeDrawn;
    public IReadOnlyList<int> CardsInCurrentHand => cardsInCurrentHand;
    public IReadOnlyList<int> UsedCardsWaitingForReshuffle => usedCardsWaitingForReshuffle;
    public int HandLimit => handLimit;
    public BattleCardDatabase Database => battleCardDatabase;
    public CardDatabase OriginalDatabase => originalCardDatabase;
    public bool IsGeneratedCardSlot(int handIndex) => temporarilyGeneratedCardSlots.Contains(handIndex);

    /// <summary>손패가 드로우되거나 카드 사용 확정으로 변경될 때 UI에 알린다.</summary>
    public event Action<IReadOnlyList<int>> HandChanged;
    public event Action<int> CardDrawn;

    /// <summary>BattleGameManager가 전투에서 사용할 카드 DB를 한 번 전달한다.</summary>
    public void ConfigureDatabase(BattleCardDatabase database)
    {
        battleCardDatabase = database;
    }

    /// <summary>기존 CardDatabase를 카드 인덱스 조회 원본으로 연결한다. 데이터 복제나 수정은 수행하지 않는다.</summary>
    public void ConfigureOriginalDatabase(CardDatabase database)
    {
        originalCardDatabase = database;
    }

    /// <summary>컴포넌트 활성화 시 플레이어 턴 시작 이벤트 구독을 시도한다.</summary>
    private void OnEnable()
    {
        TrySubscribeTurnManager();
    }

    /// <summary>게임 관리자 초기화 순서가 늦은 경우를 대비해 시작 시 구독을 다시 확인한다.</summary>
    private void Start()
    {
        // BattleGameManager보다 먼저 활성화된 경우를 보완한다.
        TrySubscribeTurnManager();
    }

    /// <summary>비활성화 시 턴 이벤트 구독을 해제해 드로우가 중복 실행되지 않게 한다.</summary>
    private void OnDisable()
    {
        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.PlayerTurnStarted -= HandlePlayerTurnStarted;
        }
    }

    /// <summary>PlayerDeck에 장착된 카드 인덱스를 복사해 전투용 드로우 덱을 초기화한다.</summary>
    public void InitializeBattleCardCycle()
    {
        InitializeBattleCardCycle(null);
    }

    /// <summary>등록된 실제 PlayerDeck을 우선 사용해 전투 덱과 디버그 보유량을 초기화한다.</summary>
    public void InitializeBattleCardCycle(PlayerDeck registeredPlayerDeck)
    {
        cardsWaitingToBeDrawn.Clear();
        cardsInCurrentHand.Clear();
        mpDiscountByHandSlot.Clear();
        usedCardsWaitingForReshuffle.Clear();
        temporarilyGeneratedCardSlots.Clear();

        PlayerDeck sourceDeck = registeredPlayerDeck != null
            ? registeredPlayerDeck
            : FindFirstObjectByType<PlayerDeck>(FindObjectsInactive.Include);
        if (ownAllCardsInDebugMode)
        {
            GrantEveryCardForShopTesting(sourceDeck);
        }
        CopyEquippedCardsIntoDrawQueue(sourceDeck);

        if (cardsWaitingToBeDrawn.Count == 0)
        {
            Debug.LogError(
                "전투 덱 초기화 실패: PlayerDeck.deckCardforUI에 유효한 장착 카드가 없습니다. " +
                "보유 카드 전체를 임의 드로우 덱으로 사용하지 않습니다.",
                this);
        }

        if (shuffleAtBattleStart)
        {
            Shuffle(cardsWaitingToBeDrawn);
        }

        battleCardCycleInitialized = true;
        DrawCardsUntilHandIsFull();
    }

    /// <summary>
    /// 디버그 전투에서 판매 QA를 위해 모든 전투 카드의 보유 수량만 2장으로 설정합니다.
    /// 실제 손패 드로우 덱은 이 목록이 아니라 PlayerDeck.deckCardforUI 10칸만 사용합니다.
    /// 실제 저장 데이터는 변경하지 않고 현재 실행 중인 메모리 값만 변경합니다.
    /// </summary>
    private void GrantEveryCardForShopTesting(PlayerDeck sourceDeck)
    {
        if (battleCardDatabase == null)
        {
            Debug.LogWarning("디버그 카드 지급 실패: 전투 카드 데이터베이스가 연결되지 않았습니다.", this);
            return;
        }

        foreach (BattleCardData card in battleCardDatabase.Cards)
        {
            if (card == null || card.legacyCardIndex < 0 ||
                BattleCardConnector.FindOriginalCard(card.legacyCardIndex, originalCardDatabase) == null)
            {
                continue;
            }

            int cardIndex = card.legacyCardIndex;
            if (sourceDeck != null)
            {
                sourceDeck.AddOwnedCard(cardIndex, PlayerDeck.MaximumOwnedCopiesPerCard);
                DataConfig.CardsCount[cardIndex] = sourceDeck.GetOwnedCardCount(cardIndex);
            }
            else
            {
                DataConfig.CardsCount[cardIndex] = PlayerDeck.MaximumOwnedCopiesPerCard;
            }
        }

        Debug.Log("디버그 카드 보유량 지급 완료: 유효 카드별 2장", this);
    }

    /// <summary>PlayerDeck의 장착 10칸에서 유효한 카드만 순서대로 전투 드로우 더미에 복사한다.</summary>
    private void CopyEquippedCardsIntoDrawQueue(PlayerDeck sourceDeck)
    {
        if (sourceDeck == null || sourceDeck.EquippedCards == null) return;
        foreach (int cardIndex in sourceDeck.EquippedCards)
        {
            if (cardIndex < 0 ||
                BattleCardConnector.FindOriginalCard(cardIndex, originalCardDatabase) == null ||
                battleCardDatabase == null ||
                battleCardDatabase.FindByLegacyCardIndex(cardIndex) == null)
            {
                continue;
            }
            cardsWaitingToBeDrawn.Add(cardIndex);
        }
        Debug.Log($"장착 덱 복사 완료: deckCardforUI 기준 {cardsWaitingToBeDrawn.Count}장", this);
    }

    /// <summary>현재 손패가 최대 손패 수가 될 때까지 드로우한다.</summary>
    public void DrawCardsUntilHandIsFull()
    {
        while (cardsInCurrentHand.Count < handLimit && TryDrawOne(out _))
        {
        }

        HandChanged?.Invoke(cardsInCurrentHand);
    }

    /// <summary>손패 위치의 카드를 전투 행동 요청으로 변환한다. 이 단계에서는 카드를 제거하지 않는다.</summary>
    public bool TryCreateCardUseWaitingForConfirmation(int handIndex, out CardUseWaitingForConfirmation pendingUse)
    {
        pendingUse = null;
        if (handIndex < 0 || handIndex >= cardsInCurrentHand.Count)
        {
            return false;
        }

        int cardIndex = cardsInCurrentHand[handIndex];
        if (cardIndex < 0)
        {
            return false;
        }
        BattleCardData cardData = battleCardDatabase != null
            ? battleCardDatabase.FindByLegacyCardIndex(cardIndex)
            : null;

        if (!BattleCardConnector.TryCreateActionRequest(
                cardData,
                originalCardDatabase,
                out BattleActionRequest actionRequest))
        {
            Debug.LogError($"카드 행동 요청 생성 실패: 전투 카드 인덱스 {cardIndex}", this);
            return false;
        }

        int reduction = handIndex < mpDiscountByHandSlot.Count ? mpDiscountByHandSlot[handIndex] : 0;
        if (reduction > 0)
        {
            actionRequest = new BattleActionRequest(
                actionRequest.DisplayName,
                actionRequest.ActionType,
                actionRequest.RangeTiles,
                Mathf.Max(0, actionRequest.MPCost - reduction),
                actionRequest.Power);
        }

        pendingUse = new CardUseWaitingForConfirmation(handIndex, cardIndex, cardData, actionRequest);
        return true;
    }

    /// <summary>카드 행동 확정 후 해당 카드 한 장을 손패에서 제거하고 버림 더미로 보낸다.</summary>
    public bool MoveConfirmedCardToUsedPile(CardUseWaitingForConfirmation pendingUse)
    {
        if (pendingUse == null)
        {
            return false;
        }

        int handIndex = pendingUse.HandIndex;
        if (handIndex < 0 || handIndex >= cardsInCurrentHand.Count || cardsInCurrentHand[handIndex] != pendingUse.CardIndex)
        {
            return false;
        }

        // Keep the consumed slot until the next player turn so the UI can retain
        // and darken the used card artwork instead of shifting/removing it.
        // 약초 버섯 등으로 생성된 보너스 카드는 장착 덱 소속이 아니므로, 버림 더미로 보내면
        // RecycleDiscardPile()을 통해 이후 턴에도 계속 드로우 덱을 돌며 나오게 된다.
        // 장착 덱 카드만 계속 순환하도록 보너스 카드는 사용 후 완전히 소멸시킨다.
        bool wasGeneratedCard = temporarilyGeneratedCardSlots.Contains(handIndex);
        cardsInCurrentHand[handIndex] = -1;
        temporarilyGeneratedCardSlots.Remove(handIndex);
        if (handIndex < mpDiscountByHandSlot.Count) mpDiscountByHandSlot[handIndex] = 0;
        if (!wasGeneratedCard)
        {
            usedCardsWaitingForReshuffle.Add(pendingUse.CardIndex);
        }
        HandChanged?.Invoke(cardsInCurrentHand);
        return true;
    }

    /// <summary>판매 후 최종 보유 수량에 맞춰 드로우·버림·손패의 동일 카드 수를 정규화한다.</summary>
    public void SynchronizeOwnedCardCount(int cardIndex, int remainingOwned)
    {
        int excess = CountRuntimeCardCopies(cardIndex) - Mathf.Max(0, remainingOwned);
        while (excess > 0 && cardsWaitingToBeDrawn.Remove(cardIndex)) excess--;
        while (excess > 0 && usedCardsWaitingForReshuffle.Remove(cardIndex)) excess--;

        bool handChanged = false;
        for (int i = cardsInCurrentHand.Count - 1; i >= 0 && excess > 0; i--)
        {
            if (cardsInCurrentHand[i] != cardIndex) continue;
            cardsInCurrentHand[i] = -1;
            if (i < mpDiscountByHandSlot.Count) mpDiscountByHandSlot[i] = 0;
            temporarilyGeneratedCardSlots.Remove(i);
            handChanged = true;
            excess--;
        }
        if (handChanged) HandChanged?.Invoke(cardsInCurrentHand);
    }

    /// <summary>전투 런타임의 모든 더미와 손패에 존재하는 특정 카드 장수를 합산한다.</summary>
    private int CountRuntimeCardCopies(int cardIndex)
    {
        int count = 0;
        foreach (int value in cardsWaitingToBeDrawn) if (value == cardIndex) count++;
        foreach (int value in usedCardsWaitingForReshuffle) if (value == cardIndex) count++;
        foreach (int value in cardsInCurrentHand) if (value == cardIndex) count++;
        return count;
    }

    /// <summary>소모된 버섯의 손패 자리에 무작위 카드를 넣고 이번 턴 MP 비용을 1 낮춘다.</summary>
    public bool GenerateWeirdMushroomCard(CardUseWaitingForConfirmation consumedUse)
    {
        if (consumedUse == null || battleCardDatabase == null) return false;

        Dictionary<string, List<int>> candidatesByRarity = new Dictionary<string, List<int>>();
        foreach (BattleCardData card in battleCardDatabase.Cards)
        {
            CardData original = card != null
                ? BattleCardConnector.FindOriginalCard(card.legacyCardIndex, originalCardDatabase)
                : null;
            if (card == null || card.legacyCardIndex < 0 ||
                card.legacyCardIndex == consumedUse.CardIndex ||
                original == null)
                continue;
            string rarity = string.IsNullOrWhiteSpace(original.rare) ? "common" : original.rare;
            if (!candidatesByRarity.TryGetValue(rarity, out List<int> rarityCards))
            {
                rarityCards = new List<int>();
                candidatesByRarity.Add(rarity, rarityCards);
            }
            rarityCards.Add(card.legacyCardIndex);
        }
        if (candidatesByRarity.Count == 0) return false;

        List<string> rarities = new List<string>(candidatesByRarity.Keys);
        string selectedRarity = rarities[UnityEngine.Random.Range(0, rarities.Count)];
        List<int> candidates = candidatesByRarity[selectedRarity];
        int generatedIndex = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        int insertIndex = Mathf.Clamp(consumedUse.HandIndex, 0, Mathf.Max(0, cardsInCurrentHand.Count - 1));
        if (insertIndex < cardsInCurrentHand.Count && cardsInCurrentHand[insertIndex] < 0)
        {
            cardsInCurrentHand[insertIndex] = generatedIndex;
            mpDiscountByHandSlot[insertIndex] = 1;
        }
        else
        {
            cardsInCurrentHand.Insert(insertIndex, generatedIndex);
            mpDiscountByHandSlot.Insert(insertIndex, 1);
        }
        temporarilyGeneratedCardSlots.Add(insertIndex);
        HandChanged?.Invoke(cardsInCurrentHand);
        return true;
    }

    /// <summary>카드 한 장을 드로우한다. 덱이 비면 버림 더미를 섞어 새 덱으로 만든다.</summary>
    private bool TryDrawOne(out int cardIndex)
    {
        cardIndex = -1;
        if (cardsWaitingToBeDrawn.Count == 0)
        {
            RecycleDiscardPile();
        }

        if (cardsWaitingToBeDrawn.Count == 0)
        {
            return false;
        }

        int lastIndex = cardsWaitingToBeDrawn.Count - 1;
        cardIndex = cardsWaitingToBeDrawn[lastIndex];
        cardsWaitingToBeDrawn.RemoveAt(lastIndex);
        cardsInCurrentHand.Add(cardIndex);
        mpDiscountByHandSlot.Add(0);
        CardDrawn?.Invoke(cardIndex);
        return true;
    }

    /// <summary>드로우 덱이 비었을 때 버림 더미를 옮겨 섞고 새로운 드로우 덱으로 만든다.</summary>
    private void RecycleDiscardPile()
    {
        if (usedCardsWaitingForReshuffle.Count == 0)
        {
            return;
        }

        cardsWaitingToBeDrawn.AddRange(usedCardsWaitingForReshuffle);
        usedCardsWaitingForReshuffle.Clear();
        Shuffle(cardsWaitingToBeDrawn);
    }

    /// <summary>첫 턴에는 덱을 초기화하고 이후 턴에는 남은 손패를 버린 뒤 새 손패를 뽑는다.</summary>
    private void HandlePlayerTurnStarted()
    {
        if (!battleCardCycleInitialized)
        {
            InitializeBattleCardCycle();
            return;
        }

        DiscardRemainingHand();
        DrawCardsUntilHandIsFull();
    }

    /// <summary>턴 종료 후 남아 있던 손패를 버림 더미로 이동한다.</summary>
    private void DiscardRemainingHand()
    {
        if (cardsInCurrentHand.Count == 0)
        {
            return;
        }

        // 손패에 남은 보너스(생성) 카드는 장착 덱 소속이 아니므로 버림 더미로 보내지 않는다.
        // 그대로 보내면 이후 턴에 장착하지 않은 카드가 계속 드로우되는 원인이 된다.
        for (int i = 0; i < cardsInCurrentHand.Count; i++)
            if (cardsInCurrentHand[i] >= 0 && !temporarilyGeneratedCardSlots.Contains(i))
                usedCardsWaitingForReshuffle.Add(cardsInCurrentHand[i]);
        cardsInCurrentHand.Clear();
        mpDiscountByHandSlot.Clear();
        temporarilyGeneratedCardSlots.Clear();
    }

    /// <summary>전투 게임 관리자가 준비되어 있으면 턴 시작 이벤트를 중복 없이 연결한다.</summary>
    private void TrySubscribeTurnManager()
    {
        if (BattleGameManager.Instance == null)
        {
            return;
        }

        BattleGameManager.Instance.PlayerTurnStarted -= HandlePlayerTurnStarted;
        BattleGameManager.Instance.PlayerTurnStarted += HandlePlayerTurnStarted;
    }

    /// <summary>피셔-예이츠 방식으로 전달된 카드 인덱스 목록을 제자리에서 무작위로 섞는다.</summary>
    private static void Shuffle(List<int> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (cards[i], cards[swapIndex]) = (cards[swapIndex], cards[i]);
        }
    }
}
