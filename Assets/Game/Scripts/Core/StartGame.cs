using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartGame : MonoBehaviour
{
    [SerializeField]
    GameObject startIMG;
    [SerializeField]
    int enemycount;
    [SerializeField]
    int enemyIndex;
    [SerializeField]
    bool isTest;

    [SerializeField]
    GameObject[] lights;
    [SerializeField]
    GameObject bossBGM;
    [SerializeField]
    GameObject battleBGM;
    // Start is called before the first frame update
    void Start()
    {
        if(isTest)
        {
            DataConfig.count = enemycount;
            DataConfig.hard = enemyIndex;

        }

        for(int i =0;i<3;i++)
        {
            lights[i].gameObject.SetActive(false);
        }

        if(DataConfig.hard == 7)
        {
            lights[2].SetActive(true);
        }else
        {
            int temp =Random.Range(0, 2);
            lights[temp].SetActive(true);
        }

        //ÁÖ»çÀ§ ±¼·È´Ù Ä¡°í...

        StartCoroutine(GameSt());
    }


    public IEnumerator GameSt()
    {
        yield return null;
        BattleManager.Instance.PlayerSpawn();
        BattleManager.Instance.SpawnEnemy();
        startIMG.SetActive(true);
        yield return new WaitForSeconds(2);
        BattleManager.Instance.QueBattle();
        if(DataConfig.hard < 7)
        {
            battleBGM.SetActive(true);
        }else
        {
            bossBGM.SetActive(true);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
