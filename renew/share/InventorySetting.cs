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

    public void OnEnable()
    {
        InitAll();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
    }

    public void InitAll()
    {
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
                    invenCards[k].Apply();
                }
            }
        }
        Debug.Log("전부 초기화 완료");

    }

    /// <summary>사용 중인 카드 슬롯을 클릭하면 해당 카드 한 장을 덱에서 제거한다.</summary>
    public void RemoveDeckCardAtSlot(int slotIndex)
    {
        if (playerDeck == null || playerDeck.deckCardforUI == null ||
            slotIndex < 0 || slotIndex >= playerDeck.deckCardforUI.Length ||
            playerDeck.deckCardforUI[slotIndex] < 0) return;

        playerDeck.deckCardforUI[slotIndex] = -1;
        ApplyDeckChange();
    }

    /// <summary>인벤토리 카드를 클릭하면 보유 수량 안에서 첫 빈 덱 슬롯에 한 장 추가한다.</summary>
    public bool TryAddCardToDeck(int cardIndex)
    {
        if (playerDeck == null || playerDeck.deckCardforUI == null ||
            !DataConfig.CardsCount.TryGetValue(cardIndex, out int ownedCount)) return false;

        int usedCount = playerDeck.deckCardforUI.Count(index => index == cardIndex);
        if (usedCount >= ownedCount) return false;

        int emptySlot = System.Array.IndexOf(playerDeck.deckCardforUI, -1);
        if (emptySlot < 0) return false;

        playerDeck.deckCardforUI[emptySlot] = cardIndex;
        ApplyDeckChange();
        return true;
    }

    /// <summary>덱을 코스트가 높은 순서로 정렬하고 빈 슬롯은 뒤로 보낸다.</summary>
    private void SortDeckByCostDescending()
    {
        if (playerDeck == null || playerDeck.deckCardforUI == null) return;
        CardDatabase database = DataPool.Instance != null ? DataPool.Instance.cardDatabase : null;
        if (database == null || database.cards == null) return;

        int[] sorted = playerDeck.deckCardforUI
            .Where(index => index >= 0 && index < database.cards.Count && database.cards[index] != null)
            .OrderByDescending(index => database.cards[index].cardCost)
            .ThenBy(index => index)
            .ToArray();

        for (int i = 0; i < playerDeck.deckCardforUI.Length; i++)
            playerDeck.deckCardforUI[i] = i < sorted.Length ? sorted[i] : -1;
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

    private void ApplyDeckChange()
    {
        SortDeckByCostDescending();
        DataConfig.cardData.Clear();
        foreach (int cardIndex in playerDeck.deckCardforUI)
            if (cardIndex >= 0) DataConfig.cardData.Add(cardIndex);
        InitAll();
    }

    public void SetScroll(bool temp)
    {
        scroll.vertical = temp;
    }

    public int returnCardIndex(int x)
    {
        if (playerDeck == null || playerDeck.deckCardforUI == null || x < 0 || x >= playerDeck.deckCardforUI.Length)
        {
            Debug.LogError($"플레이어 덱 참조 또는 슬롯이 올바르지 않습니다: 슬롯 {x}", this);
            return -1;
        }

        Debug.Log("return card Index : " + playerDeck.deckCardforUI[x]);
        return playerDeck.deckCardforUI[x];
    }

    public void ChangeCard(int index,int change)
    {
        playerDeck.ChangeCard(index, change);
    }

    public (bool hasCard, int value) returnHaveCard(int x)
    {
        bool hasCard = !playerDeck.cardPool.ContainsKey(x); // 있으면 false 없으면 true
        int value = hasCard ? 0 :playerDeck.cardPool[x];

        return (hasCard, value);
    }

    public int[] deckCardArr()
    {
        return playerDeck.deckCardforUI;
    }


    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            this.gameObject.SetActive(false);
        }
    }

}
