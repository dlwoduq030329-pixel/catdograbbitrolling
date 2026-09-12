using System;
using UnityEngine;

/// <summary>풀에 최초 생성하는 Enemy의 전투 컴포넌트와 UI를 조립한다.</summary>
public static class EnemyRuntimeFactory
{
    public static void Configure(GameObject enemy, BattleEnemyData selectedData, Sprite typeIcon,
        BattleUnitRegistry units, BattleDataPool shared, Action<GameObject> release, Vector3 spawnOffset)
    {
        if (enemy == null || selectedData == null) return;

        EnemyDetector detector = enemy.GetComponentInChildren<EnemyDetector>(true);
        EnemyTurnActor turnActor = enemy.GetComponent<EnemyTurnActor>();
        EnemyAwareness awareness = enemy.GetComponent<EnemyAwareness>();
        BattleUnitMP enemyMP = enemy.GetComponent<BattleUnitMP>();
        BattleHealth enemyHealth = enemy.GetComponent<BattleHealth>();
        BattleEnemyDeathHandler deathHandler = enemy.GetComponent<BattleEnemyDeathHandler>();
        BattleEnemyStatusView statusView = enemy.GetComponent<BattleEnemyStatusView>();
        BattleStatusEffects statusEffects = enemy.GetComponent<BattleStatusEffects>();
        BattleEnemyRuntimeData runtimeData = enemy.GetComponent<BattleEnemyRuntimeData>();

        if (detector == null || turnActor == null || awareness == null || enemyMP == null ||
            enemyHealth == null || deathHandler == null || statusView == null ||
            statusEffects == null || runtimeData == null)
        {
            Debug.LogError($"[Enemy 설정] {enemy.name} 프리팹의 필수 컴포넌트가 없습니다.", enemy);
            return;
        }

        detector.ConfigureDetectRange(selectedData.detectRange);
        statusView.BindStatusSource(statusEffects);

        enemyHealth.Initialize(selectedData.maxHP);
        deathHandler.Configure(enemyHealth, units, () => release(enemy));
        deathHandler.SpawnOffset = spawnOffset;
        deathHandler.SpawnScale = enemy.transform.localScale;
        BattleHealthBarView enemyHealthBar =
            BattleHealthBarFactory.AttachEnemyBar(enemy, enemyHealth, enemyMP, typeIcon);

        runtimeData.Initialize(selectedData);
        turnActor.ConfigureFromData(selectedData);
        awareness.ConfigureAlertRange(selectedData.alertRange);

        enemyMP.ConfigureMaxMP(Mathf.Max(selectedData.minTurnMP, selectedData.maxTurnMP));

        turnActor.ResetForSpawn(shared);
    }
}
