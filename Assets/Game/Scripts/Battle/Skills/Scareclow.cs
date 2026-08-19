using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Scareclow : MonoBehaviour
{
    [SerializeField]
    LinkHP team;
    [SerializeField]
    LinkHP enemy;

    BattlePlayer player;
    bool isEnemy = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Init(float Hp,bool _isEnemy)
    {
        isEnemy = _isEnemy;
        if(_isEnemy)
        {
            GetComponent<BattlePlayer>().Init(Hp, enemy);
            enemy.gameObject.SetActive(true);
            StartCoroutine(delCor());
        }
        else
        {
            player = GetComponent<BattlePlayer>();
            player.SetPlayer();
            player.Init(Hp, team);
            team.gameObject.SetActive(true);
            BattleManager.Instance.enemyChangeTarget(this.gameObject);
            StartCoroutine(delCorforPlayer());

        }
        Debug.Log("허수아비 소환");
    }

    public void playerDelSet()
    {

    }

    public IEnumerator delCorforPlayer()
    {
        bool isRunning = true;
        float delTime = 0;
        while(isRunning)
        {
            delTime += Time.deltaTime;

            if(delTime >= 5f ||player.HP<=0)
            {
                BattleManager.Instance.enemyChangeDetect();
                isRunning = false;  
            }
            yield return null;  
        }
        BattleManager.Instance.removePlayer(this.gameObject);

        Destroy(this.gameObject);
    }


    public void DelSet()
    {
        StartCoroutine(delCor());
    }

    public IEnumerator delCor()
    {
        yield return new WaitForSeconds(5f);
        BattleManager.Instance.RemoveEne(this.gameObject);
        Destroy(this.gameObject);
    }

    public void OnDisable()
    {
        if (!isEnemy)
        {
            //BattleManager.Instance.enemyChangeTarget(BattleManager.Instance.Player);
        } else
        {
           // BattleManager.Instance.RemoveEne(this.gameObject);
        }
    }
}
