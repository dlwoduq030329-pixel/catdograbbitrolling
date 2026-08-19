using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Tree;

public class myDeckManager : MonoBehaviour
{

    [TextArea(3, 5)]
    [SerializeField]
    private string classDescription = "이 클래스는 메인씬에서만 사용되는 덱 리스트를 의미합니다.";
    public static myDeckManager Instance;

    [SerializeField]
   // public List<CardData> deckCards = new List<CardData>(); //내 덱 리스트. 모든 정보를 소유.
    public Dictionary<int,int> myCardCount = new Dictionary<int, int>();
    public CardData[] deckcards = new CardData[10];
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        //deckCards.Clear();
        for(int i =0;i<10;i++)
        {
            deckcards[i] = null;
        }
        

        AddCardLow(DataPool.Instance.cardDatabase.cards[0]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[1]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[2]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[3]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[70]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[0]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[1]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[2]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[3]);
        AddCardLow(DataPool.Instance.cardDatabase.cards[70]);




    }

    public void ShowDeck()
    {
        for(int i =0;i<myCardCount.Count;i++)
        {
            //Debug.Log("i번째 카드 : " + deckCards[i].name + myCardCount[deckCards[i].index]);
        }
    }

    public void AddCardLow(CardData temp)
    {
        //bool canDeck = false;
        for (int i = 0; i < deckcards.Length; i++)
        {
            if (deckcards[i] == null)
            {
                deckcards[i] = temp;

                //canDeck = true;
                break;
            }
        }

        //if(!canDeck)
        {
            if (myCardCount.ContainsKey(temp.index))
            {
                myCardCount[temp.index] += 1;
            }
            else
            {
                myCardCount.Add(temp.index, 1);
            }
        }
    }

    public void ReSetCard(int[] temp)
    {
        for (int i = 0; i < 10; i++)
        {
            deckcards[i] = null;
        }

        for (int i =0;i<10;i++)
        {
            deckcards[i] = DataPool.Instance.cardDatabase.cards[temp[i]];
        }
    }


}
