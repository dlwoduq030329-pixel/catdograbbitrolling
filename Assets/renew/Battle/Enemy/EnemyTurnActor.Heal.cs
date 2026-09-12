using System.Collections.Generic;
using UnityEngine;

public partial class EnemyTurnActor
{
    /// <summary>사거리 안에서 HP 비율이 가장 낮은 살아 있는 Enemy를 회복합니다.</summary>
    private bool TryHealLowestHealthEnemy()
    {
        BattleEnemyData data = runtimeData != null ? runtimeData.Data : null;
        BattleUnitRegistry units = battleDataPool != null ? battleDataPool.Units : null;
        if (data == null || data.allyHealAmount <= 0f || units == null || characterMP == null)
            return false;
        if (characterMP.CurrentMP < data.allyHealMPCost)
            return false;

        IReadOnlyList<MapInfo> mapTiles = mapContext.GetMapTiles(battleDataPool);
        MapInfo healerTile = MapPathfinder.FindClosestTile(transform.position, mapTiles);
        if (healerTile == null)
            return false;

        GameObject bestTarget = null;
        BattleHealth bestHealth = null;
        float lowestHealthRatio = float.MaxValue;

        // 사망 처리 대기 중인 Enemy가 원본 List에 남아 있어도 안전하도록 복사 후 생존 상태를 검사합니다.
        List<GameObject> candidates = new List<GameObject>(units.Enemies);
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || !candidate.activeInHierarchy)
                continue;

            BattleHealth health = candidate.GetComponent<BattleHealth>();
            if (health == null || health.IsDead || health.MaxHealth <= 0f || health.CurrentHealth >= health.MaxHealth)
                continue;

            float healthRatio = health.CurrentHealth / health.MaxHealth;
            if (healthRatio > data.allyHealBelowRatio)
                continue;

            MapInfo targetTile = MapPathfinder.FindClosestTile(candidate.transform.position, mapTiles);
            if (targetTile == null)
                continue;

            int distance = Mathf.Abs(targetTile.Index.x - healerTile.Index.x) +
                           Mathf.Abs(targetTile.Index.y - healerTile.Index.y);
            if (distance > data.allyHealRangeTiles || healthRatio >= lowestHealthRatio)
                continue;

            bestTarget = candidate;
            bestHealth = health;
            lowestHealthRatio = healthRatio;
        }

        // 탐색 뒤 대상이 죽는 경우를 한 번 더 막습니다.
        if (bestTarget == null || bestHealth == null || bestHealth.IsDead || !bestTarget.activeInHierarchy)
            return false;
        if (!characterMP.TrySpend(Mathf.Max(0, data.allyHealMPCost)))
            return false;

        BattleCharacterAnimationBridge.PlayAttack(gameObject);
        float healed = bestHealth.Heal(data.allyHealAmount);
        if (healed <= 0f)
            return false;

        healCooldownTurns = Mathf.CeilToInt(data.skillCooldown);
        Debug.Log($"{name}: {bestTarget.name} HP {healed:0.#} 회복", this);
        return true;
    }
}
