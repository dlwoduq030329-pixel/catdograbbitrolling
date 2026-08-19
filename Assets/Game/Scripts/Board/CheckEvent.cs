using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckEvent : MonoBehaviour
{
    [SerializeField]
    RollDice rd;
    [SerializeField]
    PlayerToUI ptu;
    [SerializeField]
    GameObject eventImg;
    [SerializeField]
    TextMeshProUGUI eventText;
    DOTweenAnimation anim;
    bool resolvingStartNode;
    NodeBasePlayerMov nodebaseplayerMov;

    private void Awake()
    {
        anim = eventImg.GetComponentInChildren<DOTweenAnimation>();
        nodebaseplayerMov = GetComponent<NodeBasePlayerMov>();
    }

    private void Start()
    {
        //if(nodebaseplayerMov.returnST() == )
      
    }
    public void check(nodeState st)
   {
        switch(st)
        {
            case nodeState.battle:
                {
                    StartCoroutine(BattleCheck());
                }
                break;
            case nodeState.store:
                {
                    StartCoroutine(StoreCheck());

                }
                break;
            case nodeState.discovery:
                {
                    StartCoroutine(DiscoveryCheck());

                }
                break;
            case nodeState.gameevent:
                {
                    StartCoroutine(GameEventCheck());

                }
                break;
            case nodeState.start:
                {
                    Debug.Log("start 실행");
                    if(!DataConfig.isBattled)
                    {
                        Debug.Log("보스 전투");
                        DataConfig.isBattled = true;
                        //DataConfig.stage++;
                        StartCoroutine(BattleCheckBoss());
                    }else
                    {
                        Debug.Log("보스 전투 끝 보상 휙득");

                        if (DataConfig.stage == 2)
                        {
                            GameManagerInMain.Instance.OpenTitleSelection();
                        }
                        else if (DataConfig.stage == 3)
                        {

                            GameManagerInMain.Instance.OpenJobSelection();
                        }
                        else if (DataConfig.stage >= 4)
                        {

                            StartCoroutine(BattleCheckBoss());
                        }
                        else
                        {
                            GameManagerInMain.Instance.activeRoll(rollUseage.Move);
                        }
                        DataConfig.isBattled = false;
                    }
                    //
                    // (resolvingStartNode) break;
                    //resolvingStartNode = true;
                }
                break;
        }


    }

    


    public void CompleteStartNodeSelection()
    {
        resolvingStartNode = false;
    }

    public IEnumerator BattleCheckBoss()
    {
        eventText.text = "보스 등장";
        eventImg.gameObject.SetActive(true);
        anim.DORestartAllById("startevent");
        SoundManager.Instance.OpenEvent();
        yield return new WaitForSeconds(1);
        eventImg.gameObject.SetActive(false);

        DataConfig.hard = 7;
        DataConfig.count = 1;
        SceneManager.LoadScene(2);
        //GameManagerInMain.Instance.activeRoll();

    }

    public IEnumerator BattleCheck()
    {
        eventText.text = "전투 발생";
        eventImg.gameObject.SetActive(true);
        anim.DORestartAllById("startevent");
        SoundManager.Instance.OpenEvent();
        yield return new WaitForSeconds(1);
        eventImg.gameObject.SetActive(false);
        rd.ChangeUseage(rollUseage.Level);
        GameManagerInMain.Instance.activeRoll();

    }

    public IEnumerator StoreCheck()
    {
        eventText.text = "상점 발견";
        eventImg.gameObject.SetActive(true);
        SoundManager.Instance.OpenEvent();
        anim.DORestartAllById("startevent");
        yield return new WaitForSeconds(1);
        eventImg.gameObject.SetActive(false);

        rd.ChangeUseage(rollUseage.Move);
        //GameManagerInMain.Instance.activeRoll();
        ptu.OpenStore();

    }

    public IEnumerator DiscoveryCheck()
    {
        eventText.text = "보물 발견";
        eventImg.gameObject.SetActive(true);
        anim.DORestartAllById("startevent");
        SoundManager.Instance.OpenEvent();
        yield return new WaitForSeconds(1);
        eventImg.gameObject.SetActive(false);

        //rd.ChangeUseage(rollUseage.Move);
        //GameManagerInMain.Instance.activeRoll();
        ptu.OpenDiscovery();

    }

    public IEnumerator GameEventCheck()
    {
        eventText.text = "돌발 상황 발생";
        eventImg.gameObject.SetActive(true);
        anim.DORestartAllById("startevent");
        SoundManager.Instance.OpenEvent();
        yield return new WaitForSeconds(1);
        //eventImg.gameObject.GetComponent<EventSet>().StorySet();
        eventImg.gameObject.SetActive(false);

        //rd.ChangeUseage(rollUseage.Move);
        //GameManagerInMain.Instance.activeRoll();
        ptu.OpenEvent();

    }
}
