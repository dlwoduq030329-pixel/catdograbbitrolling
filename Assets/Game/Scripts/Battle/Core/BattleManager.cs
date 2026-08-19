using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Android;

public class BattleManager : MonoBehaviour
{
    [SerializeField]
    Transform[] playerMeleePos;
    [SerializeField]
    Transform[] playerRangePos;
    [SerializeField]
    Transform[] enemyMeleePos;
    [SerializeField]
    Transform[] enemyRangePos; 
    [SerializeField]
    getPlayerInfoUI playerInfoUI;
    [SerializeField]
    GameObject Win;
    [SerializeField]
    RewardPopUp rePop;
    [SerializeField]
    BattleCard card;
    [SerializeField]
    Transform[] scare;
    [SerializeField]
    Transform[] poses;
    [SerializeField]
    int enemyCount;
    [SerializeField]
    GameObject endingUI;
    [SerializeField]
    ResultPopup popup;
    private static BattleManager instance;
    public static BattleManager Instance => instance;
    GameObject player;
    public GameObject Player => player;
    List<GameObject> enemies = new List<GameObject>();

    private List<GameObject> players = new List<GameObject>();

    float battleTime = 0;


    bool enemyWin = false;
    bool playerWin = false;
    public bool EnemyWin => enemyWin;
    public bool PlayerWin => playerWin;

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
    }

    public void GameOver()
    {
        endingUI.GetComponent<loadScene>().Init(false);
        endingUI.SetActive(true);
    }


    public void PlayerSpawn()
    {
        int playerIndex = DataConfig.tribe;
        //int playerIndex = DataConfig.tribe;

        float range1 = DataConfig.leftDa == null ? 1 : DataConfig.leftDa.attackRange;
        float range2 = DataConfig.rightDa == null ? 1 : DataConfig.rightDa.attackRange;

        float realRange = Mathf.Max(range1, range2);

        


        //유저의 공격 사거리에 비례해 앞 또는 뒤에 위치할지 정하는 코드. 현재 무기 기획이 안되어있어 불가능.
        //dataconfig에서 장비 2개 받아온 뒤 사거리 계산 코드 및 ismelee를 판정
        bool isMelee = realRange < 9f;

        switch(isMelee)
        {
            case true:
                {
                    int spawnPos = Random.Range(0, playerMeleePos.Length);
                    player = Instantiate(UnitPool.Instance.playerPrefabs[playerIndex], playerMeleePos[spawnPos].position, Quaternion.identity);
                    BattlePlayer bp = player.GetComponent<BattlePlayer>();
                    bp.rangeSet(realRange);
                    playerInfoUI.SetPlayer(bp);
                    if(realRange == 0)
                    {
                        bp.Ps = playerAttackST.Hand;
                    }else
                    {
                        bp.Ps = playerAttackST.Melee;
                    }
                    bp.Init();
                    players.Add(player);
                }
                break;
            case false:
                {
                    int spawnPos = Random.Range(0, playerRangePos.Length);
                    player = Instantiate(UnitPool.Instance.playerPrefabs[playerIndex], playerRangePos[spawnPos].position, Quaternion.identity);
                    BattlePlayer bp = player.GetComponent<BattlePlayer>();
                    bp.rangeSet(realRange);

                    //playerInfoUI.SetPlayer(bp);
                    bp.Ps = playerAttackST.Range;
                    bp.Init();
                    players.Add(player);

                }
                break;
        }

        player.GetComponent<BattlePlayerEquip>().SetEquip();
        card.StartDraw();

       //GameObject player = Instantiate(UnitPool.Instance.playerPrefabs[playerIndex])
    }

    public void enemyChangeTarget(GameObject temp)
    {
        players.Add(temp);
        foreach(var te in enemies)
        {
            te.GetComponent<EnemyStateMachine>()?.TargetChange(temp);
        }
    }

    public void removePlayer(GameObject temp)
    {
       players.Remove(temp);
        foreach (var te in enemies)
        {
            te.GetComponent<EnemyStateMachine>()?.ChangePlayerState(playerSt.Detect);
        }

    }


    public void enemyChangeDetect()
    {
        foreach (var te in enemies)
        {
            te.GetComponent<EnemyStateMachine>()?.ChangePlayerState(playerSt.Detect);
        }
    }

    public void QueBattle()
    {
        player.GetComponent<PlayerStateMachine>().ChangePlayerState(playerSt.Idle);

        foreach(var ene in enemies)
        {
            //ene.GetComponent<BattlePlayer>().EnemyInit(1);
            ene.GetComponent<EnemyStateMachine>().ChangePlayerState(playerSt.Idle);

        }
        BattlePlayer temp = player.GetComponent<BattlePlayer>();
        playerInfoUI.SetPlayer(temp);
        temp.StartHealAp();
    }

    public void SpawnEnemy()
    {
        int level = DataConfig.hard;
        int count = DataConfig.count;
        if (level == 7) count = 1;
        Debug.Log("적 소환 로그 적의 수 : "+count);
        for (int i =0;i<count;i++)
        {
            //int x  = Random.Range(1, level+1);
            int x = Random.Range(1, level + 1);
            if (level == 7) x = 7;
            Debug.Log(UnitPool.Instance.unit.unitDatas[x].enemyHP);

            bool isMelee = UnitPool.Instance.unit.unitDatas[x].attackstate == attackState.melee ? true : false;
            if(isMelee)
            {
                if(i <3)
                {
                    var temp = Instantiate(UnitPool.Instance.unit.unitDatas[x].enemyPrefab, enemyMeleePos[i].position, Quaternion.identity);
                    UnitData tempData = UnitPool.Instance.unit.unitDatas[x];
                    InitScaledEnemy(temp.GetComponent<BattlePlayer>(), tempData);
                    enemies.Add(temp);
                    EnemySkill es;
                    if(temp.TryGetComponent<EnemySkill>(out es))
                    {
                        es.InitPos(scare, poses);
                    }
                }else
                {
                    var temp = Instantiate(UnitPool.Instance.unit.unitDatas[x].enemyPrefab, enemyRangePos[i-3].position, Quaternion.identity);
                    UnitData tempData = UnitPool.Instance.unit.unitDatas[x];
                    InitScaledEnemy(temp.GetComponent<BattlePlayer>(), tempData);
                    enemies.Add(temp);
                    EnemySkill es;
                    if (temp.TryGetComponent<EnemySkill>(out es))
                    {
                        es.InitPos(scare, poses);
                    }

                }
            }
            else
            {
                if (i < 3)
                {
                    var temp = Instantiate(UnitPool.Instance.unit.unitDatas[x].enemyPrefab, enemyRangePos[i].position, Quaternion.identity);
                    UnitData tempData = UnitPool.Instance.unit.unitDatas[x];
                    InitScaledEnemy(temp.GetComponent<BattlePlayer>(), tempData);
                    enemies.Add(temp);
                    EnemySkill es;
                    if (temp.TryGetComponent<EnemySkill>(out es))
                    {
                        es.InitPos(scare, poses);
                    }

                }
                else
                {
                    var temp = Instantiate(UnitPool.Instance.unit.unitDatas[x].enemyPrefab, enemyMeleePos[i-3].position, Quaternion.identity);
                    UnitData tempData = UnitPool.Instance.unit.unitDatas[x];
                    InitScaledEnemy(temp.GetComponent<BattlePlayer>(), tempData);
                    enemies.Add(temp);
                    EnemySkill es;
                    if (temp.TryGetComponent<EnemySkill>(out es))
                    {
                        es.InitPos(scare, poses);
                    }

                }

            }
            foreach(var ene in enemies)
            {
                
            }
        }
    }

    void InitScaledEnemy(BattlePlayer enemy, UnitData data)
    {
        int progression = Mathf.Max(0, DataConfig.stage - 2);
        float hpMultiplier = 1f + (0.25f * progression);
        float damageMultiplier = 1f + (0.15f * progression);
        enemy.EnemyInit(
            data.enemyHP * hpMultiplier,
            data.attackDamage * damageMultiplier,
            data.attackRange,
            data.attackSpeed);
    }

    public GameObject returnClosePlayer(GameObject temp)
    {
        float distance = float.MaxValue;
        int targetNum = 0;
        for(int i =0;i<players.Count;i++)
        {
            float dis = Vector3.Distance(players[i].transform.position, temp.transform.position);
            if(dis < distance)
            {
                distance = dis;
                targetNum = i;
            }
        }

        return players[targetNum];
    }

    public GameObject changeOtherTarget(GameObject temp)
    {
        if (enemies.Count <=1) return null;


        int index = enemies.IndexOf(temp);

        int nextIndex = (index + 1) % enemies.Count;

        return enemies[nextIndex];
    }

    public void RemoveEne(GameObject target)
    {
        enemies.Remove(target);
        if(enemies.Count == 0)
        {
            //게임 오버

            if(DataConfig.hard == 7)
            {
                //endingUI.GetComponent<loadScene>().Init(true);
                //endingUI.SetActive(true);
                popup.Init((DataConfig.hard * 10) * DataConfig.count, battleTime, DataConfig.tribe);
                Win.gameObject.SetActive(true);
                rePop.randomReward();


            }
            else
            {
                Time.timeScale = 0;


                popup.Init((DataConfig.hard * 10) * DataConfig.count, battleTime,DataConfig.tribe);
                Win.gameObject.SetActive(true);
                rePop.randomReward();

            }
        }
    }

    public void AddEnemy(GameObject target)
    {
        enemies.Add(target);
    }

    public void RemovePlayer(GameObject target)
    {
        player = null;
        //게임 오버
    }

    public GameObject DetectCloseEnemy(Vector3 pos)
    {
        float distance = float.MaxValue;
        GameObject tempTarget = null;
        foreach(var ene in enemies)
        {
            float tempDis = Vector3.Distance(pos, ene.transform.position);
            if (tempDis <= distance)
            {
                distance = tempDis;
                tempTarget = ene;
            }
        }

        if(tempTarget == null)
        {
            //게임 오버 시그널
            playerWin = true;
        }

        return tempTarget;
    }

    public GameObject DetectCloseEnemy(GameObject target,Vector3 pos)
    {
        float distance = float.MaxValue;
        GameObject tempTarget = null;
        foreach (var ene in enemies)
        {
            if (ene == target) continue;    
            float tempDis = Vector3.Distance(pos, ene.transform.position);
            if (tempDis <= distance)
            {
                distance = tempDis;
                tempTarget = ene;
            }
        }

        return tempTarget;
    }

    public GameObject DetectFarEnemy(GameObject target)
    {
        float distance = float.MinValue;
        GameObject tempTarget = null;

        foreach (var ene in enemies)
        {
            if (ene == target) continue;
            float tempDis = Vector3.Distance(target.transform.position,ene.transform.position);
            if (tempDis >= distance)
            {
                distance = tempDis;
                tempTarget = ene;
            }
        }

        return tempTarget;
    }


    public GameObject[] DetectTwoEnemy()
    {
        GameObject[] result = new GameObject[2];

        float firstDistance = float.MaxValue;
        float secondDistance = float.MaxValue;

        GameObject firstEnemy = null;
        GameObject secondEnemy = null;

        foreach (var ene in enemies)
        {
            float dis = Vector3.Distance(transform.position, ene.transform.position);

            if (dis < firstDistance)
            {
                secondDistance = firstDistance;
                secondEnemy = firstEnemy;

                firstDistance = dis;
                firstEnemy = ene;
            }
            else if (dis < secondDistance)
            {
                secondDistance = dis;
                secondEnemy = ene;
            }
        }

        result[0] = firstEnemy;
        result[1] = secondEnemy;

        return result;
    }

    private void Update()
    {
        battleTime += Time.deltaTime;
    }
}
