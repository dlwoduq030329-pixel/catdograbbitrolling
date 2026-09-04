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
                    // 장비 인벤토리는 두지 않는다 — 획득 현장에서 바로 장착하거나 50% 판매한다.
                    // 슬롯별 분기(빈 슬롯 자동 장착, 충돌 시 비교/선택, 골드 지급, 3D 모델 갱신)는
                    // BattleChestRewardSystem이 전담하고, PlayerWeapon이 장비 상태의 원본이다.
                    TreasureEquipmentRewardPresenter.PresentEquipmentReward(
                        this, rewardIMG, getBtns, get, giveUp, leftEquip, rightEquip);
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
