using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviourPunCallbacks
{
    public static TurnManager Instance;

    // 현재 턴의 ActorNumber
    public int CurrentTurnActor { get; private set; }

    // 룸에 있는 플레이어 ActorNumber 목록
    private List<int> turnOrder = new List<int>();

    // 턴이 바뀌었을 때 UI 등이 구독할 이벤트
    public event Action<int> OnTurnChanged;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 룸에 모두 들어온 뒤, 마스터 클라이언트가 턴 초기화
    /// </summary>
    public void InitTurn()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        turnOrder.Clear();

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            turnOrder.Add(p.ActorNumber);
        }

        // 랜덤으로 첫 턴 지정
        int firstIndex = UnityEngine.Random.Range(0, turnOrder.Count);
        int firstActor = turnOrder[firstIndex];
        Debug.Log("설정완료");
        photonView.RPC(nameof(RPC_SetTurn), RpcTarget.All, firstActor);
    }

    /// <summary>
    /// 턴을 다음 플레이어로 넘김 (마스터만 호출)
    /// </summary>
    public void EndTurn()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        int currentIndex = turnOrder.IndexOf(CurrentTurnActor);
        int nextIndex = (currentIndex + 1) % turnOrder.Count;
        int nextActor = turnOrder[nextIndex];

        photonView.RPC(nameof(RPC_SetTurn), RpcTarget.All, nextActor);
    }

    [PunRPC]
    void RPC_SetTurn(int actorNumber)
    {
        CurrentTurnActor = actorNumber;
        Debug.Log($"현재 턴: Actor {actorNumber}");

        OnTurnChanged?.Invoke(actorNumber);
    }

    /// <summary>
    /// 로컬 플레이어가 현재 턴인지 확인
    /// </summary>
    public bool IsMyTurn()
    {
        return PhotonNetwork.LocalPlayer.ActorNumber == CurrentTurnActor;
    }
}