using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player가 영구적으로 보유한 카드 수량과 전투에 장착한 카드 10칸을 관리한다.
/// 전투 중 드로우 더미·손패·버림 더미는 BattleCardDrawSystem이 별도로 관리하며,
/// 이 컴포넌트는 카드를 섞거나 뽑거나 사용하는 전투 규칙을 실행하지 않는다.
/// </summary>
public class PlayerDeck : MonoBehaviour
{
    public const int MaximumOwnedCopiesPerCard = 2;
    public const int DefaultEquippedSlotCount = 10;

    // 기존 UI와 상점 코드를 단계적으로 API 호출로 교체하기 전까지 공개 필드를 유지한다.
    // 새 코드는 이 Dictionary를 직접 수정하지 말고 아래의 보유 카드 API를 사용해야 한다.
    public Dictionary<int, int> cardPool = new Dictionary<int, int>();

    // 기존 Scene 직렬화와 UI 참조를 보존하기 위해 이름을 유지한다.
    // 값은 카드 데이터베이스 인덱스이며 -1은 비어 있는 장착 슬롯을 뜻한다.
    public int[] deckCardforUI = new int[DefaultEquippedSlotCount];

    /// <summary>카드 획득·판매로 보유 수량이 변경된 뒤 UI와 저장 계층에 알린다.</summary>
    public event Action OwnedCardsChanged;

    /// <summary>장착 카드 슬롯이 변경된 뒤 덱 편집 UI와 다음 전투 준비 계층에 알린다.</summary>
    public event Action EquippedDeckChanged;

    /// <summary>보유 카드 Dictionary를 외부에서 수정하지 않고 조회할 때 사용하는 읽기 전용 경로.</summary>
    public IReadOnlyDictionary<int, int> OwnedCards => cardPool;

    /// <summary>현재 장착된 카드 슬롯을 DrawSystem과 UI가 읽을 때 사용하는 읽기 전용 경로.</summary>
    public IReadOnlyList<int> EquippedCards => deckCardforUI;

    /// <summary>
    /// 저장 데이터가 없는 신규 Player에게 시작 카드를 지급하고 장착 덱을 구성한다.
    /// 예를 들어 5종·각 2장을 전달하면 0,0,1,1,2,2,3,3,4,4 순서로 10칸을 채운다.
    /// 저장 데이터 복원은 별도 단계에서 연결하므로 기존 데이터를 불러온 뒤 이 함수를 호출하면 안 된다.
    /// </summary>
    public void InitializeDefaultCards(int startingCardTypeCount, int copiesPerCard) // 시작 카드 데이터 채워 넣기용
    {
        cardPool.Clear();
        ResetEveryEquippedSlotToEmpty(false);

        int safeTypeCount = Mathf.Max(0, startingCardTypeCount);
        int safeCopies = Mathf.Clamp(copiesPerCard, 0, MaximumOwnedCopiesPerCard);
        for (int cardIndex = 0; cardIndex < safeTypeCount; cardIndex++)
        {
            IncreaseOwnedCardCountWithoutEvent(cardIndex, safeCopies);
            for (int copy = 0; copy < safeCopies; copy++)
            {
                TryPlaceCardInFirstEmptyEquippedSlot(cardIndex);
            }
        }

        OwnedCardsChanged?.Invoke();
        EquippedDeckChanged?.Invoke();
    }

    /// <summary>지정한 카드의 현재 보유 수량을 반환한다. 보유하지 않은 카드는 0이다.</summary>
    public int GetOwnedCardCount(int cardIndex) // 카드 개수 반환, player deck, invetorysetting, battlecarddrawsystem 등에서 사용
    {
        return cardPool.TryGetValue(cardIndex, out int ownedCount) ? ownedCount : 0;
    }

    /// <summary>지정한 카드를 한 장 이상 보유하고 있는지 반환한다.</summary>
    public bool HasCard(int cardIndex) // 카드 보유 체크, shopsystem에서 사용됨
    {
        return GetOwnedCardCount(cardIndex) > 0;
    }

    /// <summary>
    /// 상점 구매나 보상으로 얻은 카드를 보유 목록에 추가한다.
    /// 현재 게임 규칙에 따라 같은 카드는 최대 두 장까지만 보유하며 장착 덱은 자동 변경하지 않는다.
    /// </summary>
    public void AddOwnedCard(int cardIndex, int amount = 1) // 카드 획득, shop chest에서 사용
    {
        if (cardIndex < 0 || amount <= 0) return; // 없는 카드 or 수량 0이면 return

        int before = GetOwnedCardCount(cardIndex);
        IncreaseOwnedCardCountWithoutEvent(cardIndex, amount);
        if (GetOwnedCardCount(cardIndex) != before)
        {
            OwnedCardsChanged?.Invoke();
        }
    }

    /// <summary>
    /// 판매 등으로 카드 보유 수량을 감소시킨다.
    /// 판매 후 보유 수량보다 많이 장착된 카드는 뒤쪽 슬롯부터 자동 해제한다.
    /// </summary>
    public bool TryRemoveOwnedCard(int cardIndex, int amount, out int remainingCount) // 카드 판매, shop에서만 호출
    {
        remainingCount = GetOwnedCardCount(cardIndex);
        if (cardIndex < 0 || amount <= 0 || remainingCount < amount) return false;

        int nextCount = remainingCount - amount;
        bool equippedDeckChanged = RemoveExcessEquippedCopies(cardIndex, nextCount);

        if (nextCount <= 0) cardPool.Remove(cardIndex);
        else cardPool[cardIndex] = nextCount;

        remainingCount = nextCount;
        OwnedCardsChanged?.Invoke();
        if (equippedDeckChanged) EquippedDeckChanged?.Invoke();
        return true;
    }

