using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class GameManagerInMain : MonoBehaviour
{
    private static GameManagerInMain instance;
    public static GameManagerInMain Instance => instance;

    private GameObject player;
    public GameObject Player => player;

    [Header("LinkSelect")]
    [SerializeField]
    LinkSelect link;

    [Header("플레이어 소환 프리팹")]
    [SerializeField]
    GameObject[] playerPrefs;


    [Header("플레이어가 소환될 위치")]
    [SerializeField]
    Transform playerPos;

    [Header("카메라")]
    [SerializeField]
    cameraChase cam;

    [Header("노드 매니저")]
    [SerializeField]
    nodeManagerInMain nodeManager;

    [Header("roll 버튼")]
    [SerializeField]
    GameObject rollbtn;

    [Header("Gold Text")]
    [SerializeField]
    TextMeshProUGUI goldText;

    [Header("Turn Text")]
    [SerializeField]
    TextMeshProUGUI turnText;

    [Header("유저의 이름")]
    [SerializeField]
    TextMeshProUGUI playerName;

    [Header("유저의 타이틀")]
    [SerializeField]
    TextMeshProUGUI playerTitle;

    [Header("유저의 EventCheck")]
    [SerializeField]
    CheckEvent checkEvent;


    [SerializeField]
    ProfileUI pui;

    private int gold;
    public int Gold => gold;

    int turnCount;

    [SerializeField]
    GameObject portal;


    public void SetGold(int x)
    {
        gold += x;
        DataConfig.playerMoney = gold;
        ShowGoldUI();
    }

    public void ShowGoldUI()
    {
        goldText.text = "GOLD " + gold.ToString() + "G";
    }

    public void AddTurn()
    {
        turnCount++;
        ShowTurnUI();
    }

    public void ShowTurnUI()
    {
        turnText.text = "TURN " + DataConfig.turn;
    }

    public bool canUseGold(int cost)
    {
        bool temp = false;
        if(gold - cost >= 0)
        {
            temp = true;
        }

        return temp;
    }

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }

        playerName.text = DataConfig.playerName;
    }

    public void setTitle()
    {
        if (!DataConfig.isSelected[0]) return; 
        string text1 = "고양이";
        string text2 = "강아지";
        string text3 = "토끼";
        //DataConfig.stage = 1;
        playerTitle.text = DataConfig.tribe == 0 ? text1 : DataConfig.tribe == 1 ? text2 : text3;

    }

    public void AddTitle(string temp)
    {
        playerTitle.text = temp + " " +playerTitle.text;
    }

    public void activeRoll()
    {
        rollbtn.SetActive(true);
    }

    public void DeactiveRoll()
    {
        rollbtn.SetActive(false);

    }

    public void Start()
    {
        gold = DataConfig.playerMoney;
        turnCount = DataConfig.turnCount;
        ShowGoldUI();
        if (DataCheck())
        {
            //초기화 되어있다면
            Debug.Log("플레이어 불러오기");
            StartCoroutine(nameof(SpawnCor));
        }
        else
        {
            Debug.Log("플레이어 생성");
            StartCoroutine(nameof(LinkCor));

        }
        playerName.text = DataConfig.playerName;
        setTitle();

        if (DataConfig.isSelected[0])
        {
            portal.SetActive(false);
        }
        else
        {
            portal.SetActive(true);
        }
        ShowTurnUI();
    }

    public IEnumerator SpawnCor()
    {
        yield return new WaitForSeconds(1f);
        SpawnPlayer();
    }
    public IEnumerator LinkCor()
    {
        yield return new WaitForSeconds(1f);
        link.LinkTribe();
    }

   // public void 

    public bool DataCheck()
    {
        //DataConfig.LoadData();
        bool temp = DataConfig.isSelected[0] ? true : false;

        return temp;
    }

    public void setTribe()
    {
        int tempTribe = link.returntribe();
        DataConfig.isSelected[0] = true;
        DataConfig.stage = 2;
        GiveNormalDeck();

        SpawnPlayer(tempTribe);
        pui.SetImg();
    }

    public void OpenTitleSelection()
    {
        Debug.Log("1번 통과");
        link.LinkTitle();
    }

    public void OpenJobSelection()
    {
        link.LinkWork();
    }

    public void activeRoll(rollUseage ru)
    {
        rollbtn.GetComponentInChildren<RollDice>().ChangeUseage(ru);
        rollbtn.SetActive(true);
    }

    public void SpawnPlayer(int tribeIndex)
    {
        player = Instantiate(playerPrefs[tribeIndex], playerPos);
        //DataConfig.tribe = tribeIndex;



        const int baseStat = 2;
        DataConfig.SaveData(baseStat + DataPool.Instance.powerDatabase1.power1[tribeIndex].strUP,
                            baseStat + DataPool.Instance.powerDatabase1.power1[tribeIndex].wisUP,
                            baseStat + DataPool.Instance.powerDatabase1.power1[tribeIndex].dexUP,
                            baseStat + DataPool.Instance.powerDatabase1.power1[tribeIndex].vitUP,
                             tribeIndex,0);
        player.GetComponent<PlayerBase>().Init();
        player.GetComponent<PlayerBase>().DeckInit();
        cam.SetTarget(player);
        activeRoll(rollUseage.Move);
        equipPlayer();
        portal.SetActive(false);
    }

    public void equipPlayer()
    {
        int left = DataConfig.leftHand;
        int right = DataConfig.rightHand;
        int head = DataConfig.head;
        int body = DataConfig.body;

        weaponSet weapon = player.GetComponent<weaponSet>();
        weapon.EquipAdapt(left,right,body,head);
        
       
    }

    public void SpawnPlayer()
    {
        Vector3 playerpos = nodeManager.nodes[DataConfig.nowPos].transform.position;
        playerpos.y = 0;
        playerPos.transform.position = playerpos;
        playerPos.gameObject.GetComponent<NodeBasePlayerMov>().SetNodeIndex(DataConfig.nowPos);
        //여기 playerPos를 dataconfig에서 읽어와서, 이전 노드의 위치로 보내는 코드가 추가되어야함.
        player = Instantiate(playerPrefs[DataConfig.tribe], playerPos);
        
        player.GetComponent<PlayerBase>().Init();
        //player.GetComponent<PlayerBase>().DeckInit();
        cam.SetTarget(player);
        activeRoll(rollUseage.Move);
        equipPlayer();
        portal.SetActive(false);
        if (DataConfig.isBattled == true)
        {
            checkEvent.check(nodeState.start);
        }

    }

    public void GiveNormalDeck()
    {
        DataConfig.InitCard();
        DataConfig.AddCard(0, 0);
        DataConfig.AddCard(1, 0);
        DataConfig.AddCard(2, 1);
        DataConfig.AddCard(3, 1);
        DataConfig.AddCard(4, 2);
        DataConfig.AddCard(5, 2);
        DataConfig.AddCard(6, 3);
        DataConfig.AddCard(7, 3);
        DataConfig.AddCard(8, 8);
        DataConfig.AddCard(9, 8);

        DataConfig.AddDic(0, 2);
        DataConfig.AddDic(1, 2);
        DataConfig.AddDic(2, 2);
        DataConfig.AddDic(3, 2);
        DataConfig.AddDic(8, 2);

        DataConfig.SaveData();
    }
}
