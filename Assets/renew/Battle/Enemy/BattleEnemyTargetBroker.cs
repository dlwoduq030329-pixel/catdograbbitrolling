using UnityEngine;

/// <summary>
/// Player와 Enemy의 등록 순서와 관계없이 현재 Player Target을 EnemyDetector에 배포한다.
/// Unit 등록과 감지 판정은 담당하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyTargetBroker : MonoBehaviour
{
    private BattleUnitRegistry unitRegistry;
    private bool subscribed;

    /// <summary>Unit Registry 이벤트를 연결하고 이미 등록된 Unit의 Target 상태도 즉시 동기화한다.</summary>
    public void Configure(BattleUnitRegistry registry)
    {
        Unsubscribe();
        unitRegistry = registry;
        Subscribe();
        SynchronizeRegisteredUnits();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>새 Player가 등록되면 기존 모든 Enemy에 Target을 다시 배포한다.</summary>
    private void HandlePlayerChanged(GameObject player)
    {
        if (unitRegistry == null || player == null)
        {
            return;
        }

        foreach (GameObject enemy in unitRegistry.Enemies)
        {
            AssignTarget(enemy, player.transform);
        }
    }

    /// <summary>새 Enemy가 등록되면 현재 Player가 있을 때 해당 Enemy에만 Target을 배포한다.</summary>
    private void HandleEnemyRegistered(GameObject enemy)
    {
        GameObject player = unitRegistry != null ? unitRegistry.Player : null;
        if (player != null)
        {
            AssignTarget(enemy, player.transform);
        }
    }

    private void SynchronizeRegisteredUnits()
    {
        if (unitRegistry != null && unitRegistry.Player != null)
        {
            HandlePlayerChanged(unitRegistry.Player);
        }
    }

    private void Subscribe()
    {
        if (subscribed || unitRegistry == null)
        {
            return;
        }

        unitRegistry.PlayerChanged += HandlePlayerChanged;
        unitRegistry.EnemyRegistered += HandleEnemyRegistered;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || unitRegistry == null)
        {
            subscribed = false;
            return;
        }

        unitRegistry.PlayerChanged -= HandlePlayerChanged;
        unitRegistry.EnemyRegistered -= HandleEnemyRegistered;
        subscribed = false;
    }

    private static void AssignTarget(GameObject enemy, Transform playerTarget)
    {
        if (enemy == null || playerTarget == null)
        {
            return;
        }

        EnemyDetector detector = enemy.GetComponentInChildren<EnemyDetector>(true);
        detector?.SetPlayerTarget(playerTarget);
    }
}
