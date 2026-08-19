using Coffee.UIEffects;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StoreCardOwn : MonoBehaviour,IPointerDownHandler,IPointerEnterHandler,IPointerExitHandler
{
    [SerializeField]
    Image cardIMG;
    [SerializeField]
    TextMeshProUGUI cardText;
    [SerializeField]
    Button buySellBtn;
    [SerializeField]
    GameObject aboutCardinfo;
    [SerializeField]
    TextMeshProUGUI cardinfoText;
    [SerializeField]
    TextMeshProUGUI cardCostText;
    [SerializeField]
    TextMeshProUGUI cardNameText;
    [SerializeField]
    Image tagedIMG;
    [SerializeField]
    TextMeshProUGUI panelCost;
    EquipData myEquipSt;
    CardData myCard;
    weaponSt rare;
    Success thisweaponST; //장비인지, 카드인지
    WeaponKind wk;
   
    //EquipData weapon;

    private string aboutCard;
    public string AboutCard => aboutCard;

    private int cardCost;
    public int CardCost => cardCost;

    private string cardName;
    bool isSold;

    public Image StoreImage => cardIMG;
    public Image StorePreviewImage => tagedIMG;
    public TextMeshProUGUI StoreNameText => cardText;
    public TextMeshProUGUI StorePriceText => panelCost;

    public void Awake()
    {
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isSold) return;
        StoreManager storeManager = GetComponentInParent<StoreManager>();
        storeManager.BindPurchase(buySellBtn, linkBtn);
        buySellBtn.GetComponentInChildren<TextMeshProUGUI>().text = "구매";
        buySellBtn.gameObject.SetActive(true);
        storeManager.IdleKeepr();
        
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        aboutCardinfo.SetActive(true);
        cardinfoText.text = aboutCard;
        cardCostText.text = "가치 : " + cardCost.ToString()+ "G";
        tagedIMG.sprite = cardIMG.sprite;
        cardNameText.text = cardName;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        aboutCardinfo.SetActive(false);

    }

    public void linkBtn()
    {
        if (isSold) return;
        if(GameManagerInMain.Instance.canUseGold(cardCost))
        {

            GameManagerInMain.Instance.SetGold(-cardCost);

            switch(thisweaponST)
            {
                case Success.weapon:
                    {
                        /*if(myEquipSt.weaponKind == WeaponKind.Hand)
                        {
                            if (DataConfig.leftDa != null && DataConfig.rightDa != null) return;
                        }*/

                        DataConfig.GetWeapon(myEquipSt);
                        GameManagerInMain.Instance.Player.GetComponent<weaponSet>().EquipAdapt(DataConfig.leftHand,DataConfig.rightHand,DataConfig.body,DataConfig.head);
                        GetComponentInParent<StoreManager>().UpdateItems();

                        break;
                    }
                case Success.card:
                    {
                        DataConfig.AddDic(myCard.index,1);
                        break;
                    }
            }
            UIEffect temp = cardIMG.GetComponent<UIEffect>();
            temp.toneFilter = ToneFilter.Grayscale;
            temp.toneIntensity = 0.3f;

            GetComponentInParent<StoreManager>().ClearPurchase();
            buySellBtn.gameObject.SetActive(false);
            GetComponentInParent<StoreManager>().updateMoneyInStore();
            GetComponentInParent<StoreSet>().MyCards();
            GetComponentInParent<StoreManager>().HappyKeeper();
            isSold = true;

        }
    }
    public void CardInit(int x,Success temp_)
    {
        isSold = false;
        thisweaponST = temp_;
        cardText.color = Color.white;

        UIEffect cardEffect = cardIMG.GetComponent<UIEffect>();
        if (cardEffect != null)
            cardEffect.toneFilter = ToneFilter.None;

        switch(thisweaponST)
        {
            case Success.weapon:
                {
                    myEquipSt = DataPool.Instance.equipDatabase.equip[x].Clone();
                    wk = DataPool.Instance.equipDatabase.equip[x].weaponKind;
                    int rarityCount = Mathf.Clamp(DataConfig.stage, 1, 4);
                    rare = (weaponSt)Random.Range(0, rarityCount);
                    myEquipSt.weapon = rare;
                    switch ((int)rare)
                    {
                        case 0:
                            {
                                myEquipSt.cost = 60;
                                break;
                            }
                        case 1:
                            {
                                cardText.color = Color.green;
                                myEquipSt.cost = 100;
                                for (int i = 0; i < 4; i++)
                                {
                                    int temp = Random.Range(0, 4);

                                    switch (temp)
                                    {
                                        case 0:
                                            {
                                                myEquipSt.stroffset++;
                                                break;
                                            }
                                        case 1:
                                            {
                                                myEquipSt.dexoffset++;
                                                break;
                                            }
                                        case 2:
                                            {
                                                myEquipSt.vitoffset++;
                                                break;
                                            }
                                        case 3:
                                            {
                                                myEquipSt.wisoffset++;
                                                break;
                                            }
                                    }
                                }
                                break;
                            }
                        case 2:
                            {
                                cardText.color = new Color(0.64f, 0.21f, 0.93f, 1f);

                                myEquipSt.cost = 180;
                                for (int i = 0; i < 6; i++)
                                {
                                    int temp = Random.Range(0, 4);

                                    switch (temp)
                                    {
                                        case 0:
                                            {
                                                myEquipSt.stroffset++;
                                                break;
                                            }
                                        case 1:
                                            {
                                                myEquipSt.dexoffset++;
                                                break;
                                            }
                                        case 2:
                                            {
                                                myEquipSt.vitoffset++;
                                                break;
                                            }
                                        case 3:
                                            {
                                                myEquipSt.wisoffset++;
                                                break;
                                            }
                                    }
                                }
                                break;
                            }
                        case 3:
                            {
                                cardText.color = Color.yellow;
                                myEquipSt.cost = 300;
                                for (int i = 0; i < 10; i++)
                                {
                                    int temp = Random.Range(0, 4);

                                    switch (temp)
                                    {
                                        case 0:
                                            {
                                                myEquipSt.stroffset++;
                                                break;
                                            }
                                        case 1:
                                            {
                                                myEquipSt.dexoffset++;
                                                break;
                                            }
                                        case 2:
                                            {
                                                myEquipSt.vitoffset++;
                                                break;
                                            }
                                        case 3:
                                            {
                                                myEquipSt.wisoffset++;
                                                break;
                                            }
                                    }
                                }
                                break;
                            }
                    }
                    aboutCard = "STR + " + myEquipSt.stroffset + "\n" +
                                "WIS + " + myEquipSt.wisoffset + "\n" +
                                "DEX + " + myEquipSt.dexoffset + "\n" +
                                "VIT + " + myEquipSt.vitoffset + "\n";

                    if(myEquipSt.weaponKind == WeaponKind.TwoHand)
                    {
                        aboutCard += "\n이 무기는 양손무기입니다.\n 구매 시 각 손의 무기가 자동 판매됩니다.";
                    }

                    myCard = null;
                    cardCost = myEquipSt.cost;
                    cardIMG.sprite = myEquipSt.myEquipSprite;
                    cardText.text = myEquipSt.cardname;
                    cardName = myEquipSt.cardname;
                    panelCost.text = cardCost.ToString() + "G";
                }
                break;
            case Success.card:
                {
                    myCard = DataPool.Instance.cardDatabase.cards[x];
                    myEquipSt = null;
                    aboutCard = myCard.cardInfo;
                    cardCost = myCard.cardCost * 2;
                    cardIMG.sprite = myCard.myCardSprite;
                    cardText.text = myCard.name;
                    cardName = myCard.name;
                    panelCost.text = cardCost.ToString() + "G";

                    switch(myCard.rare)
                    {
                        case "common":
                            cardText.color = Color.white; break;
                        case "Rare":
                            cardText.color = Color.green; break;
                        case "Epic":
                            cardText.color = new Color(0.64f, 0.21f, 0.93f, 1f); break;
                        case "Legendary":
                            cardText.color = Color.yellow; break;


                    }
                }
                break;
        }

    }
}
