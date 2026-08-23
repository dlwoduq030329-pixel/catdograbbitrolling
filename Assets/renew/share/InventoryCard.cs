using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using Coffee.UIEffects;

public class InventoryCard : MonoBehaviour,
    IPointerDownHandler,
    IPointerExitHandler,
    IPointerEnterHandler,
    IPointerUpHandler
{
    [SerializeField]
    [FormerlySerializedAs("index")]
    int cardIndex;
    [SerializeField]
    [FormerlySerializedAs("usingSP")]
    Sprite equippedCopyIcon;
    [SerializeField]
    [FormerlySerializedAs("noHaveSP")]
    Sprite notOwnedIcon;
    [SerializeField]
    [FormerlySerializedAs("haveSP")]
    Sprite ownedCopyIcon;
    [SerializeField]
    [FormerlySerializedAs("CardInfo")]
    GameObject cardInfoPanel;

    Image[] ownershipIndicators;
    Image selectionHighlight;
    Image cardArtwork;
    InventorySetting inventoryController;

    public int CardIndex
    {
        get
        {
            CacheCardUiReferences();
            return cardIndex;
        }
    }

    public int GetCardCost(CardDatabase database)
    {
        int cardIndex = CardIndex;
        return database != null && database.cards != null &&
            cardIndex >= 0 && cardIndex < database.cards.Count && database.cards[cardIndex] != null
            ? database.cards[cardIndex].cardCost
            : int.MinValue;
    }
    // Start is called before the first frame update
    void Start()
    {
        CacheCardUiReferences();
        if (DataPool.Instance == null || DataPool.Instance.cardDatabase == null)
        {
            Debug.LogError("인벤토리 카드 표시 실패: 원본 CardDatabase 참조가 없습니다.", this);
            return;
        }

        if (cardIndex < 0 || cardIndex >= DataPool.Instance.cardDatabase.cards.Count)
        {
            Debug.LogError($"인벤토리 카드 표시 실패: 인덱스 {cardIndex}가 DB 범위를 벗어났습니다.", this);
            return;
        }

        CardData cardData = DataPool.Instance.cardDatabase.cards[cardIndex];
        cardArtwork.sprite = CardArtResolver.ResolveDisplaySprite(cardData.myCardSprite);
        CardCostLabelView.Ensure(cardArtwork.transform)?.SetCost(cardData.cost, cardData.rare);

    }

    private void Awake()
    {
        inventoryController = GetComponentInParent<InventorySetting>();

        //Debug.Log(this.gameObject.name);
        CacheCardUiReferences();
        //Apply();
        //
    }

    public void CacheCardUiReferences()
    {
        if (selectionHighlight != null) return;

        string temp = this.gameObject.name;

        cardIndex = int.Parse(temp.Substring(4, 2));

        ownershipIndicators = new Image[2];

        Image[] tempIMG = GetComponentsInChildren<Image>(true);
        selectionHighlight = tempIMG[0];
        cardArtwork = tempIMG[1];
        ownershipIndicators[0] = tempIMG[2];
        ownershipIndicators[1] = tempIMG[3];
        ownershipIndicators[0].sprite = notOwnedIcon;
        ownershipIndicators[1].sprite = notOwnedIcon;

    }

    private void OnEnable()
    {
        //Debug.Log(DataPool.Instance.cardDatabase.cards[index].index);




    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        
        selectionHighlight.gameObject.SetActive(true);
        cardInfoPanel.GetComponent<CardInfo>().CardInfoSet(cardIndex);
        cardInfoPanel.gameObject.SetActive(true);
        //CardInfo.GetComponent<CanvasGroup>().alpha = 1.0f;
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        CacheCardUiReferences();
        if (inventoryController == null) inventoryController = GetComponentInParent<InventorySetting>();
        inventoryController?.TryAddOwnedCardToDeckBeingEdited(cardIndex);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        selectionHighlight.gameObject.SetActive(false);
        cardInfoPanel.gameObject.SetActive(false);
        //CardInfo.GetComponent<CanvasGroup>().alpha = 0f;
    }

    public void RefreshOwnershipAndEquippedCount()
    {
        CacheCardUiReferences();

        if(inventoryController == null)
        {
            //Debug.Log("es 없음");
            inventoryController = GetComponentInParent<InventorySetting>();

            //return;
        }

        // 이전 보유/장착 Sprite가 남지 않도록 보유 여부를 검사하기 전에 두 칸을 항상 비운다.
        // 카드 수가 0이 되어 CardsCount 키가 제거된 경우에도 UI 게이지가 정확히 0장으로 보인다.
        for (int i = 0; i < ownershipIndicators.Length; i++)
        {
            ownershipIndicators[i].sprite = notOwnedIcon;
        }

        (bool isOwned, int ownedCount) = inventoryController.GetOwnedCardDisplayState(cardIndex);
        if (isOwned)
        {
            cardArtwork.GetComponent<UIEffect>().toneFilter = ToneFilter.None;

            for (int i = 0; i < Mathf.Min(ownedCount, ownershipIndicators.Length); i++)
            {
                ownershipIndicators[i].sprite = ownedCopyIcon;
            }
            int equippedCount = CountCopiesInDeckBeingEdited(cardIndex);

            for (int i = 0; i < Mathf.Min(equippedCount, ownershipIndicators.Length); i++)
            {
                ownershipIndicators[i].sprite = equippedCopyIcon;
            }


        }
        else
        {
            cardArtwork.GetComponent<UIEffect>().toneFilter = ToneFilter.Grayscale;
        }

    }

    public int CountCopiesInDeckBeingEdited(int targetCardIndex)
    {
        int[] deckBeingEdited = inventoryController.GetDeckBeingEdited();

        int equippedCopyCount = 0;
        for (int i = 0; i < deckBeingEdited.Length; i++)
        {
            if (deckBeingEdited[i] == targetCardIndex)
            {
                equippedCopyCount++;
            }
        }
        return equippedCopyCount;
    }




}
