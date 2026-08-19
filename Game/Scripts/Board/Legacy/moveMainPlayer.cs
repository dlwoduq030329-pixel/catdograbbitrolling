using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class moveMainPlayer : MonoBehaviour
{
    public int movCount = 0;
    public int nowPosCount;
    bool moving = false;
    public bool isMake = false;
    Animator playerAni;

    public MainSceneAni mma;
    // Start is called before the first frame update
    void Start()
    {
        /*if(playerState.Instance != null && nodePool.Instance!= null)
        {
            this.transform.position = nodePool.Instance.returnnodePos(playerState.Instance.playerPositionIndex);
            nowPosCount = playerState.Instance.playerPositionIndex;

            if(MainManager.Instance!=null && playerState.Instance.tribe != string.Empty)
            {
                MainManager.Instance.InstantiatePlayer();
            }
        }

        if(rollDice.Instance != null)
        {
            rollDice.Instance.mov += playerMov; 
        }*/

      //  StartCoroutine(Loading());
        //if(playerState)
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void playerMov(int x)
    {
        if (moving) return;
        movCount = 0;
        movCount = x;
        Debug.Log("이동 실시");
        StartCoroutine(SetMov());
    }

    public IEnumerator SetMov()
    {
        moving = true;
        if (mma != null)
        {

           // mma.ChangePlayerState(aniState.Walk.ToString());
        }
        for (int i = movCount; i > 0; i--)
        {
            //Vector3 targetPos = nodePool.Instance./returnnodePos(nodePool.Instance.nodeorigin[nowPosCount].NextTileid);
            //while(Vector3.Distance(this.transform.position,targetPos) >= 0.01f)
            {
                //  this.transform.position = Vector3.MoveTowards(this.transform.position, targetPos, 1f * Time.deltaTime);
                yield return null;
                //  }
                playerState.Instance.playerPositionIndex++;
                nowPosCount = playerState.Instance.playerPositionIndex;
                yield return null;
            }
            moving = false;
            if (mma != null)
            {

               // mma.ChangeIdle();
            }
            //MainManager.Instance.CheckNodeEvent(nodePool.Instance.nodeorigin[nowPosCount].state);
            //TurnManager.Instance.EndTurn();
        }

        void OnEnable()
        {
            isMake = false;
            /*if (rollDice.Instance != null)
                rollDice.Instance.mov += playerMov;*/
            //StartCoroutine(Loading());

        }

        void OnDisable()
        {
            if (rollDice.Instance != null)
                rollDice.Instance.mov -= playerMov;
        }
/*
        private IEnumerator Loading()
        {
            yield return new WaitUntil(() =>
           playerState.Instance != null &&
           nodePool.Instance != null &&
           MainManager.Instance != null
           && rollDice.Instance != null
            );

            if (playerState.Instance != null && nodePool.Instance != null)
            {
                //this.transform.position = nodePool.Instance.returnnodePos(playerState.Instance.playerPositionIndex);
                nowPosCount = playerState.Instance.playerPositionIndex;

                if (MainManager.Instance != null && playerState.Instance.tribe != string.Empty)
                {
                    MainManager.Instance.InstantiatePlayer();
                }
            }

            if (rollDice.Instance != null)
            {
                rollDice.Instance.mov += playerMov; // 왜 구독자 연결이 안되나...
            }

            yield return null;

            UIManager.Instance.disableBlack();
        }*/
    }
}
