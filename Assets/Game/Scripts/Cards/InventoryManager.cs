using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    [SerializeField]
    InventoryCard[] cards;
    [SerializeField]
    nowDeckCard[] nowcards;
    [SerializeField]
    GameObject tagCard;

    [SerializeField]
    public int[] useDeckTemp = new int[10];
    [SerializeField]
    ScrollRect scroll;

    [SerializeField]
    GameObject infoIMG;
    [SerializeField]
    Slider apPerSecSlider;
    [SerializeField]
    Slider averageCostSlider;

    float apPerSec;
    float averageCost;

    [SerializeField]
    Sprite originSp;
    [SerializeField]
    Sprite highlightSp;
    [SerializeField]
    Sprite UsedSP;

    // Start is called before the first frame update
    void Start()
    {
        this.gameObject.SetActive(false);

    }

    public void SetScroll(bool temp)
    {
        scroll.vertical = temp;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ShowCardInfo(int temp)
    {
        infoIMG.gameObject.SetActive(true);
        CardInfo ci = infoIMG.GetComponent<CardInfo>();
        ci.CardInfoSet(temp);
    }

    public void DelCardInfo()
    {
        infoIMG.gameObject.SetActive(false);
    }

    public void OnEnable()
    {
        // AllInit();
   /*     for (int i = 0; i < cards.Length; i++)
        {
            cards[i].Init();
        }*/

        AllInit();

    }

    public void AllInit()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            //cards[i].Init();
        }

    }

    public bool CardInTest1(int x)
    {
        int temp = 0;
        for(int i =0;i<10;i++)
        {
            if (myDeckManager.Instance.deckcards[i].index == x)
            {
                temp++;
            }
        }

        if(temp < 2)
        {
            return true;
        }else
            return false;
    }

    public bool CardInTest(int x)
    {
        int temp;
        if(myDeckManager.Instance.myCardCount.TryGetValue(x, out temp))
        {
            int z = 0;
            for(int i =0;i<10;i++)
            {
                if (myDeckManager.Instance.deckcards[i].index == x)
                {
                   z++;
                }
            }

            if(z<temp)
            {
                return true;
            }else
            {
                return false;
            }
        }else
        {
              return false;
        }
    }

    public void CardTag(int x)
    {
        tagCard.SetActive(!tagCard.activeSelf);
        TagedCard tc = tagCard.GetComponent<TagedCard>();
        tc.Init(x);
    }

    public void CardSet(int lowIndex, int cardindex)
    {
        useDeckTemp[lowIndex] = cardindex;
    }

    public void SaveDeck()
    {
        Debug.Log("µ¶ ¿˙¿Â!");
        if(myDeckManager.Instance != null)
        {
            myDeckManager.Instance.ReSetCard(useDeckTemp);
            AllInit();
            averageCost = 0;
            for(int i =0;i<myDeckManager.Instance.deckcards.Length;i++)
            {
                averageCost += DataPool.Instance.cardDatabase.cards[myDeckManager.Instance.deckcards[i].index].cost;

            }
            averageCost = averageCost / 10;
            ShowCostAndHealAP(averageCost);
        }
    }

    public void ShowCostAndHealAP(float cost)
    {
        averageCostSlider.value = cost / 10;
        float tempHealAP = 0.5f + (0.1f * PlayerConfig.dex);
        apPerSecSlider.value = tempHealAP / 6.5f;
    }

    public Sprite returnOrigin()
    {
        return originSp;
    }

    public Sprite returnhightlight()
    {
        return highlightSp;
    }

    public Sprite returnUse()
    {
        return UsedSP;
    }
}


