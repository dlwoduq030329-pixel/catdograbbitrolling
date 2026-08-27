using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 상태와 Enemy 자원을 읽어 행동 트리가 실행할 수 있는 완성된 Enemy 행동 계획을 만든다.
/// 실제 실행과 Preview가 서로 다른 공격·추격 공식을 갖지 않도록 판단 진입점을 한곳으로 통일한다.
/// 계산 결과는 EnemyTurnPlan으로 반환하며, 이동·MP 차감·피해 적용은 수행하지 않는다.
/// </summary>
public static class EnemyTurnPlanner
{
    /// <summary>
    /// 현재 타일에서 대상 타일까지 최단 경로를 계산하고 행동 트리로 Attack·Move·Wait를 결정한다.
    /// Move라면 현재 MP로 실제 이동할 칸 수와 그 결과 도착할 타일까지 함께 계산한다.
    /// 경로 자체를 만들 수 없으면 false를 반환하며, Wait는 정상적으로 생성된 계획이므로 true다.
    /// </summary>
    public static bool TryCreatePlan(
        EnemyTurnActor actor,
        BehaviorNode behaviorTree,
        Transform target,
        MapInfo startTile,
        MapInfo targetTile,
        ISet<MapInfo> blockedTiles,
        int attackRangeTiles,
        int currentMP,
        int moveMPCostPerTile,
        int basicAttackMPCost,
        out EnemyTurnPlan plan)
    {
        // out 값은 성공했을 때만 유효하다. false를 받은 호출자가 이전 계획을 실수로 재사용하지 않게 먼저 비운다.
        plan = null;
        // Planner가 계산을 시작하기 위해 반드시 필요한 경계 입력이다.
        // 누락된 참조를 임의로 Scene에서 검색하지 않고 호출자에게 실패를 돌려 책임 경계를 유지한다.
        if (actor == null || behaviorTree == null || target == null || startTile == null || targetTile == null)
        {
            return false;
        }

        // [1. 실제 거리·경로 계산]
        // 실행과 Preview 모두 같은 공용 BFS 결과를 사용한다. blockedTiles에는 다른 Enemy 점유 타일이 들어가며,
        // 반환 Path는 시작 타일을 제외하므로 Path.Count가 대상까지 필요한 실제 칸 수가 된다.
        if (!MapPathfinder.TryFindShortestPath(startTile, targetTile, blockedTiles, out List<MapInfo> path))
        {
            return false;
        }

        // [2. 판단 경계 생성]
        // Behavior Tree에는 Scene 검색을 맡기지 않고 이번 판단에 필요한 값만 EnemyAIContext로 전달한다.
        // Context는 현재 순간의 읽기 전용 스냅샷이고, Tree는 그 안의 Decision만 기록한다.
        EnemyAIContext decisionInput = new EnemyAIContext(
            actor.transform,
            target,
            startTile,
            targetTile,
            path,
            attackRangeTiles,
            currentMP,
            moveMPCostPerTile,
            basicAttackMPCost);
        // 공격 가능 여부를 먼저 검사하고, 불가능하면 이동, 둘 다 불가능하면 Wait를 선택한다.
        behaviorTree.Evaluate(decisionInput);

        // [3. 실제 이동 거리 계산]
        // Move 계획은 공격 사거리 직전까지만 이동한다. ActionExecutor와 동일한 공식을 사용해야
        // Preview의 예상 도착 타일과 실제 정지 위치가 일치한다.
        int plannedMoveTileCount = 0;
        MapInfo predictedDestination = startTile;
        if (decisionInput.Decision == EnemyAIDecision.Move)
        {
            // 현재 MP만으로 이동할 수 있는 최대 칸 수다. 비용은 최소 1로 보정해 0 나눗셈을 막는다.
            int affordableTileCount = currentMP / Mathf.Max(1, moveMPCostPerTile);
            // 대상 타일까지 완전히 들어가지 않고 공격 사거리 안에 도착하는 데 필요한 칸 수다.
            int tilesNeededBeforeAttackRange = Mathf.Max(0, path.Count - Mathf.Max(1, attackRangeTiles));
            // MP 한도와 필요한 거리 중 작은 값을 선택해야 과이동하거나 MP를 초과하지 않는다.
            plannedMoveTileCount = Mathf.Min(affordableTileCount, tilesNeededBeforeAttackRange);
            if (plannedMoveTileCount > 0)
            {
                // path에는 시작 타일이 없으므로 N칸 이동의 도착지는 N-1 인덱스다.
                predictedDestination = path[plannedMoveTileCount - 1];
            }
        }

        // [4. 읽기 전용 결과 조립]
        // 계산에 사용한 입력과 최종 판단·예상 이동 결과를 하나의 Plan으로 묶는다.
        // 이후 Actor는 실행에, ThreatPreview는 아이콘 표시에 같은 객체 구조를 사용한다.
        plan = new EnemyTurnPlan(
            actor,
            target,
            startTile,
            targetTile,
            path,
            decisionInput.Decision,
            Mathf.Max(1, attackRangeTiles),
            Mathf.Max(1, moveMPCostPerTile),
            Mathf.Max(0, basicAttackMPCost),
            plannedMoveTileCount,
            predictedDestination);
        return true;
    }
}
