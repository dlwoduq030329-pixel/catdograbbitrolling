using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;

public class BattleCard : MonoBehaviour
{
    [SerializeField]
    GameObject cardPrefab;
    [SerializeField]
    Transform CardPos;
    [SerializeField]
    GameObject cardInfo;
    [SerializeField]
    CardInfo cardin;
    public List <int> handCard = new List <int>();
    public List <int> DeckCard = new List <int>();

    public GameObject[] nowMyHand = new GameObject[5];

    private void Start()
    {
    }

    public void StartDraw()
    {
        DeckCard.Clear();
        handCard.Clear();

        foreach (var value in DataConfig.cardData)
        {
            Debug.Log(value.ToString());
            DeckCard.Add(value);
        }

        StartCoroutine(nameof(firstDraw));

    }

    public void OpenInfo(int cardIndex)
    {
        cardin.CardInfoSet(cardIndex);
        cardInfo.SetActive(true);
    }

    public void CloseInfo()
    {
        cardInfo.SetActive(false);
    }

    public IEnumerator firstDraw()
    {
        for(int i =0;i<5; i++)
        {
            int x = Random.Range(0, DeckCard.Count);
            int tempNum = DeckCard[x];
            handCard.Add(tempNum);
            DeckCard.RemoveAt(x);
            var temp = Instantiate(cardPrefab,CardPos);
            nowMyHand[i] = temp;
            temp.GetComponent<cardOwn>().CardInit(tempNum);
            yield return new WaitForSeconds(0.5f);
        }
        StartCoroutine(nameof(disableCard));
    }

    public void UseCard(int index)
    {
        int temp = handCard.IndexOf(index);

        DeckCard.Add(handCard[temp]);
        handCard.RemoveAt(temp);
    }

    public IEnumerator disableCard()
    {
        while(true)
        {
            yield return null;
            for(int i =0;i<5;i++)
            {
                if (!nowMyHand[i].activeSelf)
                {
                    int temp = Random.Range(0, DeckCard.Count);
                    nowMyHand[i].GetComponent<cardOwn>().CardInit(DeckCard[temp]);
                    handCard.Add(DeckCard[temp]);
                    DeckCard.RemoveAt(temp);
                    nowMyHand[i].SetActive(true);
                }
            }
        }
    }//수정하기
}
