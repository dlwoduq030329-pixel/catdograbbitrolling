using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EventSet : MonoBehaviour
{
    [SerializeField]
    Image storyIMG;
    [SerializeField]
    TextMeshProUGUI storyText;
    [SerializeField]
    GameObject roll2D;
    [SerializeField]
    DOTweenAnimation textAni;

    [SerializeField]
    GameObject GetCard;
    [SerializeField]
    Image GetCardIMG;

    [SerializeField]
    DOTweenAnimation da;

    WaitForSeconds waitTime = new WaitForSeconds(3f);

    List<int> canGetCardList = new List<int>();
    [SerializeField]
    GameObject successtext;
    [SerializeField]
    GameObject failtext;

    private void OnEnable()
    {
        StorySet();
    }

    int needIndex;
    int storyIndex;
    StoryEvent tempstory;
    int needst;
    public void StorySet()
    {

        int temp = Random.Range(0, DataPool.Instance.storydata.stories.Count);
        tempstory = DataPool.Instance.storydata.stories[temp];

        storyIndex = temp;
        storyIMG.sprite = tempstory.storyIMG;
        Debug.Log(tempstory.story);
        storyText.text = tempstory.story+" " + DataConfig.playerDatas[tempstory.needStateIndex];
        needIndex = tempstory.needStateIndex;
        needst = tempstory.needStatus[needIndex];
    }

    public void Roll()
    {
        StartCoroutine(RollCor());
    }

    public IEnumerator RollCor()
    {
        uiDice ud = roll2D.GetComponent<uiDice>();
        roll2D.SetActive(true);
        yield return null;
        ud.animStart();

        int temp = Random.Range(1, 7);
       yield return waitTime;
        ud.EndRoll(temp);
        yield return new WaitForSeconds(2f);
        SuccessCheck(needIndex, temp);

    }

    public void SuccessCheck(int eventIndex,int Roll)
    {

        if (needst > DataConfig.playerDatas[tempstory.needStateIndex] + Roll)
        {
            //실패
            Debug.Log("실패");
            StartCoroutine(fail());
        }else
        {
            //성공
            Debug.Log("성공");

            StartCoroutine(success());
        }




    }

    public bool canGetCard(int index)
    {
        bool temp = true;
        if (DataConfig.CardsCount.ContainsKey(index))
        {
            if (DataConfig.CardsCount[index] >= 2)
                temp = false;
        }
        return temp;
    }

    public IEnumerator success()
    {
        successtext.gameObject.SetActive(true);
        //성공 이미지 출력
        yield return new WaitForSeconds(1f);

        switch(tempstory.success)
        {
            case Success.money:
                {
                    //돈 추가
                    GameManagerInMain.Instance.SetGold(tempstory.successCount);
                }
                break;
            case Success.weapon:
                {
                    //무기 휙둑
                    int tempIndex = Random.Range(0, DataPool.Instance.equipDatabase.equip.Count);
                    EquipData myEquipSt = DataPool.Instance.equipDatabase.equip[tempIndex].Clone();
                    myEquipSt.weapon = (weaponSt)Random.Range(0, 4);
                    switch ((int)myEquipSt.weapon)
                    {
                        case 0:
                            {
                                myEquipSt.cost = 40;
                                break;
                            }
                        case 1:
                            {
                                myEquipSt.cost = 60;
                                for (int i = 0; i < 4; i++)
                                {
                                    int temp = Random.Range(0, 5);

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
                                myEquipSt.cost = 100;
                                for (int i = 0; i < 6; i++)
                                {
                                    int temp = Random.Range(0, 5);

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
                                myEquipSt.cost = 160;
                                for (int i = 0; i < 10; i++)
                                {
                                    int temp = Random.Range(0, 5);

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

                    DataConfig.GetWeapon(myEquipSt);
                    GameManagerInMain.Instance.Player.GetComponent<weaponSet>().EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head);




                    GetCardIMG.sprite = DataPool.Instance.equipDatabase.equip[tempIndex].myEquipSprite;
                    GetCard.SetActive(true);
                    yield return new WaitForSeconds(3f);
                    //카드 이미지 없애기.
                    GetCard.SetActive(false);

                }
                break;
            case Success.card:
                {
                    //랜덤 카드 휙득
                    canGetCardList.Clear();
                    int max = DataPool.Instance.cardDatabase.cards.Count;
                    for (int i = 0; i < max; i++)
                    {
                        if (canGetCard(i))
                        {
                            canGetCardList.Add(i);
                        }
                    }

                    int x = canGetCardList[Random.Range(0, canGetCardList.Count)];

                    DataConfig.AddDic(x, 1);
                    //카드 이미지 띄우기.
                    GetCardIMG.sprite = DataPool.Instance.cardDatabase.cards[x].myCardSprite;
                    GetCard.SetActive(true);
                    yield return new WaitForSeconds(3f);
                    //카드 이미지 없애기.
                    GetCard.SetActive(false);
                }
                break;
            case Success.status:
                {
                    int statReward = Mathf.Clamp(tempstory.successCount, 1, 2);
                    DataConfig.playerDatas[tempstory.getSTIndex] += statReward;
                }
                break;
        }

        //관련 보상 이미지 출력
        yield return new WaitForSeconds(1f);
        roll2D.SetActive(false);

        GameManagerInMain.Instance.activeRoll(rollUseage.Move);
       gameObject.SetActive(false);

    }

    public IEnumerator fail()
    {
        failtext.gameObject.SetActive(true);
        //성공 이미지 출력
        yield return new WaitForSeconds(1f);


        yield return null;

        switch (tempstory.fail)
        {
            case Fail.money:
                {
                    //돈 감소

                    GameManagerInMain.Instance.SetGold(-tempstory.failCount);
                }
                break;
            case Fail.battle:
                {
                    DataConfig.hard = tempstory.battleenemyIndex;
                    DataConfig.count = tempstory.failCount;
                    SceneManager.LoadScene(2);
                }
                break;
        }

        roll2D.SetActive(false);

       
        GameManagerInMain.Instance.activeRoll(rollUseage.Move);
        gameObject.SetActive(false);

    }

    private void OnDisable()
    {
        successtext.gameObject.SetActive(false);
        failtext.SetActive(false);
    }

}
