using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 NPC 타일에 도착했을 때 이벤트만 전달한다.
/// NPC 오브젝트는 Transform상 타일의 자식은 아니다(부모로 넣으면 SetParent가 Scale을 다시
/// 계산하면서 틀어지는 문제가 있었다). 대신 스폰 시 자신이 속한 MapInfo.NpcTrigger에 이 컴포넌트
/// 참조를 등록해두므로, 도착 타일에서 그 참조 하나로 바로 찾을 수 있다 — 별도 등록·조회 테이블은
/// 여전히 필요 없다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleNpcInteractionTrigger : MonoBehaviour
{
    [SerializeField] private UnityEvent interacted = new UnityEvent();

    public static event Action<BattleNpcInteractionTrigger> NpcInteracted;
    public MapInfo Tile { get; private set; }
    public NpcData Data { get; private set; }

    public void SetTile(MapInfo tile)
    {
        Tile = tile;
    }

    /// <summary>스폰 시점에 어떤 NpcData로 생성됐는지 기록한다. NpcDatabase가 비어 있거나 연결 안 됐으면 null일 수 있다.</summary>
    public void SetData(NpcData data)
    {
        Data = data;
    }

    public void InvokeArrivalEvent()
    {
        Debug.Log($"NPC 도착 이벤트: {name}", this);
        interacted.Invoke();
        NpcInteracted?.Invoke(this);
    }
}
