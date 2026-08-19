using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class cardOwn : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField]
    public float useAP;
    [SerializeField]
    public string cardName;
    [SerializeField]
    float damage;
    [SerializeField]
    float heal;
    [SerializeField]
    public int cardIndex;


    [SerializeField]
    Image[] images;

    int handIndex;

        

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CardInit(int index)
    {
        cardIndex = DataPool.Instance.cardDatabase.cards[index].index;
        useAP = DataPool.Instance.cardDatabase.cards[index].cost;
        cardName = DataPool.Instance.cardDatabase.cards[index].name;
        Debug.Log(cardName);
        heal = DataPool.Instance.cardDatabase.cards[index].heal;
        damage = DataPool.Instance.cardDatabase.cards[index].damage;
        for(int i=0;i<images.Length;i++)
        {
            images[i].sprite = DataPool.Instance.cardDatabase.cards[index].myCardSprite;
        }

        GetComponent<CardUse>().Init();

    }
}
