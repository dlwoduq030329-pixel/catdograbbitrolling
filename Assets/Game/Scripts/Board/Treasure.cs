using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum RewardState
{ 
    Card,
    Equipment
}

public class Treasure : MonoBehaviour
{
    [SerializeField]
    Button dropBTN;
    [SerializeField]
    Button openBTN;
    [SerializeField]
    Image rewardIMG;
    [SerializeField]
    Image[] open;
    [SerializeField]
    Image close;
    [SerializeField]
    GameObject rockpit;

    [SerializeField]
    GameObject getBtns;
    [SerializeField]
    Button get;
    [SerializeField]
    Button giveUp;

    [SerializeField]
    Button leftEquip;
    [SerializeField]
    Button rightEquip;
    [SerializeField]
    GameObject equipBtn;
    int rewardIndex;

    bool tryOpen =false;
    bool rewardPresented;
    bool movementRestored;
    public bool TryOpen => tryOpen;

    RewardSt nowst;
    public RewardSt Nowst => nowst;

    // Start is called before the first frame update
    void Start()
    {
        //StartCoroutine(nameof(OpenChest));
    }

    private void OnEnable()
    {
        //Open();
        getBtns.SetActive(false);
        tryOpen = false;
        rewardPresented = false;
        movementRestored = false;
        rockpit.SetActive(false);
        GameManagerInMain.Instance?.DeactiveRoll();
    }

    // Update is called once per frame
    void Update()
    {
        if (tryOpen) return;
        if(Input.GetMouseButtonDown(0)&& !tryOpen)
        {
            tryOpen = true;
            Open();
        }
    }

    public void Open()
    {
        getBtns.gameObject.SetActive(false);
        rockpit.SetActive(false);

        dropBTN.onClick.Invoke();
        Invoke(nameof(DownSound), 0.4f);
        Invoke(nameof(OpenAfterDrop), 1.1f);
    }

    public void DownSound()
    {
        SoundManager.Instance.BoxDown();
    }

    public void Lock()
    {
        // 자물쇠 이벤트는 사용하지 않고 곧바로 상자를 연다.
        OpenAfterDrop();
    }

    private void OpenAfterDrop()
    {
        if (rewardPresented || !gameObject.activeInHierarchy) return;

        rewardPresented = true;
        GetAward();
        openBTN.onClick.Invoke();
        Invoke(nameof(OpenBox), 0.3f);
    }

    public void Success()
    {
        OpenAfterDrop();
    }

    public void OpenBox()
    {
        SoundManager.Instance.BoxOpen();
    }

    public void SetFalse()
    {
        tryOpen = false;

    }

    public void Fail()
    {
        OpenAfterDrop();
    }

    public void Close()
    {
        open[0].gameObject.SetActive(false);
        open[1].gameObject.SetActive(false);
        rewardIMG.gameObject.SetActive(false);

        close.gameObject.SetActive(true);

        this.gameObject.SetActive(false);
    }


