using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


public class StoreManager : MonoBehaviour
{
    [SerializeField]
    StoreCardOwn[] storeCards;
    [SerializeField]
    TextMeshProUGUI reloadText;
    [SerializeField]
    TextMeshProUGUI myMoney;
    [SerializeField]
    RectTransform content;
    [SerializeField]
    TextMeshProUGUI nowMoney;
    [SerializeField]
    EquipStore[] ess;
    [SerializeField]
    Image storeKeeper;
    [SerializeField]
    Sprite Happy;
    [SerializeField]
    Sprite Idle;

    int reloadPrice = 10;
    Button currentPurchaseButton;
    UnityAction currentPurchaseAction;

    public List<int> ReloadList = new List<int>();

    public void OnEnable()
    {
        updateMoneyInStore();
        UpdateForSale();
        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            fitter.enabled = false;
            fitter.enabled = true;
        }

    }

    

    public void HappyKeeper()
    {
        storeKeeper.sprite = Happy;
    }

    public void IdleKeepr()
    {
        storeKeeper.sprite = Idle;  
    }

    public void UpdateItems()
    {
       foreach(var temp in ess)
        {
           temp.Init();
        }
    }

    public void updateMoneyInStore()
    {
        nowMoney.text = GameManagerInMain.Instance.Gold + "G 보유";
        //
    }


    public void BindPurchase(Button button, UnityAction purchaseAction)
    {
        if (currentPurchaseButton != null && currentPurchaseAction != null)
            currentPurchaseButton.onClick.RemoveListener(currentPurchaseAction);

        currentPurchaseButton = button;
        currentPurchaseAction = purchaseAction;
        if (currentPurchaseButton != null && currentPurchaseAction != null)
            currentPurchaseButton.onClick.AddListener(currentPurchaseAction);
    }

    public void ClearPurchase()
    {
        if (currentPurchaseButton != null && currentPurchaseAction != null)
            currentPurchaseButton.onClick.RemoveListener(currentPurchaseAction);

        currentPurchaseButton = null;
        currentPurchaseAction = null;
    }

    public void UpdateCardPool()
    {
        ReloadList.Clear();
        int max = DataPool.Instance.cardDatabase.cards.Count;
        for(int i =0;i<max;i++)
        {
            if(canGetCard(i))
            {
                ReloadList.Add(i);
            }
        }
    }

    public void reload()
    {
        //if(DataConfig.)
        if(GameManagerInMain.Instance.canUseGold(reloadPrice))
        {
            GameManagerInMain.Instance.SetGold(-reloadPrice);
            reloadPrice *= 2;
            reloadText.text = reloadPrice.ToString() + "G";
            myMoney.text = DataConfig.playerMoney.ToString() + "G 보유";
            UpdateForSale();
        }
    }


    public void UpdateForSale()
    {
        UpdateCardPool();

        for(int i =0;i<storeCards.Length;i++)
        {
            int randomitemCase = Random.Range(0, 2);
            if(randomitemCase == 0)
            {
                //카드일 경우
                if (ReloadList.Count > 0)
                {
                    int temp = Random.Range(0, ReloadList.Count);
                    storeCards[i].CardInit(ReloadList[temp], Success.card);
                }
                else
                {
                    int temp = Random.Range(1, DataPool.Instance.equipDatabase.equip.Count);
                    storeCards[i].CardInit(temp, Success.weapon);
                }
            }
            else
            {
                int temp = Random.Range(1, DataPool.Instance.equipDatabase.equip.Count);
                storeCards[i].CardInit(temp, Success.weapon);
            }
        }

        
    }


    public bool canGetCard(int index)
    {
        bool temp = true;
        if (DataConfig.CardsCount.ContainsKey(index))
        {
           if(DataConfig.CardsCount[index]>=2)
                temp = false;
        }
        return temp;
    }
}
