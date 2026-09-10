using System;
using UnityEngine;

/// <summary>풀에 최초 생성하는 Enemy의 전투 컴포넌트와 UI를 조립한다.</summary>
public static class EnemyRuntimeFactory
{
    public static void Configure(GameObject enemy, BattleEnemyData selectedData, Sprite typeIcon,
        BattleUnitRegistry units, BattleDataPool shared, Action<GameObject> release, Vector3 spawnOffset)
    {
        EnemyDetector detector = enemy.GetComponentInChildren<EnemyDetector>();
        if (detector == null)
        {
            detector = enemy.AddComponent<EnemyDetector>();
        }
        detector.ConfigureDetectRange(selectedData.detectRange);

        EnemyTurnActor turnActor = BattleComponentResolver.GetOrAdd<EnemyTurnActor>(enemy, null);
        BattleComponentResolver.GetOrAdd<BattleEnemyActionExecutor>(enemy, null);

        EnemyAwareness awareness = BattleComponentResolver.GetOrAdd<EnemyAwareness>(enemy, null);

        BattleComponentResolver.GetOrAdd<PathDebugView>(enemy, null);

        BattleUnitMP enemyMP = BattleComponentResolver.GetOrAdd<BattleUnitMP>(enemy, null);
        BattleHealth enemyHealth = BattleComponentResolver.GetOrAdd<BattleHealth>(enemy, null);
        BattleEnemyDeathHandler deathHandler =
            BattleComponentResolver.GetOrAdd<BattleEnemyDeathHandler>(enemy, null);
        BattleComponentResolver.GetOrAdd<BattleEnemyStatusView>(enemy, null);

        enemyHealth.Initialize(selectedData.maxHP);
        deathHandler.Configure(enemyHealth, units, () => release(enemy));
        deathHandler.SpawnOffset = spawnOffset;
        deathHandler.SpawnScale = enemy.transform.localScale;
        BattleHealthBarView enemyHealthBar =
            BattleHealthBarFactory.AttachEnemyBar(enemy, enemyHealth, enemyMP, typeIcon);

        BattleEnemyRuntimeData runtimeData =
            BattleComponentResolver.GetOrAdd<BattleEnemyRuntimeData>(enemy, null);

        runtimeData.Initialize(selectedData);
        turnActor.ConfigureFromData(selectedData);
        awareness.ConfigureAlertRange(selectedData.alertRange);

        enemyMP.ConfigureMaxMP(Mathf.Max(selectedData.minTurnMP, selectedData.maxTurnMP));

        turnActor.ResetForSpawn(shared);
    }
}