    /// <summary>장착 덱 10칸에 특정 카드가 몇 장 들어 있는지 계산한다.</summary>
    public int GetEquippedCopyCount(int cardIndex) // deck 시스템에서만 호출함
    {
        int count = 0;
        foreach (int equippedCardIndex in deckCardforUI)
        {
            if (equippedCardIndex == cardIndex) count++;
        }
        return count;
    }


    /// <summary>
    /// 인벤토리에서 편집을 끝낸 장착 덱 10칸을 한 번에 검증하고 적용한다.
    /// 슬롯별로 즉시 교체하면 카드 위치를 맞바꾸는 도중 일시적으로 보유 수량을 초과할 수 있으므로,
    /// 전체 요청의 카드별 사용 수량을 먼저 계산한 뒤 유효할 때만 기존 배열을 교체한다.
    /// </summary>
    public bool TryReplaceEntireEquippedDeck(IReadOnlyList<int> requestedDeck, out string failureReason) // inventory에서 사용
    {
        failureReason = string.Empty;
        if (requestedDeck == null || requestedDeck.Count != DefaultEquippedSlotCount)
        {
            failureReason = $"장착 덱은 정확히 {DefaultEquippedSlotCount}칸이어야 합니다.";
            return false;
        }

        Dictionary<int, int> requestedCopies = new Dictionary<int, int>();
        for (int slotIndex = 0; slotIndex < requestedDeck.Count; slotIndex++)
        {
            int cardIndex = requestedDeck[slotIndex];
            if (cardIndex < 0) continue;

            int nextCopies = requestedCopies.TryGetValue(cardIndex, out int currentCopies)
                ? currentCopies + 1
                : 1;
            if (nextCopies > GetOwnedCardCount(cardIndex))
            {
                failureReason = $"카드 {cardIndex}의 장착 수량이 보유 수량을 초과합니다.";
                return false;
            }
            requestedCopies[cardIndex] = nextCopies;
        }

        for (int slotIndex = 0; slotIndex < requestedDeck.Count; slotIndex++)
        {
            deckCardforUI[slotIndex] = requestedDeck[slotIndex];
        }

        EquippedDeckChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 이벤트를 보내지 않고 보유 수량만 증가시킨다.
    /// 시작 카드 여러 장을 한꺼번에 구성하거나, 공개 AddOwnedCard가 변경 전후를 비교할 때 사용한다.
    /// </summary>
    private void IncreaseOwnedCardCountWithoutEvent(int cardIndex, int amount) // 카드 수량 증가, InitializeDefaultCards에서만 사용, deck에서만 호출
    {
        if (cardIndex < 0 || amount <= 0) return;

        int nextCount = Mathf.Clamp(
            GetOwnedCardCount(cardIndex) + amount,
            0,
            MaximumOwnedCopiesPerCard);
        if (nextCount > 0) cardPool[cardIndex] = nextCount;
    }

    /// <summary>
    /// 시작 덱 구성 중 첫 번째 빈 장착 슬롯에 카드를 넣는다.
    /// 플레이어가 보유한 수량보다 많이 장착하려는 경우와 빈 슬롯이 없는 경우에는 실패한다.
    /// </summary>
    private bool TryPlaceCardInFirstEmptyEquippedSlot(int cardIndex) //deck에서만 호출
    {
        if (GetEquippedCopyCount(cardIndex) >= GetOwnedCardCount(cardIndex)) return false;

        for (int slotIndex = 0; slotIndex < deckCardforUI.Length; slotIndex++)
        {
            if (deckCardforUI[slotIndex] >= 0) continue;

            deckCardforUI[slotIndex] = cardIndex;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 카드 판매 후 남길 수 있는 장착 수량을 초과한 만큼 뒤쪽 슬롯부터 비운다.
    /// 예를 들어 두 장을 장착한 카드를 한 장 판매하면 해당 카드 슬롯 하나만 -1이 된다.
    /// </summary>
    private bool RemoveExcessEquippedCopies(int cardIndex, int maximumEquippedCopies) //deck에서만 호출
    {
        int copiesToRemove = GetEquippedCopyCount(cardIndex) - Mathf.Max(0, maximumEquippedCopies);
        if (copiesToRemove <= 0) return false;

        for (int slotIndex = deckCardforUI.Length - 1; slotIndex >= 0 && copiesToRemove > 0; slotIndex--)
        {
            if (deckCardforUI[slotIndex] != cardIndex) continue;

            deckCardforUI[slotIndex] = -1;
            copiesToRemove--;
        }
        return true;
    }

    /// <summary>장착 배열을 10칸으로 맞추고 모든 슬롯 값을 빈 슬롯 표시인 -1로 초기화한다.</summary>
    private void ResetEveryEquippedSlotToEmpty(bool notifyChanged)
    {
        if (deckCardforUI == null || deckCardforUI.Length != DefaultEquippedSlotCount)
        {
            deckCardforUI = new int[DefaultEquippedSlotCount];
        }

        for (int slotIndex = 0; slotIndex < deckCardforUI.Length; slotIndex++)
        {
            deckCardforUI[slotIndex] = -1;
        }

        if (notifyChanged) EquippedDeckChanged?.Invoke();
    }

}
