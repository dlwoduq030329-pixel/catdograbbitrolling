using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryStore : MonoBehaviour,
    IPointerDownHandler
{
    Image cardIMG;
    Image[] cardGet;
    [SerializeField]
    Sprite have;
    [SerializeField]
    Sprite use;
    [SerializeField]
    Sprite none;
    StoreSet storeSet;

    int cardIndex;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StoreInvenInit(int x)
    {

        GetCom();
        cardIndex = x;
        cardIMG.sprite = DataPool.Instance.cardDatabase.cards[x].myCardSprite;

        if (!DataConfig.CardsCount.ContainsKey(x)) return;
        int Count = DataConfig.CardsCount[x];
        for (int i = 0; i < 2; i++)
        {
            cardGet[i].sprite = none;
        }

        for (int i =0; i<Count;i++)
        {
            cardGet[i].sprite = have;
        }

        for(int j = 0; j< returnListCount(x);j ++)
        {
            cardGet[j].sprite = use;
        }

        
    }

    public int returnListCount(int index)
    {
        int temp = 0;

        foreach (var x in DataConfig.cardData)
        {
            if(x == index)
            {
                temp++;
            }
        }
        return temp;
    }

    public void GetCom()
    {
        Image[] temp = GetComponentsInChildren<Image>(true);
        cardIMG = temp[2];
        cardGet = new Image[2];
        cardGet[0] = temp[3];
        cardGet[1] = temp[4];
        storeSet = GetComponentInParent<StoreSet>();
    }

   public void OnPointerDown(PointerEventData eventData)
    {
        /* if(storeSet==null)
         {
             GetCom();
         }
         storeSet.LinkSellButton(cardIndex);*/
        Debug.Log("´©¸§");
        GetComponentInParent<sellCard>().CardSellBtn(cardIndex);
    }


}