    public void GetAward()
    {
        rewardIMG.gameObject.SetActive(true);


        List<int> eligibleCards = new List<int>();
        if (DataPool.Instance != null && DataPool.Instance.cardDatabase != null)
        {
            for (int i = 0; i < DataPool.Instance.cardDatabase.cards.Count; i++)
                if (!CheckCard(i)) eligibleCards.Add(i);
        }
        bool hasEquipment = DataPool.Instance != null &&
                            DataPool.Instance.equipDatabase != null &&
                            DataPool.Instance.equipDatabase.equip.Count > 0;
        if (eligibleCards.Count == 0 && !hasEquipment)
        {
            GrantGoldFallback();
            return;
        }

        RewardState rs = (RewardState)Random.Range(0, 2);
        if (rs == RewardState.Card && eligibleCards.Count == 0) rs = RewardState.Equipment;
        if (rs == RewardState.Equipment && !hasEquipment) rs = RewardState.Card;

        switch (rs)
        {
                case RewardState.Card:
                {
                    int temp = eligibleCards[Random.Range(0, eligibleCards.Count)];
#if false
                    int tryCount = 0;
                    int maxTry = 100;

                    do
                    {
                        temp++;
                        if (temp > DataPool.Instance.cardDatabase.cards.Count -1) temp = 0;
                        tryCount++;

                        Debug.Log("cardIndex : " + temp);
                        if (tryCount > maxTry)
                        {
                            Debug.LogWarning("조건 만족 카드 없음");
                            return;
                        }

                    } while (CheckCard(temp));
#endif

                    Debug.Log(rewardIMG.name);
                    rewardIndex = temp;
                    rewardIMG.gameObject.GetComponent<GetItem>().Init(RewardState.Card, rewardIndex);
                    rewardIMG.sprite = DataPool.Instance.cardDatabase.cards[rewardIndex].myCardSprite;
                }
                break;
                case RewardState.Equipment:
                {
                    int length = DataPool.Instance.equipDatabase.equip.Count;
                    EquipData tempEquip = DataPool.Instance.equipDatabase.equip[Random.Range(0,length)].Clone();

                    int x = Random.Range(0, 4);

                    tempEquip.weapon = (weaponSt)x;
                    switch(x)
                    {
                        case 0:
                            {
                                tempEquip.cost = 40;
                                break;
                            }
                        case 1:
                            {
                                tempEquip.cost = 60;
                                for (int i = 0; i < 4; i++)
                                {
                                    int temp = Random.Range(0, 5);

                                    switch (temp)
                                    {
                                        case 0:
                                            {
                                                tempEquip.stroffset++;
                                                break;
                                            }
                                        case 1:
                                            {
                                                tempEquip.dexoffset++;
                                                break;
                                            }
                                        case 2:
                                            {
                                                tempEquip.vitoffset++;
                                                break;
                                            }
                                        case 3:
                                            {
                                                tempEquip.wisoffset++;
                                                break;
                                            }
                                    }
                                }
                                break;
                            }
                        case 2:
                            {
                                tempEquip.cost = 100;
                                for (int i = 0; i < 6; i++)
                                {
                                    int temp = Random.Range(0, 5);

                                    switch (temp)
                                    {
                                        case 0:
                                            {
                                                tempEquip.stroffset++;
                                                break;
                                            }
                                        case 1:
                                            {
                                                tempEquip.dexoffset++;
                                                break;
                                            }
                                        case 2:
                                            {
                                                tempEquip.vitoffset++;
                                                break;
                                            }
                                        case 3:
                                            {
                                                tempEquip.wisoffset++;
                                                break;
                                            }
                                    }
                                }
                                break;
                            }
                        case 3:
                            {
                                tempEquip.cost = 160;
                                for (int i = 0; i <10; i++)
                                {
                                    int temp = Random.Range(0, 5);

                                    switch (temp)
                                    {
                                        case 0:
                                            {
                                                tempEquip.stroffset++;
                                                break;
                                            }
                                        case 1:
                                            {
                                                tempEquip.dexoffset++;
                                                break;
                                            }
                                        case 2:
                                            {
                                                tempEquip.vitoffset++;
                                                break;
                                            }
                                        case 3:
                                            {
                                                tempEquip.wisoffset++;
                                                break;
                                            }
                                    }
                                }
                                break;
                            }
                    }

                    rewardIndex = tempEquip.weaponIndex;
                    string equipString = "str + " + tempEquip.stroffset + "\n" +
                                         "wis + " + tempEquip.wisoffset + "\n" +
                                         "dex + " + tempEquip.dexoffset + "\n" +
                                         "vit + " + tempEquip.vitoffset;
                    rewardIMG.gameObject.GetComponent<GetItem>().Init(RewardState.Equipment, rewardIndex,equipString);
                    rewardIMG.sprite = DataPool.Instance.equipDatabase.equip[rewardIndex].myEquipSprite;

                    //버튼 on
                    getBtns.gameObject.SetActive(true);
                    get.onClick.RemoveAllListeners();
                    rightEquip.onClick.RemoveAllListeners();
                    leftEquip.onClick.RemoveAllListeners();
                    switch (tempEquip.weaponKind)
                    {
                        case WeaponKind.Hand:
                            {

                                if (DataConfig.isTwoHandEquip())
                                {
                                    get.onClick.AddListener(() => equipBtn.gameObject.SetActive(true));

                                    weaponSet set = GameManagerInMain.Instance.Player.GetComponent<weaponSet>();
                                    


                                    leftEquip.GetComponent<Image>().sprite = DataConfig.leftDa.myEquipSprite;
                                    rightEquip.GetComponent<Image>().sprite = DataConfig.rightDa.myEquipSprite;
                                    rightEquip.onClick.AddListener(DataConfig.SellRightWeapon);
                                    //rightEquip.onClick.AddListener(() => DataConfig.GetWeapon(tempEquip));
                                    leftEquip.onClick.AddListener(DataConfig.SellLeftWeapon);
                                    leftEquip.onClick.AddListener(() => DataConfig.GetWeapon(tempEquip));

                                    leftEquip.onClick.AddListener(() => set.EquipAdapt(DataConfig.leftHand,DataConfig.rightHand,DataConfig.body,DataConfig.head));
                                    rightEquip.onClick.AddListener(() => set.EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head));

                                    rightEquip.onClick.AddListener(CloseTreasureAndStopCor);
                                    leftEquip.onClick.AddListener(CloseTreasureAndStopCor);

                                    //버튼에 추가적으로 판매 기능까지 넣기.
                                }
                                else
                                {
                                    get.onClick.AddListener(() => DataConfig.GetWeapon(tempEquip));
                                    weaponSet set = GameManagerInMain.Instance.Player.GetComponent<weaponSet>();
                                    get.onClick.AddListener(() => set.EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head));

                                    get.onClick.AddListener(CloseTreasureAndStopCor);

                                }
                                break;
                            }
                        case WeaponKind.Body:
                            {
                                get.onClick.AddListener(() => DataConfig.GetWeapon(tempEquip));
                                weaponSet set = GameManagerInMain.Instance.Player.GetComponent<weaponSet>();
                                get.onClick.AddListener(() => set.EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head));

                                get.onClick.AddListener(CloseTreasureAndStopCor);


                                break;
                            }
                        case WeaponKind.Head:
                            {
                                get.onClick.AddListener(() => DataConfig.GetWeapon(tempEquip));
                                weaponSet set = GameManagerInMain.Instance.Player.GetComponent<weaponSet>();
                                get.onClick.AddListener(() => set.EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head));

                                get.onClick.AddListener(CloseTreasureAndStopCor);


                                break;
                            }
                        case WeaponKind.TwoHand:
                            {
                                get.onClick.AddListener(DataConfig.SellRightWeapon);
                                get.onClick.AddListener(DataConfig.SellLeftWeapon);
                                get.onClick.AddListener(()=>DataConfig.GetWeapon(tempEquip));
                                weaponSet set = GameManagerInMain.Instance.Player.GetComponent<weaponSet>();
                                get.onClick.AddListener(() => set.EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head));

