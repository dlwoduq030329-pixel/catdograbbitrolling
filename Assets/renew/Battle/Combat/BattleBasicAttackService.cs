using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기본 공격을 위한 이동 위치와 경로를 계획하고 확정 직전 조건 및 MP 소비를 처리한다.
/// 입력, UI, 이동 애니메이션과 피해 적용은 담당하지 않는다.
/// </summary>
public static class BattleBasicAttackService
{
    /// <summary>Enemy 공격 사거리까지 필요한 최단 접근 경로와 이동·공격 MP 합계를 계산한다.</summary>
    public static bool TryCreatePlan(
        MapInfo playerTile,
        MapInfo enemyTile,
        IEnumerable<MapInfo> reachableTiles,
        ISet<MapInfo> occupiedTiles,
        Func<MapInfo, bool> isWalkable,
        int attackRange,
        int actionCost,
        BattleUnitMP playerMP,
        out List<MapInfo> movementPath,
        out int totalCost)
    {
        movementPath = null;
        totalCost = 0;
        if (playerTile == null || enemyTile == null || playerMP == null)
        {
            return false;
        }

        List<MapInfo> bestPath = null;
        int bestAttackDistance = int.MaxValue;
        HashSet<MapInfo> origins = new HashSet<MapInfo>(reachableTiles) { playerTile };

        foreach (MapInfo origin in origins)
        {
            if (origin != playerTile && occupiedTiles.Contains(origin))
            {
                continue;
            }

            int attackDistance = BattleTileRangeCalculator.GetDistance(origin, enemyTile, attackRange);
            if (attackDistance < 0 ||
                !BattleTileRangeCalculator.TryCalculatePath(
                    playerTile,
                    origin,
                    isWalkable,
                    occupiedTiles,
                    out List<MapInfo> candidatePath))
            {
                continue;
            }

            if (bestPath == null ||
                candidatePath.Count < bestPath.Count ||
                (candidatePath.Count == bestPath.Count && attackDistance < bestAttackDistance))
            {
                bestPath = candidatePath;
                bestAttackDistance = attackDistance;
            }
        }

        if (bestPath == null)
        {
            return false;
        }

        totalCost = bestPath.Count + Mathf.Max(0, actionCost);
        if (!playerMP.CanSpend(totalCost))
        {
            return false;
        }

        movementPath = bestPath;
        return true;
    }

    /// <summary>확정 시점의 MP와 대상 유효성을 다시 검사하여 오래된 공격 계획 실행을 방지한다.</summary>
    public static bool TryConfirm(
        BattleActionRequest pendingAction,
        GameObject player,
        EnemyTurnActor enemy,
        MapInfo playerTile,
        MapInfo enemyTile,
        IReadOnlyList<MapInfo> movementPath,
        int currentActionCost,
        out BattleActionResult result)
    {
        result = null;
        if (pendingAction == null || player == null || enemy == null ||
            !enemy.gameObject.activeInHierarchy || movementPath == null)
        {
            return false;
        }

        int attackDistance = BattleTileRangeCalculator.GetDistance(
            playerTile,
            enemyTile,
            pendingAction.RangeTiles);
        BattleUnitMP playerMP = player.GetComponent<BattleUnitMP>();
        int movementCost = movementPath.Count;
        int actionCost = Mathf.Max(0, currentActionCost);
        int totalCost = movementCost + actionCost;

        if (attackDistance < 0 || playerMP == null || !playerMP.TrySpend(totalCost))
        {
            return false;
        }

        BattleActionRequest confirmedRequest = new BattleActionRequest(
            pendingAction.DisplayName,
            pendingAction.ActionType,
            pendingAction.RangeTiles,
            actionCost,
            pendingAction.Power);
        result = new BattleActionResult(
            confirmedRequest,
            player,
            enemy.gameObject,
            new List<MapInfo>(movementPath),
            movementCost,
            actionCost);
        return true;
    }
}
