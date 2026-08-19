/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerSystem : MonoBehaviour
{
    private static PowerSystem instance = null;
    public static PowerSystem Instance => instance;
    //public List<int> MyDeck = new List<int>();
    //[SerializeField]
    public Dictionary<int, string> power1 = new Dictionary<int, string>();
    public Dictionary<int, string> power2 = new Dictionary<int, string>();
    public Dictionary<int, string> power3 = new Dictionary<int, string>();

    public delegate void PowerGetReady();
    public PowerGetReady pGR;
    [SerializeField]
    [Tooltip("첫번째 증강 데이터 베이스")]
    private PowerDatabase1 powerDatabase1;
    [Tooltip("두번째 증강 데이터 베이스")]
    private PowerDatabase2 powerDatabase2;
    [Tooltip("세번째 증강 데이터 베이스")]
    private PowerDatabase3 powerDatabase3;

    bool isInit = false;

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

        if (isInit) return;
        for (int i = 0; i < powerDatabase1.power1.Count; i++)
        {

            string temp = powerDatabase1.power1[i].title + "," +
                powerDatabase1.power1[i].strUP + "," +
                powerDatabase1.power1[i].wisUP + "," +
                powerDatabase1.power1[i].dexUP + "," +
                powerDatabase1.power1[i].vitUP;

            //DeckInfo.Add(cardDatabase.cards[i].index, temp);
            //DeckConfig.DeckInfo.Add((int)cardDatabase.cards[i].index, temp);
            PowerConfig.power1.Add(powerDatabase1.power1[i].index, temp);
            //if (cardDatabase.cards[i].myCardSprite == null) continue;
            //CardConfig.cardSprite.Add(cardDatabase.cards[i].myCardSprite);
            Debug.Log(powerDatabase1.power1[i].index + "번째 증강1 등록 완료" + temp);
        }

        for (int i = 0; i < powerDatabase2.power2.Count; i++)
        {

            string temp = powerDatabase2.power2[i].title + "," +
                powerDatabase2.power2[i].strUp + "," +
                powerDatabase2.power2[i].wisUP + "," +
                powerDatabase2.power2[i].dexUP + "," +
                powerDatabase2.power2[i].vitUP + "," +
                powerDatabase2.power2[i].addCardIndex;

            //DeckInfo.Add(cardDatabase.cards[i].index, temp);
            //DeckConfig.DeckInfo.Add((int)cardDatabase.cards[i].index, temp);
            PowerConfig.power1.Add(powerDatabase2.power2[i].index, temp);
            //if (cardDatabase.cards[i].myCardSprite == null) continue;
            //CardConfig.cardSprite.Add(cardDatabase.cards[i].myCardSprite);
            Debug.Log(powerDatabase2.power2[i].index + "번째 증강2 등록 완료" + temp);
        }

        for (int i = 0; i < powerDatabase3.power3.Count; i++)
        {

            string temp = powerDatabase3.power3[i].title + "," +
                powerDatabase3.power3[i].strUp + "," +
                powerDatabase3.power3[i].wisUP + "," +
                powerDatabase3.power3[i].dexUP + "," +
                powerDatabase3.power3[i].vitUP + "," +
                powerDatabase3.power3[i].activeFuncName;

            //DeckInfo.Add(cardDatabase.cards[i].index, temp);
            //DeckConfig.DeckInfo.Add((int)cardDatabase.cards[i].index, temp);
            PowerConfig.power1.Add(powerDatabase3.power3[i].index, temp);
            //if (cardDatabase.cards[i].myCardSprite == null) continue;
            //CardConfig.cardSprite.Add(cardDatabase.cards[i].myCardSprite);
            Debug.Log(powerDatabase3.power3[i].index + "번째 증강2 등록 완료" + temp);
        }
        isInit = true;
        pGR?.Invoke();
    }
}
*/