                                get.onClick.AddListener(CloseTreasureAndStopCor);

                                //get.onClick.AddListener()
                                break;
                            }
                    }

                    //get.onClick.AddListener(() => tryOpen = false);
                    giveUp.onClick.RemoveListener(CloseTreasureAndStopCor);
                    giveUp.onClick.AddListener(CloseTreasureAndStopCor);

                }
                break;
        }
    }

    private void GrantGoldFallback()
    {
        const int fallbackGold = 25;
        DataConfig.playerMoney += fallbackGold;
        rewardIMG.gameObject.SetActive(false);
        Debug.Log($"[Treasure] No card or equipment reward was available. Granted {fallbackGold}G instead.", this);
        CloseTreasureAndStopCor();
    }

    public void CloseTreasureAndStopCor()
    {
        CancelInvoke();
        rockpit.SetActive(false);
        this.gameObject.SetActive(false);
        //tryOpen = false;
    }



    public bool CheckCard(int x)
    {
        // true means this card must be skipped. New cards and cards owned once
        // remain valid rewards; only cards already at the two-copy cap are rejected.
        return DataConfig.CardsCount.TryGetValue(x, out int owned) && owned >= 2;
    }

    private void OnDisable()
    {
        CancelInvoke();
        Close();
        if (!movementRestored)
        {
            movementRestored = true;
            GameManagerInMain.Instance?.activeRoll(rollUseage.Move);
        }
    }

}
