using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Coffee.UIEffects;

public class InventoryCard : MonoBehaviour,
    IPointerDownHandler,
    IPointerExitHandler,
    IPointerEnterHandler,
    IPointerUpHandler
{
    [SerializeField]
    int index;
    [SerializeField]
    Sprite usingSP;
    [SerializeField]
    Sprite noHaveSP;
    [SerializeField]
    Sprite haveSP;
    [SerializeField]
    GameObject tagedCard;
    [SerializeField]
    GameObject CardInfo;

    Image[] have;
    Image Choose;
    Image CardIMG;
    InventorySetting es;

    int usingCount;
    public int CardIndex
    {
        get
        {
            initInfo();
            return index;
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
        initInfo();
        if (DataPool.Instance == null || DataPool.Instance.cardDatabase == null)
        {
            Debug.LogError("인벤토리 카드 표시 실패: 원본 CardDatabase 참조가 없습니다.", this);
            return;
        }

        if (index < 0 || index >= DataPool.Instance.cardDatabase.cards.Count)
        {
            Debug.LogError($"인벤토리 카드 표시 실패: 인덱스 {index}가 DB 범위를 벗어났습니다.", this);
            return;
        }

        CardData cardData = DataPool.Instance.cardDatabase.cards[index];
        CardIMG.sprite = CardArtResolver.ResolveDisplaySprite(cardData.myCardSprite);
        CardCostLabelView.Ensure(CardIMG.transform)?.SetCost(cardData.cost, cardData.rare);

    }

    private void Awake()
    {
        es = GetComponentInParent<InventorySetting>();

        //Debug.Log(this.gameObject.name);
        initInfo();
        //Apply();
        //
    }

    public void initInfo()  
    {
        if (Choose != null) return;

        string temp = this.gameObject.name;

        index = int.Parse(temp.Substring(4, 2));

        have = new Image[2];

        Image[] tempIMG = GetComponentsInChildren<Image>(true);
        Choose = tempIMG[0];
        CardIMG = tempIMG[1];
        have[0] = tempIMG[2];
        have[1] = tempIMG[3];
        have[0].sprite = noHaveSP;
        have[1].sprite = noHaveSP;

    }

    private void OnEnable()
    {
        //Debug.Log(DataPool.Instance.cardDatabase.cards[index].index);




    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        
        Choose.gameObject.SetActive(true);
        CardInfo.GetComponent<CardInfo>().CardInfoSet(index);
        CardInfo.gameObject.SetActive(true);
        //CardInfo.GetComponent<CanvasGroup>().alpha = 1.0f;
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        initInfo();
        if (es == null) es = GetComponentInParent<InventorySetting>();
        es?.TryAddCardToDeck(index);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!DataConfig.CardsCount.ContainsKey(index)) return;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        Choose.gameObject.SetActive(false);
        CardInfo.gameObject.SetActive(false);
        //CardInfo.GetComponent<CanvasGroup>().alpha = 0f;
    }

    public void Apply()
    {
        initInfo();

        if(es == null)
        {
            //Debug.Log("es 없음");
            es = GetComponentInParent<InventorySetting>();

            //return;
        }

        // 이전 보유/장착 Sprite가 남지 않도록 보유 여부를 검사하기 전에 두 칸을 항상 비운다.
        // 카드 수가 0이 되어 CardsCount 키가 제거된 경우에도 UI 게이지가 정확히 0장으로 보인다.
        for (int i = 0; i < have.Length; i++)
        {
            have[i].sprite = noHaveSP;
        }

        if (!es.returnHaveCard(index).hasCard)
        {
            int dicCount = es.returnHaveCard(index).value;
            CardIMG.GetComponent<UIEffect>().toneFilter = ToneFilter.None;

            for (int i = 0; i < Mathf.Min(dicCount, have.Length); i++)
            {
                have[i].sprite = haveSP;
            }
            int listCount = retunListValue(index);

            for (int i = 0; i < Mathf.Min(listCount, have.Length); i++)
            {
                have[i].sprite = usingSP;
            }


        }
        else
        {
            CardIMG.GetComponent<UIEffect>().toneFilter = ToneFilter.Grayscale;
        }

    }

    public int returnDicValue(int cardIndex)
    {

        return DataConfig.CardsCount[cardIndex];
    }

    public int retunListValue(int cardIndex)
    {
        int[] deckCard = es.deckCardArr();

        int temp = 0;
        for (int i = 0; i < deckCard.Length; i++)
        {
            if (deckCard[i] == cardIndex)
            {
                temp++;
            }
        }
        //Debug.Log(cardIndex + "의 갯수 : " + temp);
        usingCount = temp;
        return temp;
    }

    public bool canChoose()
    {
        return false;
    }


    public void StoreUI()
    {

    }




}
