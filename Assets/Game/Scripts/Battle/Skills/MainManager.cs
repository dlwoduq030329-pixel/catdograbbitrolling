using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainManager : MonoBehaviour
{

    public static MainManager Instance = null;
    public GameObject player;
    [SerializeField]
    GameObject playerparent;
    Vector3 offset = new Vector3(0, 5f, 0);

    public delegate void ChaseCam(GameObject player);
    public ChaseCam chaseCam;

    // Start is called before the first frame update
    void Awake()
    {
        if(Instance ==null)
        {
            Instance = this;
        }
    }

    public void OnEnable()
    {
        if (playerState.Instance == null) return;
       if(playerState.Instance.powerIndex!=0)
        {
            InstantiatePlayer();
        }
    }

    public void Start()
    {
        if (playerState.Instance.powerIndex == 0)
        {
            Debug.Log("통과");
            playerState.Instance.powerIndex++;
            UIManager.Instance.SetPowerUI();
        }
        else
        {
            //InstantiatePlayer(playerState.Instance.playerPositionIndex);
        }
    }

    public void InstantiatePlayer(int x = 0)
    {
        StartCoroutine(PlayerMake(x));
    }

    public IEnumerator PlayerMake(int temp)
    {
        player = PhotonNetwork.Instantiate("Player" + PlayerConfig.tribe, offset,Quaternion.identity);//nodeManager.Instance.nodes[temp].transform.position,Quaternion.identity
        yield return null;

        player.transform.SetParent(playerparent.transform,false);
        //MainSceneAni msa = player.GetComponent<MainSceneAni>();
        //msa.LinkIn();
        if(TurnManager.Instance!=null)
        {
            //TurnManager.Instance.InitTurn();
            if(UIManager.Instance!=null)
            {
                UIManager.Instance.RefreshDiceButton();

            }
        }

        PlayerConfig.ApplyToPhoton();

        chaseCam?.Invoke(player);
        
    }

    public void CheckNodeEvent(nodeState x)
    {
        switch(x)
        {
            case nodeState.None:
                TurnManager.Instance.EndTurn();
                break;
            case nodeState.battle:
                //TurnManager.Instance.EndTurn();
                playerState.Instance.enemyCount = Random.Range(1, 7);
                PhotonManager.Instance.LoadbattleScene();
                break;
            case nodeState.store:
                TurnManager.Instance.EndTurn();
                break;
            case nodeState.discovery:
                TurnManager.Instance.EndTurn();
                break;
            case nodeState.gameevent:
                TurnManager.Instance.EndTurn();
                break;

        }
    }

    //생성된 플레이어 트리거로 전투씬 이동, 카드 사용, 오토 배틀 메인씬으로 이동, 
}
