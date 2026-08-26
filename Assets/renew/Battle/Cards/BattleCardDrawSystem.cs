using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손패에서 카드를 선택한 순간의 정보를 사용 확정 시점까지 보관한다.
/// 대상 선택 중 손패가 바뀌었는지 확인할 수 있도록 손패 위치와 카드 인덱스를 함께 저장하며,
/// 확정 전에는 카드를 손패에서 제거하거나 MP를 소비하지 않는다.
/// </summary>
public sealed class SelectedCardUseInfo
{
    /// <summary>선택 당시 카드가 있던 손패 목록 위치. 확정 시 같은 위치인지 다시 검사한다.</summary>
    public int HandSlotIndex { get; }
    /// <summary>원본 카드와 선택 당시 카드를 식별하는 인덱스.</summary>
    public int CardIndex { get; }
    /// <summary>대상 유형, 범위 형태, 효과 목록처럼 카드 고유 규칙이 담긴 전투 데이터.</summary>
    public BattleCardData CardData { get; }
    /// <summary>카드 이름, 사거리, MP 비용, 기본 위력처럼 카드 사용에 공통으로 필요한 행동 정보.</summary>
    public BattleActionRequest ActionInfo { get; }

    /// <summary>선택 순간의 손패 위치·카드 데이터·할인 적용 행동 정보를 하나로 고정한다.</summary>
    internal SelectedCardUseInfo(
        int handIndex,
        int cardIndex,
        BattleCardData cardData,
        BattleActionRequest actionInfo)
    {
        HandSlotIndex = handIndex;
        CardIndex = cardIndex;
        CardData = cardData;
        ActionInfo = actionInfo;
    }
}

