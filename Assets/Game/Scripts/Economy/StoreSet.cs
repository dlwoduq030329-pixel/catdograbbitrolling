using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StoreSet : MonoBehaviour
{
    [SerializeField]
    GameObject[] Cards;
    [SerializeField]
    Button sellButton;
    [SerializeField]
    ScrollRect scrollRect;
    [SerializeField]
    RollDice rd;
    [SerializeField]
    GameObject[] equipments;  

    List<int> myCards = new List<int>();
    int myCardsCount;

    public void OnEnable()
    {
        MyCards();
        
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void MyCards()
    {
        myCards.Clear();
        for (int i = 0; i < Cards.Length; i++)
        {
            Cards[i].gameObject.SetActive(false);
        }
        foreach (var key in DataConfig.CardsCount.Keys)
        {
            myCards.Add(key);
        }

        myCardsCount = myCards.Count;

        for(int i =0;i<myCardsCount;i++)
        {
            Cards[i].gameObject.SetActive(true);
            Cards[i].gameObject.GetComponent<InventoryStore>().StoreInvenInit(myCards[i]);
        }
        Canvas.ForceUpdateCanvases(); // 레이아웃 먼저 갱신
        scrollRect.verticalNormalizedPosition = 1f; // 맨 위
    }

    public void Reload()
    {
        //판매 목록 리롤.
    }

    public void SetSellItem()
    {
        //아이템,카드,장비 랜덤 돌리고 화면에 띄우는거 및 버튼 연결.
    }

    public void LinkSellButton(int index)
    {
        sellButton.onClick.RemoveAllListeners();
        sellButton.gameObject.SetActive(true);
        sellButton.onClick.AddListener(() => Sell(index));
    }

    public void Sell(int index)
    {
        int CardCount = DataConfig.CardsCount[index];
        int temp = 0;
        foreach(var count in DataConfig.cardData)
        {
            if(count == CardCount)
            {
                temp++;
            }
        }

        if (temp == CardCount ||
            temp == 0) return;

        if(CardCount -1 > 0)
        {
            DataConfig.AddDic(index, -1);
        }else
        {
            DataConfig.AddDic(index, -1);
            DataConfig.CardsCount.Remove(index);
        }


        sellButton.gameObject.SetActive(false);


    }

    public void OnDisable()
    {
        GameManagerInMain.Instance.activeRoll(rollUseage.Move);
    }
}
