using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static Unity.VisualScripting.Member;

public class CardSystem : MonoBehaviour
{

    private static CardSystem instance = null;
    public static CardSystem Instance => instance;
    //public List<int> MyDeck = new List<int>();
    [SerializeField]
    public Dictionary<int, string> DeckInfo = new Dictionary<int, string>();
    //public delegate void CardGetReady();
    //public CardGetReady cGR;
    [SerializeField]
    [Tooltip("카드 데이터 베이스")]
    private CardDatabase cardDatabase;

    public List<int> myDeck = new List<int>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }

        for (int i = 0; i < cardDatabase.cards.Count; i++)
        {

            string temp = cardDatabase.cards[i].name + "," +
                cardDatabase.cards[i].cost + "," +
                cardDatabase.cards[i].rare + "," +
                cardDatabase.cards[i].damage + "," +
                cardDatabase.cards[i].heal;

            //DeckInfo.Add(cardDatabase.cards[i].index, temp);
            //DeckConfig.DeckInfo.Add((int)cardDatabase.cards[i].index, temp);
            CardConfig.cardPool.Add(cardDatabase.cards[i].index, temp);
            //if (cardDatabase.cards[i].myCardSprite == null) continue;
            //CardConfig.cardSprite.Add(cardDatabase.cards[i].myCardSprite);
            //Debug.Log(cardDatabase.cards[i].index + "번째 카드 등록 완료" + temp);
        }
        //cGR?.Invoke();

    }

    /*    public void SetCardsInfo(int index,string CardINfo)
        {
            DeckInfo.Add(index, CardINfo);
        }

        public Dictionary<int, string> SetDeck(Dictionary<int, string> tempinfo)
        {
            var copied = new Dictionary<int, string>(tempinfo);        return copied;
           // StartCoroutine(ConnectGoogle());
        }*/


}
