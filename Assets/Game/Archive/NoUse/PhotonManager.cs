using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using Photon.Realtime;

public class PhotonManager : MonoBehaviourPunCallbacks
{

    private static PhotonManager instance = null;
    public static PhotonManager Instance => instance;

    //포톤 기능 사용~
    private string gameVersition = "1.0.0b";
    private string roomName = "Multiplay";

    public GameObject player;
    private Vector3 createPosition = new Vector3(0.5f, 10, 0.5f);

    public delegate void PlayerCreatedEvent();

    public event PlayerCreatedEvent playerCreated; // 캐릭터가 생성되었다~!

    private void OnPlayerCreated()
    {
        playerCreated.Invoke();
    }

    private IEnumerator CreatePlayer(Vector3 pos,Charactor temp)
    {
        yield return new WaitForSeconds(1f);
        Quaternion rotation = Quaternion.Euler(0, -90, 0);
        player = PhotonNetwork.Instantiate("Player"+temp, pos, rotation);
        OnPlayerCreated(); // 마지막에 생성 되었음을 알린다.
       // moveMainPlayer mmp = player.GetComponentInParent<moveMainPlayer>();
       // mmp.isMake = true;
        yield return null;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);

        }
        DontDestroyOnLoad(this.gameObject);
    }
    public override void OnEnable()
    {
        Debug.Log("뿡뿡뿡뿡뿡뿡");
        base.OnEnable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void Start()
    {
        
    }


    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            Debug.Log("타이틀 씬");
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.GameVersion = gameVersition;

            Debug.Log("포톤 서버와 초당 데이터 통신 수 : " + PhotonNetwork.SendRate);
        }
        else if (SceneManager.GetActiveScene().buildIndex == 1)
        {
            Debug.Log("메인씬");

            if (PhotonNetwork.IsMasterClient)
            {
                TurnManager.Instance.InitTurn();
                nodeManager.Instance.Setting();
            }

        }
    }

    public void spawnMyCharactor(Charactor chara)
    {
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(CreatePlayer(createPosition,chara));

    }

    public void ConnectToPhoton(int x = 0)
    {
        if (!PhotonNetwork.IsConnected)
        {
           switch(x)
            {
                case 0:
                    {                 
                        PhotonNetwork.ConnectUsingSettings();
                    }
                    break;
                default:
                    {
                        roomName += x.ToString();
                        PhotonNetwork.ConnectUsingSettings();
                    }
                    break;


            }
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("포톤에 접속 : " + PhotonNetwork.IsConnected);

        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("로비 접속 : " + PhotonNetwork.InLobby);
        JoinRoom();
    }

    public void JoinRoom()
    {
        RoomOptions options = new RoomOptions();
        options.MaxPlayers = 0;
        options.IsOpen = true;
        options.IsVisible = true;
        PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("방 생성 완료. 현재 방의 이름 : " + PhotonNetwork.CurrentRoom.Name);
    }
    public override void OnJoinedRoom()
    {
        Debug.Log("방입장 : " + PhotonNetwork.InRoom + " , 인원수 : " + PhotonNetwork.CurrentRoom.PlayerCount);
        PlayerConfig.ApplyToPhoton();

        LoadMainScene();

    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("방에 접속 실패....." + returnCode + "/" + message + "방 생성 시도");
        JoinRoom();
    }


    public void LoadMainScene()
    {

        
        Debug.Log("Try Main Scene Load....");

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(1);
        }
        else
        {
            Debug.Log("Not Master Client....");
        }
    }

    public void LoadbattleScene()
    {



        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(2);
        }
        else
        {
        }
    }
}
