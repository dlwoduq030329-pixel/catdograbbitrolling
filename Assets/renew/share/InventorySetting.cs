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

    // 편집 중인 임시 덱이다. 인벤토리에서 카드를 클릭/드래그해서 바꾸는 동안에는 이 배열만 바뀌고,
    // "저장" 버튼(SaveDeck)을 눌러야 실제 전투에 쓰이는 playerDeck.deckCardforUI에 반영된다.
    // 저장하지 않고 화면을 닫았다가 다시 열면(OnEnable) 마지막으로 저장된 덱으로 되돌아간다.
    private int[] stagedDeck = new int[10];
    private bool stagedInitialized;

    public void OnEnable()
    {
        LoadStagedDeckFromPlayerDeck();
        InitAll();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
    }

    /// <summary>패널을 열 때(또는 저장 직후) 실제 전투 덱 값으로 편집용 임시 덱을 새로 채운다.</summary>
    private void LoadStagedDeckFromPlayerDeck()
    {
        if (playerDeck == null || playerDeck.deckCardforUI == null) return;

        int length = playerDeck.deckCardforUI.Length;
        if (stagedDeck == null || stagedDeck.Length != length)
        {
            stagedDeck = new int[length];
        }
        System.Array.Copy(playerDeck.deckCardforUI, stagedDeck, length);
        stagedInitialized = true;
    }

    public void InitAll()
    {
        if (!stagedInitialized) LoadStagedDeckFromPlayerDeck();

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

    /// <summary>사용 중인 카드 슬롯을 클릭하면 해당 카드 한 장을 편집용 임시 덱에서 제거한다(저장 전까지는 미리보기일 뿐).</summary>
    public void RemoveDeckCardAtSlot(int slotIndex)
    {
        if (!stagedInitialized) LoadStagedDeckFromPlayerDeck();
        if (stagedDeck == null ||
            slotIndex < 0 || slotIndex >= stagedDeck.Length ||
            stagedDeck[slotIndex] < 0) return;

        stagedDeck[slotIndex] = -1;
        ApplyStagedChange();
    }

    /// <summary>인벤토리 카드를 클릭하면 보유 수량 안에서 첫 빈 편집용 덱 슬롯에 한 장 추가한다(저장 전까지는 미리보기일 뿐).</summary>
    public bool TryAddCardToDeck(int cardIndex)
    {
        if (!stagedInitialized) LoadStagedDeckFromPlayerDeck();
        if (stagedDeck == null ||
            !DataConfig.CardsCount.TryGetValue(cardIndex, out int ownedCount)) return false;

        int usedCount = stagedDeck.Count(index => index == cardIndex);
        if (usedCount >= ownedCount) return false;

        int emptySlot = System.Array.IndexOf(stagedDeck, -1);
        if (emptySlot < 0) return false;

        stagedDeck[emptySlot] = cardIndex;
        ApplyStagedChange();
        return true;
    }

    /// <summary>편집용 임시 덱을 코스트가 높은 순서로 정렬하고 빈 슬롯은 뒤로 보낸다.</summary>
    private void SortDeckByCostDescending()
    {
        if (stagedDeck == null) return;
        CardDatabase database = DataPool.Instance != null ? DataPool.Instance.cardDatabase : null;
        if (database == null || database.cards == null) return;

        int[] sorted = stagedDeck
            .Where(index => index >= 0 && index < database.cards.Count && database.cards[index] != null)
            .OrderByDescending(index => database.cards[index].cardCost)
            .ThenBy(index => index)
            .ToArray();

        for (int i = 0; i < stagedDeck.Length; i++)
            stagedDeck[i] = i < sorted.Length ? sorted[i] : -1;
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

    /// <summary>편집용 임시 덱이 바뀔 때마다 정렬하고 화면을 다시 그린다. 실제 전투 데이터는 아직 바뀌지 않는다.</summary>
    private void ApplyStagedChange()
    {
        SortDeckByCostDescending();
        InitAll();
    }

    /// <summary>
    /// "저장" 버튼에 연결하는 함수다. 지금까지 편집한 덱을 실제 전투에 쓰이는 PlayerDeck.deckCardforUI에
    /// 반영한다. 전투 중 드로우 시스템(BattleCardDrawSystem)은 전투 시작 시점에만 deckCardforUI를 읽어
    /// 드로우 덱을 구성하므로, 저장한 덱은 진행 중인 전투가 아니라 다음 전투부터 적용된다.
    /// </summary>
    public void SaveDeck()
    {
        if (playerDeck == null || playerDeck.deckCardforUI == null || stagedDeck == null)
        {
            Debug.LogWarning("덱 저장 실패: PlayerDeck 참조가 없습니다.", this);
            return;
        }

        int length = Mathf.Min(playerDeck.deckCardforUI.Length, stagedDeck.Length);
        for (int i = 0; i < length; i++)
        {
            playerDeck.deckCardforUI[i] = stagedDeck[i];
        }

        DataConfig.cardData.Clear();
        foreach (int cardIndex in playerDeck.deckCardforUI)
        {
            if (cardIndex >= 0) DataConfig.cardData.Add(cardIndex);
        }

        Debug.Log("덱 저장 완료: 다음 전투부터 적용됩니다.", this);
    }

    public void SetScroll(bool temp)
    {
        scroll.vertical = temp;
    }

    /// <summary>편집 화면에 표시할 카드 인덱스를 반환한다(편집용 임시 덱 기준, 저장 전 값도 즉시 반영됨).</summary>
    public int returnCardIndex(int x)
    {
        if (!stagedInitialized) LoadStagedDeckFromPlayerDeck();
        if (stagedDeck == null || x < 0 || x >= stagedDeck.Length)
        {
            Debug.LogError($"플레이어 덱 참조 또는 슬롯이 올바르지 않습니다: 슬롯 {x}", this);
            return -1;
        }

        Debug.Log("return card Index : " + stagedDeck[x]);
        return stagedDeck[x];
    }

    /// <summary>드래그로 카드를 교체할 때 편집용 임시 덱만 바꾼다. 저장 전까지는 실제 전투 덱에 영향을 주지 않는다.</summary>
    public void ChangeCard(int index, int change)
    {
        if (!stagedInitialized) LoadStagedDeckFromPlayerDeck();
        if (stagedDeck == null || index < 0 || index >= stagedDeck.Length) return;

        stagedDeck[index] = change;
    }

    public (bool hasCard, int value) returnHaveCard(int x)
    {
        bool hasCard = !playerDeck.cardPool.ContainsKey(x); // 있으면 false 없으면 true
        int value = hasCard ? 0 :playerDeck.cardPool[x];

        return (hasCard, value);
    }

    /// <summary>인벤토리의 "사용 중" 표시에 쓰이는 편집용 임시 덱 배열이다(저장 여부와 무관하게 현재 편집 상태 기준).</summary>
    public int[] deckCardArr()
    {
        if (!stagedInitialized) LoadStagedDeckFromPlayerDeck();
        return stagedDeck;
    }


    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            this.gameObject.SetActive(false);
        }
    }

}
