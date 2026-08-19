using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public enum EquipState
{
    LeftHand,
    RightHand,
    Head,
    Body
}
public class EquipStore : MonoBehaviour,IPointerDownHandler
{

    [SerializeField]
    EquipState state;

    public int equipIndex;
    [SerializeField]
    Image thisIMG;

    public void OnEnable()
    {
        Init();
    }

    // Start is called before the first frame update
    public void Init()
    {
        switch(state)
        {
                case EquipState.LeftHand:
                {
                    equipIndex = DataConfig.leftHand;
                }
                break;
                case EquipState.RightHand:
                {
                    equipIndex = DataConfig.rightHand;
                }
                break;
                case EquipState.Head:
                {
                    equipIndex = DataConfig.head;
                }
                break;
                case EquipState.Body:
                {
                    equipIndex = DataConfig.body;
                }
                break;
        }
        //thisIMG=
        thisIMG.sprite = DataPool.Instance.equipDatabase.equip[equipIndex].myEquipSprite;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Init();
        if (equipIndex == 0)
        {
            Debug.Log("ºó¼Õ!");
            return;
        }

        /* if(storeSet==null)
         {
             GetCom();
         }
         storeSet.LinkSellButton(cardIndex);*/
        //Debug.Log("´©¸§");
        GetComponentInParent<sellCard>().EquipCardSellBtn(equipIndex,state);
    }
}
