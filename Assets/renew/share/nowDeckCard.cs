using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class nowDeckCard : MonoBehaviour, IPointerClickHandler
{
    [Header("DataConfig와 연결을 위한 index")]
    [SerializeField]
    int cardIndex;  //현재 덱 List의 index
    [Header("카드 자체의 Index값. 카드의 종류 결정")]
    [SerializeField]
    int thisIndex;

    Image cardSprite;
    [SerializeField]
    InventorySetting es;
    // Start is called before the first frame update
    void Start()
    {
    }

    public void Awake()
    {
        cardSprite = GetComponent<Image>();
      //  es = GetComponentInParent<InventorySetting>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Init()
    {
        if (cardSprite == null)
        {
            cardSprite = GetComponent<Image>();
        }

        if (es == null)
        {
            es = GetComponentInParent<InventorySetting>(true);
        }

        if (cardSprite == null || es == null)
        {
            Debug.LogError($"덱 카드 UI 참조가 없습니다: {name}", this);
            return;
        }

        int v = es.returnCardIndex(cardIndex);
        CardDatabase database = DataPool.Instance != null ? DataPool.Instance.cardDatabase : null;
        if (v < 0)
        {
            cardSprite.sprite = null;
            cardSprite.enabled = false;
            thisIndex = -1;
            return;
        }
        if (database == null || database.cards == null || v >= database.cards.Count || database.cards[v] == null)
        {
            Debug.LogError($"덱 카드 데이터를 찾을 수 없습니다: 슬롯 {cardIndex}, 카드 인덱스 {v}", this);
            return;
        }

        thisIndex = v;
        cardSprite.enabled = true;
        cardSprite.sprite = database.cards[thisIndex].myCardSprite;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (es == null) es = GetComponentInParent<InventorySetting>(true);
        es?.RemoveDeckCardAtSlot(cardIndex);
    }



    public void ChangeSet(int x)
    {
       // im.CardSet(cardIndex, x);
        cardSprite.sprite = DataPool.Instance.cardDatabase.cards[x].myCardSprite;
        es.ChangeCard(thisIndex, x);
        //DataConfig.AddCard(cardIndex,x);
        //im.SaveDeck();

    }

    public void Test()
    {
        Debug.Log("현재" + cardIndex + "의 위치에 존재.")
;    }


    public void OnEnable()
    {
        //Init();
    }
}
