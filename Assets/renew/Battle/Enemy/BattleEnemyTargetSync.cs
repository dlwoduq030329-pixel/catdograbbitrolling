using UnityEngine;

/// <summary>
/// Player와 Enemy의 등록 순서와 관계없이 현재 Player Target을 EnemyDetector에 배포한다.
/// Unit 등록과 감지 판정은 담당하지 않는다.
///
/// Configure()와 OnEnable/OnDisable이 둘 다 Subscribe/Unsubscribe를 호출하는 이유(2026-08-21 정리):
/// BattleSceneInstaller가 이 컴포넌트를 gameObject.AddComponent로 붙이면 Unity가 그 자리에서 즉시
/// OnEnable()을 먼저 호출한다. 이 시점에는 아직 Configure(registry)가 실행되기 전이라 unitRegistry가
/// null이라 Subscribe()는 조용히 아무것도 하지 않는다. 실제 구독은 그 다음 줄에서 Installer가 명시적으로
/// 호출하는 Configure()가 담당한다. 즉 OnEnable의 Subscribe()는 "이미 Configure된 뒤 이 오브젝트가
/// 다시 비활성화됐다가 재활성화되는 경우"만을 위한 것이고, Configure 시점의 최초 구독은 OnEnable이 아니라
/// Configure 자신이 해야 한다 — 둘 중 하나만 남기면 최초 구독이 누락된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyTargetSync : MonoBehaviour
{
    private BattleUnitRegistry unitRegistry;
    private bool subscribed;

    /// <summary>
    /// BattleSceneInstaller가 컴포넌트를 준비한 직후 한 번 호출해 Unit Registry 이벤트를 연결하고,
    /// 이미 등록되어 있던 Player·Enemy에도 놓치지 않고 Target을 즉시 배포한다(SynchronizeRegisteredUnits).
    /// </summary>
    public void Configure(BattleUnitRegistry registry)
    {
        unitRegistry = registry;
        Subscribe();
        SynchronizeRegisteredUnits();
    }

    /// <summary>Configure로 이미 등록된 상태에서 이 오브젝트가 다시 활성화될 때만 실제로 구독을 되살린다.</summary>
    private void OnEnable()
    {
        Subscribe();
    }

    /// <summary>이 오브젝트가 비활성화되는 동안 Registry 이벤트를 받지 않도록 구독을 끊는다.</summary>
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

    /// <summary>Configure 시점에 Player가 이미 등록되어 있으면 그 Player를 현재 모든 Enemy에 즉시 배포한다.</summary>
    private void SynchronizeRegisteredUnits()
    {
        if (unitRegistry != null && unitRegistry.Player != null)
        {
            HandlePlayerChanged(unitRegistry.Player);
        }
    }

    /// <summary>PlayerChanged·EnemyRegistered 두 이벤트를 함께 구독한다. subscribed 플래그로 중복 구독을 막는다.</summary>
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

    /// <summary>Subscribe로 건 두 이벤트를 함께 해제한다. 이미 해제된 상태면 아무 것도 하지 않는다.</summary>
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

    /// <summary>해당 Enemy의 EnemyDetector를 찾아 실제로 Player Transform을 Target으로 심는다.</summary>
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
