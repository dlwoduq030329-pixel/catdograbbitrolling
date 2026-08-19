using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class sellCard : MonoBehaviour
{
    [SerializeField]
    Button sellBtn;
    [SerializeField]
    StoreSet ss;
    [SerializeField]
    StoreManager sm;

    public void CardSellBtn(int index)
    {
        if (!DataConfig.CardsCount.ContainsKey(index) || DataConfig.CardsCount[index] <=0) return;
        int nowCard = DataConfig.CardsCount[index]; //2
        int deckCard = ReturnCardCount(index);//1
        sellBtn.onClick.RemoveAllListeners();
        if (nowCard == deckCard|| nowCard < deckCard) return;

        
        int cardMoney = DataPool.Instance.cardDatabase.cards[index].cardCost / 2;
        sellBtn.onClick.AddListener(() => GameManagerInMain.Instance.SetGold(cardMoney));
        sellBtn.onClick.AddListener(() => DataConfig.AddDic(index, -1));
        sellBtn.onClick.AddListener(() => sm.updateMoneyInStore());
        sellBtn.onClick.AddListener(() => ss.MyCards());
        sellBtn.onClick.AddListener(() => sellBtn.gameObject.SetActive(false));


        sellBtn.gameObject.SetActive(true);
        sellBtn.GetComponentInChildren<TextMeshProUGUI>().text = "판매";
    }

    public void EquipCardSellBtn(int index,EquipState es)
    {
        sellBtn.onClick.RemoveAllListeners();
        sellBtn.GetComponentInChildren<TextMeshProUGUI>().text = "판매";
        

        switch (es)
        {
            case EquipState.LeftHand:
                {
                    sellBtn.onClick.AddListener(()=>DataConfig.SellLeftWeapon());
                    sellBtn.onClick.AddListener(() => sm.updateMoneyInStore());
                    sellBtn.onClick.AddListener(() => sm.UpdateItems());
                    sellBtn.onClick.AddListener(() =>GameManagerInMain.Instance.Player.GetComponent<weaponSet>().EquipAdapt(DataConfig.leftHand,DataConfig.rightHand,DataConfig.body,DataConfig.head));

                    sellBtn.onClick.AddListener(() => sellBtn.gameObject.SetActive(false));
                    sellBtn.onClick.AddListener(() => sellBtn.onClick.RemoveAllListeners());
                    break;
                }
            case EquipState.RightHand:
                {
                    sellBtn.onClick.AddListener(() => DataConfig.SellRightWeapon());
                    sellBtn.onClick.AddListener(() => sm.updateMoneyInStore());
                    sellBtn.onClick.AddListener(() => sm.UpdateItems());
                    sellBtn.onClick.AddListener(() => GameManagerInMain.Instance.Player.GetComponent<weaponSet>().EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head));

                    sellBtn.onClick.AddListener(() => sellBtn.gameObject.SetActive(false));
                    sellBtn.onClick.AddListener(() => sellBtn.onClick.RemoveAllListeners());

                    break;
                }
            case EquipState.Head:
                {
                    sellBtn.onClick.AddListener(() => DataConfig.SellheadWeapon());
                    sellBtn.onClick.AddListener(() => sm.updateMoneyInStore());
                    sellBtn.onClick.AddListener(() => sm.UpdateItems());
                    sellBtn.onClick.AddListener(() => GameManagerInMain.Instance.Player.GetComponent<weaponSet>().EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head));

                    sellBtn.onClick.AddListener(() => sellBtn.gameObject.SetActive(false));
                    sellBtn.onClick.AddListener(() => sellBtn.onClick.RemoveAllListeners());

                    break;
                }
            case EquipState.Body:
                {
                    sellBtn.onClick.AddListener(() => DataConfig.SellBodyWeapon());
                    sellBtn.onClick.AddListener(() => sm.updateMoneyInStore());
                    sellBtn.onClick.AddListener(() => sm.UpdateItems());
                    sellBtn.onClick.AddListener(() => GameManagerInMain.Instance.Player.GetComponent<weaponSet>().EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head));

                    sellBtn.onClick.AddListener(() => sellBtn.gameObject.SetActive(false));
                    sellBtn.onClick.AddListener(() => sellBtn.onClick.RemoveAllListeners());

                    break;
                }
        }
        sellBtn.gameObject.SetActive(true);

    }


    public int ReturnCardCount(int index)
    {
        int count = 0;



        foreach(var temp in DataConfig.cardData)
        {
            if(temp == index)
            {
                count++;
            }
        }

        return count;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
