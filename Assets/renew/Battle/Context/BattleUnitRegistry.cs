using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 전투에 참여하는 Player와 Enemy의 공식 목록이며 Spawn·Turn·Status·Death를 잇는 브릿지다.
/// Enemy는 생성 순서대로 List에 등록되어 기본 행동 순서를 보장하고, EnemyTurnRunner는 매턴 이 목록의 Queue 스냅샷을 실행한다.
/// 최종 구조에서는 Unit별 점유 타일까지 함께 소유해 UnregisterUnit 한 번으로 참가 목록과 점유 정보를 동시에 제거한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleUnitRegistry : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private List<GameObject> enemies = new List<GameObject>();

    public GameObject Player => player;
    public IReadOnlyList<GameObject> Enemies => enemies;

    public event Action<GameObject> PlayerChanged;
    public event Action<GameObject> EnemyRegistered;
    public event Action<GameObject> EnemyUnregistered;

    /// <summary>
    /// 현재 전투에서 사용하는 Player Body를 공식 Player로 등록한다.
    /// 동일 참조를 다시 전달하면 이벤트를 중복 발행하지 않는다.
    /// </summary>
    public void RegisterPlayer(GameObject target)
    {
        if (player == target)
        {
            return;
        }

        player = target;
        PlayerChanged?.Invoke(player);
    }

    /// <summary>
    /// Spawn된 Enemy를 생성 순서대로 목록 끝에 중복 없이 등록한다.
    /// 실제 등록된 경우에만 EnemyRegistered를 발행하고 true를 반환한다.
    /// </summary>
    public bool RegisterEnemy(GameObject enemy)
    {
        RemoveMissingEnemies();
        if (enemy == null || enemies.Contains(enemy))
        {
            return false;
        }

        enemies.Add(enemy);
        EnemyRegistered?.Invoke(enemy);
        return true;
    }

    /// <summary>
    /// 사망·소환 해제된 Enemy를 공식 참가 목록에서 제거한다.
    /// 최종 구조에서는 같은 API가 점유 타일까지 함께 제거해 두 Registry의 불일치를 원천 차단한다.
    /// 죽는 시점(Enemy 자기 턴 실행 중, 다른 Enemy 처리 중 등)에 직접 호출하면 이 List를 순회 중인
    /// 다른 코드의 컬렉션 변경 예외로 이어질 수 있다. 턴 실행 도중 발생한 사망은 QueueUnregisterEnemy로
    /// 미뤄두고, 순회가 끝난 안전한 시점에만 이 메서드를 직접 호출한다.
    /// </summary>
    public bool UnregisterEnemy(GameObject enemy)
    {
        if (enemy == null || !enemies.Remove(enemy))
        {
            return false;
        }

        EnemyUnregistered?.Invoke(enemy);
        return true;
    }

    private readonly Queue<GameObject> pendingUnregisterEnemies = new Queue<GameObject>();

    /// <summary>
    /// Enemy 사망 처리(BattleEnemyDeathHandler.HandleDied)가 죽은 즉시 호출한다.
    /// 이 시점은 다른 Enemy의 턴 실행 도중일 수 있어 참가 목록을 바로 변경하지 않고 대기열에만 쌓는다.
    /// 같은 Enemy가 중복 등록되면 DrainPendingUnregisters에서 두 번째 호출은 조용히 무시된다
    /// (UnregisterEnemy가 List.Remove 실패 시 false를 반환할 뿐 예외를 던지지 않는다).
    /// </summary>
    public void QueueUnregisterEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        pendingUnregisterEnemies.Enqueue(enemy);
    }

    /// <summary>
    /// 대기열에 쌓인 사망 Enemy를 실제 참가 목록에서 제거한다.
    /// BattleEnemyTurnRunner가 매 적 턴 실행(RunAll) 시작 직전, 아무도 참가 목록을 순회하지 않는
    /// 안전한 시점에 호출해야 한다. 이 시점 전까지는 죽은 Enemy도 Enemies 목록에 그대로 남아있다.
    /// </summary>
    public void DrainPendingUnregisters()
    {
        while (pendingUnregisterEnemies.Count > 0)
        {
            UnregisterEnemy(pendingUnregisterEnemies.Dequeue());
        }
    }

    /// <summary>
    /// 정상 Unregister 경로를 거치지 않고 Unity에서 파괴되어 null로 남은 Enemy 참조를 정리한다.
    /// 주 생명주기 대신 사용하는 기능이 아니라 외부 Destroy에 대한 마지막 복구 장치다.
    /// </summary>
    public void RemoveMissingEnemies()
    {
        enemies.RemoveAll(enemy => enemy == null);
    }

    /// <summary>
    /// Scene 종료 또는 전투 재초기화 시 Player와 Enemy 참가 정보를 모두 비운다.
    /// 현재는 개별 해제 이벤트를 발행하지 않으므로 전투 진행 중에는 호출하지 않는다.
    /// </summary>
    public void Clear()
    {
        player = null;
        enemies.Clear();
    }
}