/// <summary>
/// 플레이어 전투 덱, 손패, 버림 더미와 턴 시작 드로우를 관리한다.
/// 카드 UI는 HandCardsChanged 이벤트와 선택 카드 정보 생성/사용 완료 API만 사용한다.
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

    // 아직 뽑지 않은 장착 카드 인덱스. 목록의 마지막 원소부터 한 장씩 손패로 이동한다.
    private readonly List<int> cardsInDrawPile = new List<int>();
    // 현재 손패 슬롯 순서와 동일한 카드 인덱스 목록. -1은 사용되어 비어 있는 슬롯이다.
    private readonly List<int> cardsInHand = new List<int>();
    // cardsInHand와 같은 위치를 사용하는 병렬 목록. 각 손패 슬롯의 MP 할인량을 저장한다.
    private readonly List<int> mpDiscountForEachHandSlot = new List<int>();
    // 사용했거나 턴이 끝날 때 남은 장착 카드. 드로우 더미가 비면 다시 섞여 이동한다.
    private readonly List<int> cardsInDiscardPile = new List<int>();
    // 버섯 효과처럼 장착 덱 밖에서 생성된 카드의 손패 위치. 버릴 때 덱으로 되돌아가지 않게 구분한다.
    private readonly HashSet<int> temporaryCardSlotsInHand = new HashSet<int>();
    // PlayerDeck의 장착 카드가 전투 드로우 더미로 복사됐는지 나타낸다.
    private bool isBattleDeckInitialized;

    /// <summary>앞으로 뽑을 카드가 들어 있는 드로우 더미.</summary>
    public IReadOnlyList<int> DrawPileCards => cardsInDrawPile;
    /// <summary>현재 화면에 표시되는 손패 카드.</summary>
    public IReadOnlyList<int> HandCards => cardsInHand;
    /// <summary>사용을 마치고 다음 재섞기를 기다리는 카드.</summary>
    public IReadOnlyList<int> DiscardPileCards => cardsInDiscardPile;
    /// <summary>전투 중 동시에 유지할 수 있는 최대 손패 수.</summary>
    public int HandLimit => handLimit;
    /// <summary>손패 UI가 효과·대상·범위 같은 Renew 전투 카드 정보를 읽을 때 사용한다.</summary>
    public BattleCardDatabase Database => battleCardDatabase;
    /// <summary>손패 UI가 기존 카드 이름·이미지 정보를 찾을 때 사용하는 원본 데이터베이스.</summary>
    public CardDatabase OriginalDatabase => originalCardDatabase;
    /// <summary>지정한 손패 슬롯이 덱에서 뽑은 카드가 아니라 효과로 임시 생성된 카드인지 확인한다.</summary>
    public bool IsTemporaryCardSlot(int handSlotIndex) => temporaryCardSlotsInHand.Contains(handSlotIndex);

    /// <summary>드로우·사용·버리기 등으로 현재 손패 구성이 달라졌을 때 새 손패를 전달한다.</summary>
    public event Action<IReadOnlyList<int>> HandCardsChanged;
    /// <summary>카드 한 장이 손패에 들어온 직후 해당 카드 인덱스를 전달한다. 개별 드로우 연출 연결용이다.</summary>
    public event Action<int> CardDrawn;

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
            BattleGameManager.Instance.PlayerTurnStarted -= RefreshHandForNewPlayerTurn;
        }
    }

    /// <summary>
    /// 플레이어 등록 과정에서 전달받은 실제 PlayerDeck을 사용해 전투 덱과 디버그 보유량을 초기화한다.
    /// 씬 검색으로 PlayerDeck을 대신 찾지 않으므로 호출자는 등록된 덱을 반드시 명시적으로 전달해야 한다.
    /// </summary>
    public void InitializeBattleCardCycle(PlayerDeck registeredPlayerDeck)
    {
        if (registeredPlayerDeck == null)
        {
            Debug.LogError(
                "전투 덱 초기화 실패: BattlePlayerRegistrationService에서 등록된 PlayerDeck을 전달하지 않았습니다.",
                this);
            return;
        }

        // 이전 전투나 재초기화 시점의 카드 위치 정보가 섞이지 않도록 모든 런타임 상태를 비운다.
        cardsInDrawPile.Clear();
        cardsInHand.Clear();
        mpDiscountForEachHandSlot.Clear();
        cardsInDiscardPile.Clear();
        temporaryCardSlotsInHand.Clear();

        // 이후 로직은 씬 검색 없이 플레이어 등록 서비스가 전달한 동일한 PlayerDeck만 사용한다.
        PlayerDeck sourceDeck = registeredPlayerDeck;
        if (ownAllCardsInDebugMode)
        {
            // 판매 QA용 보유량만 늘리며 실제 드로우 대상은 아래 장착 덱 복사 단계에서 결정한다.
            GrantEveryCardForShopTesting(sourceDeck);
        }
        // PlayerDeck의 장착 슬롯만 전투 드로우 더미로 복사한다.
        CopyEquippedCardsIntoDrawPile(sourceDeck);

        if (cardsInDrawPile.Count == 0)
        {
            Debug.LogError(
                "전투 덱 초기화 실패: PlayerDeck.deckCardforUI에 유효한 장착 카드가 없습니다. " +
                "보유 카드 전체를 임의 드로우 덱으로 사용하지 않습니다.",
                this);
        }

        if (shuffleAtBattleStart)
        {
            // 첫 손패가 장착 슬롯 순서대로 고정되지 않도록 전투 시작 시 한 번 섞는다.
            Shuffle(cardsInDrawPile);
        }

        // 이 시점부터 턴 시작 이벤트는 재초기화 대신 남은 손패 버리기와 재드로우를 수행한다.
        isBattleDeckInitialized = true;
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
    private void CopyEquippedCardsIntoDrawPile(PlayerDeck sourceDeck)
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
            cardsInDrawPile.Add(cardIndex);
        }
        Debug.Log($"장착 덱 복사 완료: deckCardforUI 기준 {cardsInDrawPile.Count}장", this);
    }

    /// <summary>
    /// 현재 손패가 최대 손패 수가 될 때까지 한 장씩 드로우한 뒤 완성된 손패를 UI에 한 번 전달한다.
    /// 개별 카드가 들어오는 순간은 TryDrawOneCard 내부의 CardDrawn 이벤트가 별도로 알린다.
    /// </summary>
    public void DrawCardsUntilHandIsFull()
    {
        while (cardsInHand.Count < handLimit)
        {
            // 드로우 더미와 재활용할 버림 더미가 모두 비었다면 더 채울 수 없으므로 반복을 끝낸다.
            if (!TryDrawOneCard())
            {
                break;
            }
        }

        // 카드별 이벤트와 별개로 최종 손패 목록은 드로우가 모두 끝난 후 한 번만 갱신한다.
        HandCardsChanged?.Invoke(cardsInHand);
    }

    /// <summary>
    /// 플레이어가 누른 손패 슬롯에서 카드 사용에 필요한 정보를 읽어 하나로 묶는다.
    /// 카드 데이터 조회와 손패 전용 MP 할인 계산까지만 수행하며,
    /// 카드를 손패에서 제거하거나 MP를 차감하지 않는다. 실제 소비는 사용 확정 후 별도 함수가 담당한다.
    /// </summary>
    /// <param name="handSlotIndex">플레이어가 선택한 손패 목록의 위치.</param>
    /// <param name="selectedCardInfo">성공 시 슬롯·카드 번호·카드 규칙·할인 적용 행동 정보를 반환한다.</param>
    /// <returns>카드 사용 정보를 정상적으로 만들었으면 true, 슬롯이나 카드 데이터가 잘못됐으면 false.</returns>
    public bool TryGetCardUseInfoFromHandSlot(int handSlotIndex, out SelectedCardUseInfo selectedCardInfo)
    {
        // 실패한 호출자가 이전 값을 실수로 재사용하지 않도록 출력값을 먼저 비운다.
        selectedCardInfo = null;
        // 손패 목록 바깥을 누른 입력은 카드 선택으로 처리할 수 없다.
        if (handSlotIndex < 0 || handSlotIndex >= cardsInHand.Count)
        {
            return false;
        }

        // 손패 슬롯에는 카드 객체 대신 원본 카드를 찾기 위한 정수 인덱스가 저장되어 있다.
        int selectedCardIndex = cardsInHand[handSlotIndex];
        // -1은 이미 사용되어 비어 있는 슬롯이므로 다시 선택할 수 없다.
        if (selectedCardIndex < 0)
        {
            return false;
        }
        // 카드 번호를 대상·범위·효과 목록이 담긴 Renew 전투 카드 데이터로 찾는다.
        BattleCardData cardData = battleCardDatabase != null
            ? battleCardDatabase.FindByLegacyCardIndex(selectedCardIndex)
            : null;

        // 기존 카드 이름/수치와 Renew 카드 규칙을 대상 선택 시스템이 공통으로 읽는 행동 정보로 변환한다.
        if (!BattleCardConnector.TryCreateActionRequest(
                cardData,
                originalCardDatabase,
                out BattleActionRequest actionInfo))
        {
            Debug.LogError($"카드 행동 정보 생성 실패: 전투 카드 인덱스 {selectedCardIndex}", this);
            return false;
        }

        // 할인 목록은 손패와 같은 위치를 사용한다. 일반 카드는 0, 현재 임시 버섯 카드는 1이다.
        int handSlotMpDiscount = handSlotIndex < mpDiscountForEachHandSlot.Count
            ? mpDiscountForEachHandSlot[handSlotIndex]
            : 0;
        if (handSlotMpDiscount > 0)
        {
            // BattleActionRequest는 읽기 전용이므로 이번 손패 카드에만 할인된 새 행동 정보를 만든다.
            actionInfo = new BattleActionRequest(
                actionInfo.DisplayName,
                actionInfo.ActionType,
                actionInfo.RangeTiles,
                Mathf.Max(0, actionInfo.MPCost - handSlotMpDiscount),
                actionInfo.Power);
        }

        // 확정 시 같은 카드인지 재검사할 수 있도록 슬롯 위치와 카드 번호도 함께 고정한다.
        selectedCardInfo = new SelectedCardUseInfo(
            handSlotIndex,
            selectedCardIndex,
            cardData,
            actionInfo);
        return true;
    }

    /// <summary>카드 행동 확정 후 해당 카드 한 장을 손패에서 제거하고 버림 더미로 보낸다.</summary>
    public bool TryMoveUsedCardToDiscardPile(SelectedCardUseInfo selectedCardInfo)
    {
        // 선택 정보가 없으면 어떤 손패 카드를 소비해야 하는지 판단할 수 없다.
        if (selectedCardInfo == null)
        {
            return false;
        }

        int handSlotIndex = selectedCardInfo.HandSlotIndex;
        // 대상 선택 도중 손패가 바뀌었을 수 있으므로 선택 당시 슬롯과 카드 번호를 모두 다시 확인한다.
        if (handSlotIndex < 0 ||
            handSlotIndex >= cardsInHand.Count ||
            cardsInHand[handSlotIndex] != selectedCardInfo.CardIndex)
        {
            return false;
        }

        // RemoveAt으로 카드를 당기지 않고 -1로 비워 두면 남은 카드 위치가 바뀌지 않으며,
        // UI는 다음 플레이어 턴 전까지 사용한 카드 자리를 어둡게 유지할 수 있다.
        // 약초 버섯 등으로 생성된 보너스 카드는 장착 덱 소속이 아니므로, 버림 더미로 보내면
        // MoveDiscardPileBackToDrawPile()을 통해 이후 턴에도 계속 드로우 덱을 돌며 나오게 된다.
        // 장착 덱 카드만 계속 순환하도록 보너스 카드는 사용 후 완전히 소멸시킨다.
        bool wasTemporaryCard = temporaryCardSlotsInHand.Contains(handSlotIndex);
        cardsInHand[handSlotIndex] = -1;
        temporaryCardSlotsInHand.Remove(handSlotIndex);
        if (handSlotIndex < mpDiscountForEachHandSlot.Count)
        {
            mpDiscountForEachHandSlot[handSlotIndex] = 0;
        }
        if (!wasTemporaryCard)
        {
            // 장착 덱에서 뽑은 일반 카드만 버림 더미로 보내 이후 덱 재순환에 포함한다.
            cardsInDiscardPile.Add(selectedCardInfo.CardIndex);
        }
        // 임시 생성 카드는 버림 더미에 들어가지 않았으므로 이 시점에 완전히 소멸한다.
        HandCardsChanged?.Invoke(cardsInHand);
        return true;
    }

    /// <summary>
    /// 카드 판매로 PlayerDeck의 장착 슬롯이 자동 해제된 뒤, 전투에 남은 같은 카드 수를 실제 장착 수에 맞춘다.
    /// 드로우 더미 → 버림 더미 → 손패 순서로 초과 카드만 제거하며 임시 생성 카드는 장착 덱 수에 포함하지 않는다.
    /// 판매는 장착 수를 늘리지 않으므로 이 함수는 부족한 카드를 새로 추가하지 않는다.
    /// </summary>
    public void RemoveRuntimeCardCopiesAboveEquippedCount(int cardIndex, int equippedCardCount)
    {
        // 보유량이 아니라 PlayerDeck.EquippedCards에 실제로 남은 장수를 기준으로 제거량을 계산한다.
        int copiesToRemove = CountEquippedCardCopiesInBattle(cardIndex) - Mathf.Max(0, equippedCardCount);
        // 아직 화면에 나오지 않은 카드부터 제거해 현재 손패 변화가 최소화되도록 한다.
        while (copiesToRemove > 0 && cardsInDrawPile.Remove(cardIndex)) copiesToRemove--;
        // 드로우 더미만으로 부족하면 이미 사용된 카드가 있는 버림 더미에서 제거한다.
        while (copiesToRemove > 0 && cardsInDiscardPile.Remove(cardIndex)) copiesToRemove--;

        bool handChanged = false;
        // 드로우·버림 더미에서 모두 제거하고도 초과분이 남았다면 현재 화면의 손패에서도 제거해야 한다.
        // 마지막 슬롯부터 역순으로 검사하면 앞쪽에 배치된 카드 위치를 최대한 유지할 수 있다.
        for (int handSlotIndex = cardsInHand.Count - 1;
             handSlotIndex >= 0 && copiesToRemove > 0;
             handSlotIndex--)
        {
            // 판매한 카드 번호와 다른 슬롯은 제거 대상이 아니다.
            // 같은 카드 번호라도 버섯 효과 등으로 임시 생성된 카드는 PlayerDeck 장착 카드가 아니므로 건너뛴다.
            if (cardsInHand[handSlotIndex] != cardIndex ||
                temporaryCardSlotsInHand.Contains(handSlotIndex))
            {
                continue;
            }

            // RemoveAt을 사용하면 뒤 카드들의 슬롯 번호가 전부 바뀌므로, 현재 슬롯만 -1로 표시해 비운다.
            cardsInHand[handSlotIndex] = -1;
            // 카드가 사라진 슬롯에 MP 할인만 남지 않도록 같은 위치의 할인값도 함께 초기화한다.
            if (handSlotIndex < mpDiscountForEachHandSlot.Count)
            {
                mpDiscountForEachHandSlot[handSlotIndex] = 0;
            }
            // 실제 손패가 바뀌었음을 기록해 반복이 끝난 뒤 UI 갱신 이벤트를 한 번만 호출한다.
            handChanged = true;
            // 장착 수보다 많았던 카드 한 장을 제거했으므로 남은 제거 목표를 한 장 줄인다.
            copiesToRemove--;
        }
        // 손패에서 실제 제거가 있었을 때만 최종 손패 목록을 UI에 전달한다.
        // 드로우·버림 더미만 바뀐 경우 화면의 손패는 같으므로 불필요한 UI 갱신을 하지 않는다.
        if (handChanged)
        {
            HandCardsChanged?.Invoke(cardsInHand);
        }
    }

    /// <summary>
    /// 전투의 드로우·버림 더미와 손패에 남은 특정 장착 카드 수를 합산한다.
    /// 카드 효과로 생성된 임시 손패 카드는 PlayerDeck 장착 덱 소속이 아니므로 계산에서 제외한다.
    /// </summary>
    private int CountEquippedCardCopiesInBattle(int cardIndex)
    {
        int battleEquippedCardCount = 0;

        // 아직 뽑지 않은 드로우 더미에서 같은 카드 번호가 몇 장 남아 있는지 센다.
        // 같은 카드는 최대 보유 수량만큼 중복될 수 있으므로 발견할 때마다 한 장씩 더한다.
        foreach (int drawPileCardIndex in cardsInDrawPile)
        {
            if (drawPileCardIndex == cardIndex)
            {
                battleEquippedCardCount++;
            }
        }

        // 이미 사용했지만 덱이 비면 다시 섞일 버림 더미에서도 같은 카드 번호를 센다.
        foreach (int discardPileCardIndex in cardsInDiscardPile)
        {
            if (discardPileCardIndex == cardIndex)
            {
                battleEquippedCardCount++;
            }
        }

        // 현재 손패는 카드 효과로 생성된 임시 카드가 섞일 수 있으므로 슬롯 위치까지 함께 검사한다.
        for (int handSlotIndex = 0; handSlotIndex < cardsInHand.Count; handSlotIndex++)
        {
            // 임시 생성 슬롯은 PlayerDeck 장착 덱 소속이 아니므로 판매 후 장착 수 비교에서 제외한다.
            // 일반 손패 슬롯이면서 카드 번호까지 같을 때만 장착 카드 한 장으로 계산한다.
            if (!temporaryCardSlotsInHand.Contains(handSlotIndex) &&
                cardsInHand[handSlotIndex] == cardIndex)
            {
                battleEquippedCardCount++;
            }
        }

        // 세 카드 위치에 흩어진 동일 장착 카드의 전체 장수를 호출자에게 반환한다.
        return battleEquippedCardCount;
    }

    /// <summary>
    /// 사용한 버섯 카드의 손패 자리에 무작위 임시 카드를 생성하고 이번 턴에만 MP 비용을 1 낮춘다.
    /// 후보 카드를 기존 데이터의 희귀도 문자열별로 묶은 뒤 희귀도 하나, 해당 희귀도의 카드 하나를 차례로 무작위 선택한다.
    /// 생성 카드는 PlayerDeck 장착 덱에 추가하지 않으며 사용하거나 턴이 끝나면 완전히 소멸한다.
    /// </summary>
    public bool GenerateWeirdMushroomCard(SelectedCardUseInfo usedMushroomCardInfo)
    {
        // 사용한 버섯 정보가 없으면 자기 자신을 후보에서 제외하거나 생성 위치를 결정할 수 없다.
        // 전투 카드 DB가 없으면 무작위 생성 후보 자체를 만들 수 없다.
        if (usedMushroomCardInfo == null || battleCardDatabase == null)
        {
            return false;
        }

        // Key는 기존 카드 데이터에 저장된 희귀도 문자열이고 Value는 그 희귀도에 속한 카드 인덱스 목록이다.
        // 예: "common" -> [0, 2, 5], "rare" -> [1, 7]
        Dictionary<string, List<int>> cardIndexesGroupedByRarity =
            new Dictionary<string, List<int>>();

        // 전투 카드 DB 전체를 순회하면서 생성 가능한 카드를 희귀도별 목록으로 분류한다.
        foreach (BattleCardData card in battleCardDatabase.Cards)
        {
            // 희귀도는 Renew BattleCardData가 아니라 기존 CardData에 문자열로 저장되어 있어 원본을 함께 찾는다.
            CardData original = card != null
                ? BattleCardConnector.FindOriginalCard(card.legacyCardIndex, originalCardDatabase)
                : null;

            // 비어 있거나 유효한 인덱스가 없는 카드, 방금 사용한 버섯 자기 자신,
            // 기존 데이터가 없어 희귀도를 알 수 없는 카드는 생성 후보에서 제외한다.
            if (card == null || card.legacyCardIndex < 0 ||
                card.legacyCardIndex == usedMushroomCardInfo.CardIndex ||
                original == null)
            {
                continue;
            }

            // 기존 rare 값이 null·빈 문자열·공백이면 분류에서 누락되지 않도록 일반 등급으로 취급한다.
            // 현재 CardData.rare가 enum이 아닌 string이므로 "common"도 문자열로 사용한다.
            string cardRarity = string.IsNullOrWhiteSpace(original.rare)
                ? "common"
                : original.rare;

            // 이 희귀도가 처음 등장했다면 카드 인덱스를 담을 빈 목록을 만들어 딕셔너리에 등록한다.
            if (!cardIndexesGroupedByRarity.TryGetValue(
                    cardRarity,
                    out List<int> cardIndexesForThisRarity))
            {
                cardIndexesForThisRarity = new List<int>();
                cardIndexesGroupedByRarity.Add(cardRarity, cardIndexesForThisRarity);
            }

            // 현재 카드 번호를 해당 희귀도의 무작위 생성 후보에 추가한다.
            cardIndexesForThisRarity.Add(card.legacyCardIndex);
        }

        // 유효한 후보가 하나도 없으면 랜덤 선택을 수행할 수 없다.
        if (cardIndexesGroupedByRarity.Count == 0)
        {
            return false;
        }

        // 먼저 존재하는 희귀도 종류 중 하나를 동일 확률로 선택한다.
        // 카드 개수 비례가 아니므로 common 20장, rare 2장이어도 희귀도 선택 확률은 각각 50%다.
        List<string> availableRarities = new List<string>(cardIndexesGroupedByRarity.Keys);
        string randomlySelectedRarity =
            availableRarities[UnityEngine.Random.Range(0, availableRarities.Count)];

        // 선택된 희귀도에 속한 카드 번호 목록에서 최종 생성 카드 한 장을 동일 확률로 선택한다.
        List<int> cardIndexesInSelectedRarity =
            cardIndexesGroupedByRarity[randomlySelectedRarity];
        int generatedCardIndex = cardIndexesInSelectedRarity[
            UnityEngine.Random.Range(0, cardIndexesInSelectedRarity.Count)];

        // 방금 사용한 버섯이 있던 손패 위치를 새 카드가 들어갈 유효한 슬롯 범위로 제한한다.
        int mushroomHandSlotIndex = Mathf.Clamp(
            usedMushroomCardInfo.HandSlotIndex,
            0,
            Mathf.Max(0, cardsInHand.Count - 1));

        // 정상 사용 흐름에서는 버섯 소비 단계가 이 슬롯을 -1로 비워 두므로 같은 자리에 새 카드를 채운다.
        if (mushroomHandSlotIndex < cardsInHand.Count && cardsInHand[mushroomHandSlotIndex] < 0)
        {
            cardsInHand[mushroomHandSlotIndex] = generatedCardIndex;
            mpDiscountForEachHandSlot[mushroomHandSlotIndex] = 1;
        }
        else
        {
            // 보완 경로: 버섯 자리가 비어 있지 않으면 같은 위치에 새 슬롯을 삽입한다.
            // 이 경로는 기존 임시 슬롯 번호를 밀 수 있으므로 CARD-HAND-04 기술부채로 추적한다.
            cardsInHand.Insert(mushroomHandSlotIndex, generatedCardIndex);
            mpDiscountForEachHandSlot.Insert(mushroomHandSlotIndex, 1);
        }

        // 이 슬롯은 장착 덱 소속이 아니므로 판매 수량 계산과 버림 더미 재순환에서 제외한다.
        temporaryCardSlotsInHand.Add(mushroomHandSlotIndex);
        // 생성된 카드와 할인 상태가 즉시 화면에 반영되도록 최종 손패를 한 번 전달한다.
        HandCardsChanged?.Invoke(cardsInHand);
        return true;
    }

    /// <summary>
    /// 드로우 더미의 마지막 카드 한 장을 손패로 이동한다.
    /// 드로우 더미가 비었으면 먼저 버림 더미를 섞어 재사용하며, 양쪽 모두 비었으면 false를 반환한다.
    /// 성공 시 카드 슬롯과 MP 할인 슬롯을 같은 위치에 추가하고 CardDrawn 이벤트로 뽑힌 카드 인덱스를 알린다.
    /// </summary>
    private bool TryDrawOneCard()
    {
        // 아직 뽑지 않은 카드가 없다면, 사용을 마친 카드가 모인 버림 더미를 새 드로우 더미로 바꿔 본다.
        // MoveDiscardPileBackToDrawPile은 버림 더미가 비어 있으면 아무것도 하지 않고 그대로 돌아온다.
        if (cardsInDrawPile.Count == 0)
        {
            MoveDiscardPileBackToDrawPile();
        }

        // 재활용을 시도한 뒤에도 0장이면 드로우 더미와 버림 더미가 모두 비어 있다는 뜻이다.
        // 호출한 DrawCardsUntilHandIsFull은 false를 받으면 손패 채우기 반복을 중단한다.
        if (cardsInDrawPile.Count == 0)
        {
            return false;
        }

        // List의 유효한 인덱스는 0부터 Count - 1까지이므로 Count - 1이 마지막 카드의 위치다.
        // 이 시스템은 목록의 마지막 원소를 실제 카드 덱의 맨 위 카드로 정하고 그 위치부터 뽑는다.
        int lastIndex = cardsInDrawPile.Count - 1;
        // 드로우 더미의 맨 위에 있던 카드 번호를 읽는다.
        int drawnCardIndex = cardsInDrawPile[lastIndex];
        // 같은 카드가 드로우 더미와 손패에 동시에 존재하지 않도록 원래 더미에서는 제거한다.
        cardsInDrawPile.RemoveAt(lastIndex);
        // 뽑은 카드 번호를 손패의 마지막 슬롯에 추가한다.
        cardsInHand.Add(drawnCardIndex);

        // MP 할인 목록은 cardsInHand와 같은 슬롯 번호를 사용하는 병렬 목록이다.
        // 일반 드로우 카드는 할인 효과가 없으므로 새 카드와 같은 위치에 기본값 0을 추가한다.
        // 예: cardsInHand[2]의 비용 할인은 mpDiscountForEachHandSlot[2]에서 읽는다.
        mpDiscountForEachHandSlot.Add(0);

        // 한 장의 드로우가 끝난 순간을 카드 연출이나 로그가 받을 수 있도록 뽑힌 카드 번호를 알린다.
        // 전체 손패 UI 갱신은 여러 장을 모두 뽑은 뒤 DrawCardsUntilHandIsFull이 별도로 한 번 호출한다.
        CardDrawn?.Invoke(drawnCardIndex);
        // 카드 한 장이 정상적으로 손패에 들어갔음을 호출자에게 반환한다.
        return true;
    }

    /// <summary>드로우 덱이 비었을 때 버림 더미를 옮겨 섞고 새로운 드로우 덱으로 만든다.</summary>
    private void MoveDiscardPileBackToDrawPile()
    {
        // 버림 더미까지 비어 있으면 새 드로우 더미로 옮길 카드가 없다.
        // 빈 목록도 AddRange와 Shuffle 자체는 가능하지만, 아무 작업도 필요 없다는 조건을 여기서 명확히 끝낸다.
        if (cardsInDiscardPile.Count == 0)
        {
            return;
        }

        // 사용을 마친 모든 장착 카드를 새 드로우 더미로 되돌린다.
        cardsInDrawPile.AddRange(cardsInDiscardPile);
        // 같은 카드가 두 더미에 동시에 존재하지 않도록 이동이 끝난 버림 더미를 비운다.
        cardsInDiscardPile.Clear();
        // 매 순환마다 같은 순서로 다시 뽑히지 않도록 새 드로우 더미를 섞는다.
        Shuffle(cardsInDrawPile);
    }

    /// <summary>
    /// 새 플레이어 턴이 시작되면 이전 턴에 남은 일반 카드를 버림 더미로 옮기고 손패 제한까지 다시 뽑는다.
    /// 전투 덱 최초 구성은 이 함수가 아니라 BattlePlayerRegistrationService의 PlayerDeck 등록 단계에서 끝나 있어야 한다.
    /// </summary>
    private void RefreshHandForNewPlayerTurn()
    {
        if (!isBattleDeckInitialized)
        {
            Debug.LogError(
                "턴 시작 드로우 실패: PlayerDeck 등록보다 먼저 플레이어 턴이 시작되었습니다. " +
                "BattlePlayerRegistrationService의 등록 순서를 확인하세요.",
                this);
            return;
        }

        // 이전 턴에 사용하지 않고 남긴 장착 카드만 버림 더미로 이동한다. 임시 생성 카드는 소멸한다.
        MoveRemainingHandToDiscardPile();
        // 빈 손패를 최대 손패 수까지 채운 뒤 최종 목록을 UI에 전달한다.
        DrawCardsUntilHandIsFull();
    }

    /// <summary>새 턴 직전 남아 있던 장착 카드는 버림 더미로 옮기고 임시 생성 카드는 제거한다.</summary>
    private void MoveRemainingHandToDiscardPile()
    {
        if (cardsInHand.Count == 0)
        {
            return;
        }

        // 손패에 남은 보너스(생성) 카드는 장착 덱 소속이 아니므로 버림 더미로 보내지 않는다.
        // 그대로 보내면 이후 턴에 장착하지 않은 카드가 계속 드로우되는 원인이 된다.
        for (int i = 0; i < cardsInHand.Count; i++)
            if (cardsInHand[i] >= 0 && !temporaryCardSlotsInHand.Contains(i))
                cardsInDiscardPile.Add(cardsInHand[i]);
        cardsInHand.Clear();
        mpDiscountForEachHandSlot.Clear();
        temporaryCardSlotsInHand.Clear();
    }

    /// <summary>전투 게임 관리자가 준비되어 있으면 턴 시작 이벤트를 중복 없이 연결한다.</summary>
    private void TrySubscribeTurnManager()
    {
        if (BattleGameManager.Instance == null)
        {
            return;
        }

        BattleGameManager.Instance.PlayerTurnStarted -= RefreshHandForNewPlayerTurn;
        BattleGameManager.Instance.PlayerTurnStarted += RefreshHandForNewPlayerTurn;
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
