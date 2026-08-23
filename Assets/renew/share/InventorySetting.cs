using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySetting : MonoBehaviour
{
    [SerializeField]
    nowDeckCard[] myCards; //DataConfig의 cardData의 정보를 받아옴.
    [SerializeField]
    InventoryCard[] invenCards;
    [SerializeField]
    ScrollRect scroll;
    [SerializeField]
    PlayerDeck playerDeck;

    // 현재 인벤토리 화면에서 변경 중인 덱 복사본이다. 카드를 클릭/드래그해서 바꾸는 동안에는 이 배열만 바뀌고,
    // "저장" 버튼(SaveDeck)을 눌러야 실제 전투에 쓰이는 playerDeck.deckCardforUI에 반영된다.
    // 저장하지 않고 화면을 닫았다가 다시 열면(OnEnable) 마지막으로 저장된 덱으로 되돌아간다.
    private int[] deckBeingEdited = new int[10];
    private bool deckEditingCopyLoaded;

    public void OnEnable()
    {
        CopyEquippedDeckIntoEditor();
        RefreshDeckEditorVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
    }

    /// <summary>패널을 열 때 실제 장착 덱을 편집 화면용 배열에 복사한다. 저장하지 않은 이전 편집 내용은 이때 폐기된다.</summary>
    private void CopyEquippedDeckIntoEditor()
    {
        if (playerDeck == null || playerDeck.deckCardforUI == null) return;

        int length = playerDeck.deckCardforUI.Length;
        if (deckBeingEdited == null || deckBeingEdited.Length != length)
        {
            deckBeingEdited = new int[length];
        }
        System.Array.Copy(playerDeck.deckCardforUI, deckBeingEdited, length);
        deckEditingCopyLoaded = true;
    }

    public void RefreshDeckEditorVisuals()
    {
        if (!deckEditingCopyLoaded) CopyEquippedDeckIntoEditor();

        SortDeckByCostDescending();
        SortInventoryByCostDescending();

        if (myCards != null)
        {
            for (int i = 0; i < myCards.Length; i++)
            {
                if (myCards[i] != null)
                {
                    myCards[i].Init();
                }
            }
        }

        if (invenCards != null)
        {
            for (int k = 0; k < invenCards.Length; k++)
            {
                if (invenCards[k] != null)
                {
                    invenCards[k].RefreshOwnershipAndEquippedCount();
                }
            }
        }
        Debug.Log("전부 초기화 완료");

    }

    /// <summary>장착 카드 슬롯을 누르면 편집 중인 덱에서 한 장을 제거하고 즉시 화면을 다시 그린다. 저장 전에는 PlayerDeck을 변경하지 않는다.</summary>
    public void RemoveCardFromDeckBeingEdited(int slotIndex)
    {
        if (!deckEditingCopyLoaded) CopyEquippedDeckIntoEditor();
        if (deckBeingEdited == null ||
            slotIndex < 0 || slotIndex >= deckBeingEdited.Length ||
            deckBeingEdited[slotIndex] < 0) return;

        deckBeingEdited[slotIndex] = -1;
        SortAndRefreshDeckBeingEdited();
    }

    /// <summary>보유 카드를 누르면 편집 중인 덱의 첫 빈 슬롯에 추가하고 즉시 화면을 다시 그린다. 저장 전에는 PlayerDeck을 변경하지 않는다.</summary>
    public bool TryAddOwnedCardToDeckBeingEdited(int cardIndex)
    {
        if (!deckEditingCopyLoaded) CopyEquippedDeckIntoEditor();
        if (deckBeingEdited == null || playerDeck == null) return false;

        int ownedCount = playerDeck.GetOwnedCardCount(cardIndex);
        if (ownedCount <= 0) return false;

        int usedCount = deckBeingEdited.Count(index => index == cardIndex);
        if (usedCount >= ownedCount) return false;

        int emptySlot = System.Array.IndexOf(deckBeingEdited, -1);
        if (emptySlot < 0) return false;

        deckBeingEdited[emptySlot] = cardIndex;
        SortAndRefreshDeckBeingEdited();
        return true;
    }

    /// <summary>편집용 임시 덱을 코스트가 높은 순서로 정렬하고 빈 슬롯은 뒤로 보낸다.</summary>
    private void SortDeckByCostDescending()
    {
        if (deckBeingEdited == null) return;
        CardDatabase database = DataPool.Instance != null ? DataPool.Instance.cardDatabase : null;
        if (database == null || database.cards == null) return;

        int[] sorted = deckBeingEdited
            .Where(index => index >= 0 && index < database.cards.Count && database.cards[index] != null)
            .OrderByDescending(index => database.cards[index].cardCost)
            .ThenBy(index => index)
            .ToArray();

        for (int i = 0; i < deckBeingEdited.Length; i++)
            deckBeingEdited[i] = i < sorted.Length ? sorted[i] : -1;
    }

    /// <summary>인벤토리 카드 오브젝트를 코스트가 높은 순서로 배치한다.</summary>
    private void SortInventoryByCostDescending()
    {
        if (invenCards == null) return;
        CardDatabase database = DataPool.Instance != null ? DataPool.Instance.cardDatabase : null;
        if (database == null || database.cards == null) return;

        invenCards = invenCards
            .Where(card => card != null)
            .OrderByDescending(card => card.GetCardCost(database))
            .ThenBy(card => card.CardIndex)
            .ToArray();

        if (invenCards.Length == 0) return;
        Transform commonParent = invenCards[0].transform.parent;
        if (commonParent == null || invenCards.Any(card => card.transform.parent != commonParent)) return;
        int firstSibling = invenCards.Min(card => card.transform.GetSiblingIndex());
        for (int i = 0; i < invenCards.Length; i++)
            invenCards[i].transform.SetSiblingIndex(firstSibling + i);
    }

    /// <summary>카드 추가·제거 후 편집 중인 덱을 코스트순으로 정렬하고 모든 덱·보유 카드 이미지를 갱신한다.</summary>
    private void SortAndRefreshDeckBeingEdited()
    {
        SortDeckByCostDescending();
        RefreshDeckEditorVisuals();
    }

    /// <summary>
    /// "저장" 버튼에 연결하는 함수다. 지금까지 편집한 덱을 실제 전투에 쓰이는 PlayerDeck.deckCardforUI에
    /// 반영한다. 전투 중 드로우 시스템(BattleCardDrawSystem)은 전투 시작 시점에만 deckCardforUI를 읽어
    /// 드로우 덱을 구성하므로, 저장한 덱은 진행 중인 전투가 아니라 다음 전투부터 적용된다.
    /// </summary>
    public void SaveDeck()
    {
        if (playerDeck == null || playerDeck.deckCardforUI == null || deckBeingEdited == null)
        {
            Debug.LogWarning("덱 저장 실패: PlayerDeck 참조가 없습니다.", this);
            return;
        }

        if (!playerDeck.TryReplaceEntireEquippedDeck(deckBeingEdited, out string failureReason))
        {
            Debug.LogWarning($"덱 저장 실패: {failureReason}", this);
            return;
        }

        DataConfig.cardData.Clear();
        foreach (int cardIndex in playerDeck.EquippedCards)
        {
            if (cardIndex >= 0) DataConfig.cardData.Add(cardIndex);
        }

        Debug.Log("덱 저장 완료: 다음 전투부터 적용됩니다.", this);
    }

    public void SetInventoryVerticalScrollEnabled(bool enabled)
    {
        scroll.vertical = enabled;
    }

    /// <summary>지정한 편집 슬롯에 현재 표시할 카드 인덱스를 반환한다. 빈 슬롯은 -1이다.</summary>
    public int GetCardIndexFromDeckBeingEdited(int slotIndex)
    {
        if (!deckEditingCopyLoaded) CopyEquippedDeckIntoEditor();
        if (deckBeingEdited == null || slotIndex < 0 || slotIndex >= deckBeingEdited.Length)
        {
            Debug.LogError($"플레이어 덱 참조 또는 슬롯이 올바르지 않습니다: 슬롯 {slotIndex}", this);
            return -1;
        }

        return deckBeingEdited[slotIndex];
    }

    /// <summary>드래그·드롭으로 선택한 편집 슬롯의 카드를 교체한다. 저장 전까지 실제 PlayerDeck에는 반영하지 않는다.</summary>
    public void ReplaceCardInDeckBeingEdited(int slotIndex, int replacementCardIndex)
    {
        if (!deckEditingCopyLoaded) CopyEquippedDeckIntoEditor();
        if (deckBeingEdited == null || slotIndex < 0 || slotIndex >= deckBeingEdited.Length) return;

        deckBeingEdited[slotIndex] = replacementCardIndex;
        SortAndRefreshDeckBeingEdited();
    }

    public (bool isOwned, int ownedCount) GetOwnedCardDisplayState(int cardIndex)
    {
        int ownedCount = playerDeck != null ? playerDeck.GetOwnedCardCount(cardIndex) : 0;
        return (ownedCount > 0, ownedCount);
    }

    /// <summary>각 카드가 편집 중인 덱에 몇 장 들어 있는지 UI가 계산할 때 사용하는 현재 편집 배열을 반환한다.</summary>
    public int[] GetDeckBeingEdited()
    {
        if (!deckEditingCopyLoaded) CopyEquippedDeckIntoEditor();
        return deckBeingEdited;
    }


    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            this.gameObject.SetActive(false);
        }
    }

}
