using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;
public class CardInfo : MonoBehaviour
{
    [SerializeField]
    Image cardIMG;
    [SerializeField]
    TextMeshProUGUI cardText;
    [SerializeField]
    TextMeshProUGUI cardTag;
    [SerializeField]
    TextMeshProUGUI cardCost;
    [SerializeField]
    bool canMove = true;
    Canvas canvas;
    RectTransform rect;
    // Start is called before the first frame update
    void Start()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

    }

    // Update is called once per frame
    void Update()
    {
        if (!canMove) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
canvas.transform as RectTransform, Input.mousePosition,
canvas.worldCamera,
out Vector2 pos
);
        float x=0.5f;
        float y=0.5f;

        if (pos.x<248)
        {
            x = 0;
        }else
        {
            x = 1;
        }

        if(pos.y > -85)
        {
            y = 1;
        }else
        {
            y = 0;
        }

        rect.pivot = new Vector2(x, y);
        rect.localPosition = pos;

        
    }

    public void CardInfoSet(int cardIndex)
    {
       // this.gameObject.SetActive(true);
        cardIMG.sprite = DataPool.Instance.cardDatabase.cards[cardIndex].myCardSprite;
        cardText.text = DataPool.Instance.cardDatabase.cards[cardIndex].cardInfo;

        switch(DataPool.Instance.cardDatabase.cards[cardIndex].damage)
        {
            case 0:
                cardTag.text = "태그 : 물리 공격";
                break;
            case 1:
                cardTag.text = "태그 : 마법 공격";
                break;
            case 2:
                cardTag.text = "태그 : 지원";
                break;
            default:
                cardTag.text = "";
                break;
        }
        cardCost.text = "가치 : " + (DataPool.Instance.cardDatabase.cards[cardIndex].cardCost * 2).ToString() + "G";

    }

    public void CardInfoSet(int cardIndex,string cardData)
    {
       // this.gameObject.SetActive(true);
        cardIMG.sprite = DataPool.Instance.equipDatabase.equip[cardIndex].myEquipSprite;
        cardText.text = cardData;


    }

    public void FalseActive()
    {
        this.gameObject.SetActive(false);
    }

    
}
