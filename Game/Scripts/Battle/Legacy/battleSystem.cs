using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class battleSystem : MonoBehaviour
{
    public static battleSystem Instance;
    bool isReady = false;
    public delegate void GameStart();
    public GameStart cueSign;

    public GameStart startMov;
    public float nowAp = 10;
    [SerializeField]
    GameObject gameEndIMG;
  
    [SerializeField]
    GameObject rewardIMG;
    [SerializeField]
    Button[] rewardBTNS;
    [SerializeField]
    public List<Vector3> playerPos = new List<Vector3>();
    [SerializeField]
    public List<Vector3> enemyPos = new List<Vector3>();
    [SerializeField]
    Slider apslider;
    [SerializeField]
    TextMeshProUGUI aptext;

    public List<int> deck = new List<int>();
    public List<int> hand = new List<int>();

    public List<GameObject> enemy = new List<GameObject>();
    public List<GameObject> player = new List<GameObject>();

    public delegate void cardUse(string temp);
    public cardUse use;
    public bool can = false; // 카드 사용 가능
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public void cardUseLink(string temp)
    {
        use?.Invoke(temp);
    }

    public void OnEnable()
    {
        //playerState.Instance.setplayer += CreatePlayer;
    }

    public void OnDisable()
    {
       // playerState.Instance.setplayer += CreatePlayer;
       for(int i =0;i<3;i++)
        {
            rewardBTNS[i].onClick.RemoveAllListeners();
        }
    }

    public void Start()
    {
        //GamePlay();
        playerPos.Add(new Vector3(-2,1,0));
        playerPos.Add(new Vector3(-2, 1, -3));
        playerPos.Add(new Vector3(-2, 1, 3));
        playerPos.Add(new Vector3(-4, 1, 0));
        playerPos.Add(new Vector3(-4, 1, -3));
        playerPos.Add(new Vector3(-4, 1, 3));
    
        enemyPos.Add(new Vector3(2, 1, 0));
        enemyPos.Add(new Vector3(2, 1, -3));
        enemyPos.Add(new Vector3(2, 1, 3));
        enemyPos.Add(new Vector3(4, 1, 0));
        enemyPos.Add(new Vector3(4, 1, -3));
        enemyPos.Add(new Vector3(4, 1, 3));

    }

    public void CreatePlayer(int x,float range)
    {
       
        Vector3 pos;
        switch (x)
        {
            default:
                {
                    if(range<3)
                    {
                        pos = playerPos[Random.Range(0, 3)];
                        playerPos.Remove(pos);
                    }else
                    {
                        pos = playerPos[Random.Range(3, 6)];
                        playerPos.Remove(pos);
                    }
                    GameObject temp = PhotonNetwork.Instantiate("Player",pos,Quaternion.identity);
                    player.Add(temp);
                }
                break;
        }
    }

    public void battleEnd()
    {
        // 게임 오버 UI
        Debug.Log("게임오버");
        gameEndIMG.SetActive(true);
        LinkBTN();
    }

    public void LinkBTN()
    {
        for(int i =0;i<rewardBTNS.Length;i++)
        {
            int temp = 1;//Random.Range(0, 2);

            switch(temp)
            {
                case 0:
                    {
                       int x =  Random.Range(0, DataPool.Instance.equipDatabase.equip.Count);
                        rewardBTNS[i].image.sprite = DataPool.Instance.equipDatabase.equip[x].myEquipSprite;
                    }
                    break;
                case 1:
                    {
                        int x = Random.Range(0, DataPool.Instance.cardDatabase.cards.Count);
                        rewardBTNS[i].image.sprite = DataPool.Instance.cardDatabase.cards[x].myCardSprite;
                        Debug.Log(DataPool.Instance.cardDatabase.cards[x].name);
                        rewardBTNS[i].onClick.AddListener(() => myDeckManager.Instance.AddCardLow(DataPool.Instance.cardDatabase.cards[x]));
                    }
                    break;
            }

            rewardBTNS[i].onClick.AddListener(battleEndBtn);
        }
    }

   

    public void battleEndBtn()
    {
        PhotonManager.Instance.LoadMainScene();
    }

    public void CreateEnemy()
    {
        enemy.Clear();
        switch(playerState.Instance.enemyIndex)
        {
            default:
                {
                    for(int i = 0;i<playerState.Instance.enemyCount;i++)
                    {
                        GameObject ene = PhotonNetwork.Instantiate("Enemy", enemyPos[i], Quaternion.identity);
                        enemy.Add(ene);
                    }
                }
                break;
        }
    }

    

    public void GamePlay()
    {
        battleDeckInit();
        Debug.Log("실행 성공");
        CreatePlayer(0,playerState.Instance.attackRange);
        CreateEnemy();
        //playerState.Instance.s
    }

    private void Update()
    {
        if (isReady) return;

        if (player.Count >= 1 && enemy.Count == playerState.Instance.enemyCount)
        {
            cueSign?.Invoke();
            
            isReady = true;
        }
    }

    public void MovStart()
    {
        startMov?.Invoke();
    }

    public void battleDeckInit()
    {
        deck.Clear();
        for(int i =0;i<10;i++)
        {
            deck.Add(myDeckManager.Instance.deckcards[i].index);
            //deck.Add(myDeckManager.Instance.deckCards[i].index);
            //0123401234
            //내 덱 카드들의 index를 이용해 list생성 이는 실제 배틀에서 사용되는 덱을 의미.
            //이 List는 카드의 index만을 담고있다.
        }
    }



    public void DrawCard(int x)
    {
        deck.Remove(x);
        hand.Add(x);
    }

    public void UseCard(int x)
    {
        hand.Remove(x);
        deck.Add(x);
    }
    
    public void SetAP(float temp,float max)
    {
        nowAp = temp;
        apslider.value = temp/max;
        aptext.text = ((int)temp).ToString() + "AP";
    }
}
