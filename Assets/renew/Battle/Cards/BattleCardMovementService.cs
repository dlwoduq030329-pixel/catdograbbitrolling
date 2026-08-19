using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>카드의 돌진·순간이동·밀치기를 MapInfo 연결과 Unit 점유 규칙에 맞춰 처리한다.</summary>
public static class BattleCardMovementService
{
    public static event Action<PushPlan> PushApplied;
    public enum PushResult
    {
        None,
        Moved,
        Resisted,
        EnemyCollision,
        WallCollision,
        WaterDefeat
    }

    public sealed class MovementPlan
    {
        public MapInfo Destination { get; }

        public MovementPlan(MapInfo destination)
        {
            Destination = destination;
        }
    }

    /// <summary>상태를 변경하지 않고 계산한 밀치기 결과. UI 예고와 실제 실행이 같은 판정을 공유한다.</summary>
    public sealed class PushPlan
    {
        public GameObject Source { get; }
        public GameObject Target { get; }
        public PushResult Result { get; }
        public MapInfo StartTile { get; }
        public MapInfo Destination { get; }
        public GameObject BlockingEnemy { get; }
        public int MovedTiles { get; }
        public int PushForce { get; }
        public int TargetWeight { get; }

        public PushPlan(
            GameObject source,
            GameObject target,
            PushResult result,
            MapInfo startTile,
            MapInfo destination,
            GameObject blockingEnemy,
            int movedTiles,
            int pushForce,
            int targetWeight)
        {
            Source = source;
            Target = target;
            Result = result;
            StartTile = startTile;
            Destination = destination;
            BlockingEnemy = blockingEnemy;
            MovedTiles = movedTiles;
            PushForce = pushForce;
            TargetWeight = targetWeight;
        }
    }

    public static bool TryCreateDashPlan(
        GameObject player,
        GameObject target,
        int maxDistance,
        out MovementPlan plan,
        out string failureReason)
    {
        return TryCreateApproachPlan(
            player,
            target,
            Mathf.Max(0, maxDistance),
            true,
            out plan,
            out failureReason);
    }

    public static bool TryCreateTeleportPlan(
        GameObject player,
        GameObject target,
        out MovementPlan plan,
        out string failureReason)
    {
        return TryCreateApproachPlan(
            player,
            target,
            int.MaxValue,
            false,
            out plan,
            out failureReason);
    }

    public static void ApplyMovement(GameObject unit, MovementPlan plan)
    {
        if (unit == null || plan == null || plan.Destination == null)
        {
            return;
        }

        MapInfo startTile = FindClosestTile(unit.transform.position);
        float heightOffset = startTile != null
            ? unit.transform.position.y - startTile.transform.position.y
            : 0f;
        unit.transform.position = plan.Destination.transform.position + Vector3.up * heightOffset;
        ResolveMapRegistry()?.SetOccupiedTile(unit, plan.Destination);
    }

    public static PushResult TryPush(
        GameObject source,
        GameObject target,
        int distanceTiles,
        int pushForce,
        out int movedTiles)
    {
        if (!TryCreatePushPlan(source, target, distanceTiles, pushForce, out PushPlan plan))
        {
            movedTiles = 0;
            return PushResult.None;
        }

        ApplyPushPlan(plan);
        movedTiles = plan.MovedTiles;
        return plan.Result;
    }

    /// <summary>밀기 방향·무게·지형·점유 결과를 계산하지만 Transform, HP와 상태는 변경하지 않는다.</summary>
    public static bool TryCreatePushPlan(
        GameObject source,
        GameObject target,
        int distanceTiles,
        int pushForce,
        out PushPlan plan)
    {
        return TryCreatePushPlan(
            source,
            target,
            null,
            distanceTiles,
            pushForce,
            out plan);
    }

    /// <summary>돌진 후 위치처럼 아직 적용되지 않은 밀기 시작 타일을 사용해 결과를 미리 계산한다.</summary>
    public static bool TryCreatePushPlan(
        GameObject source,
        GameObject target,
        MapInfo sourceTileOverride,
        int distanceTiles,
        int pushForce,
        out PushPlan plan)
    {
        plan = null;
        if (source == null || target == null || distanceTiles <= 0)
        {
            return false;
        }

        BattleEnemyRuntimeData runtimeData = target.GetComponent<BattleEnemyRuntimeData>();
        int targetWeight = runtimeData != null && runtimeData.Data != null
            ? Mathf.Max(1, runtimeData.Data.pushWeight)
            : 1;
        int safePushForce = Mathf.Max(1, pushForce);

        MapInfo sourceTile = sourceTileOverride != null
            ? sourceTileOverride
            : FindClosestTile(source.transform.position);
        MapInfo currentTile = FindClosestTile(target.transform.position);
        if (sourceTile == null || currentTile == null)
        {
            return false;
        }

        if (safePushForce < targetWeight)
        {
            plan = new PushPlan(
                source, target, PushResult.Resisted, currentTile, currentTile,
                null, 0, safePushForce, targetWeight);
            return true;
        }

        MapInfo startTile = currentTile;
        int movedTiles = 0;
        for (int step = 0; step < distanceTiles; step++)
        {
            Vector3 awayDirection = (currentTile.transform.position - sourceTile.transform.position);
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                break;
            }

            awayDirection.Normalize();
            MapInfo destination = null;
            float bestDirectionScore = 0.5f;
            foreach (MapInfo neighbour in BattleTileRangeCalculator.GetNeighbours(currentTile))
            {
                if (neighbour == null)
                {
                    continue;
                }

                Vector3 candidateDirection = neighbour.transform.position - currentTile.transform.position;
                candidateDirection.y = 0f;
                float score = candidateDirection.sqrMagnitude > Mathf.Epsilon
                    ? Vector3.Dot(awayDirection, candidateDirection.normalized)
                    : -1f;
                if (score > bestDirectionScore)
                {
                    bestDirectionScore = score;
                    destination = neighbour;
                }
            }

            if (destination == null)
            {
                plan = new PushPlan(
                    source, target, PushResult.WallCollision, startTile, currentTile,
                    null, movedTiles, safePushForce, targetWeight);
                return true;
            }

            if (destination.Type == TileType.River)
            {
                plan = new PushPlan(
                    source, target, PushResult.WaterDefeat, startTile, destination,
                    null, movedTiles, safePushForce, targetWeight);
                return true;
            }

            GameObject blockingEnemy = FindEnemyOnTile(destination, target);
            if (blockingEnemy != null)
            {
                plan = new PushPlan(
                    source, target, PushResult.EnemyCollision, startTile, currentTile,
                    blockingEnemy, movedTiles, safePushForce, targetWeight);
                return true;
            }

            if (!BattleMapTraversalService.IsWalkable(destination))
            {
                plan = new PushPlan(
                    source, target, PushResult.WallCollision, startTile, currentTile,
                    null, movedTiles, safePushForce, targetWeight);
                return true;
            }

            currentTile = destination;
            movedTiles++;
        }

        PushResult result = movedTiles > 0 ? PushResult.Moved : PushResult.None;
        plan = new PushPlan(
            source, target, result, startTile, currentTile,
            null, movedTiles, safePushForce, targetWeight);
        return true;
    }

    /// <summary>미리 계산한 밀치기 계획을 실제 위치·상태·HP에 한 번 적용한다.</summary>
    public static void ApplyPushPlan(PushPlan plan)
    {
        if (plan == null || plan.Target == null)
        {
            return;
        }

        if (plan.MovedTiles > 0 && plan.Result != PushResult.WaterDefeat && plan.Destination != null)
        {
            float heightOffset = plan.StartTile != null
                ? plan.Target.transform.position.y - plan.StartTile.transform.position.y
                : 0f;
            plan.Target.transform.position =
                plan.Destination.transform.position + Vector3.up * heightOffset;
            ResolveMapRegistry()?.SetOccupiedTile(plan.Target, plan.Destination);
        }

        switch (plan.Result)
        {
            case PushResult.EnemyCollision:
                // 연쇄 밀치기는 발생하지 않으며 밀려가던 첫 대상만 기절한다.
                ApplyStun(plan.Target);
                break;

            case PushResult.WallCollision:
                ApplyWallCollision(plan.Target);
                break;

            case PushResult.WaterDefeat:
                BattleHealth health = plan.Target.GetComponent<BattleHealth>();
                if (health != null && !health.IsDead)
                {
                    health.TakeDamage(health.CurrentHealth + health.CurrentShield);
                }
                break;
        }

        PushApplied?.Invoke(plan);
    }

    private static GameObject FindEnemyOnTile(MapInfo tile, GameObject excludedEnemy)
    {
        EnemyTurnActor[] enemies = UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(
            FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            if (enemy == null || enemy.gameObject == excludedEnemy || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (FindClosestTile(enemy.transform.position) == tile)
            {
                return enemy.gameObject;
            }
        }

        return null;
    }

    private static void ApplyStun(GameObject enemy)
    {
        BattleEnemyControlState state = BattleComponentResolver.GetOrAdd<BattleEnemyControlState>(enemy, null);
        BattleComponentResolver.GetOrAdd<BattleEnemyStatusView>(enemy, null);
        state?.ApplyStun(1);
    }

    private static void ApplyWallCollision(GameObject enemy)
    {
        BattleEnemyControlState state = BattleComponentResolver.GetOrAdd<BattleEnemyControlState>(enemy, null);
        BattleComponentResolver.GetOrAdd<BattleEnemyStatusView>(enemy, null);
        state?.ApplyStun(1);
        state?.ApplyRoot(1);
    }

    private static bool TryCreateApproachPlan(
        GameObject player,
        GameObject target,
        int maxPathDistance,
        bool requirePath,
        out MovementPlan plan,
        out string failureReason)
    {
        plan = null;
        failureReason = string.Empty;
        MapInfo playerTile = player != null ? FindClosestTile(player.transform.position) : null;
        MapInfo targetTile = target != null ? FindClosestTile(target.transform.position) : null;
        if (playerTile == null || targetTile == null)
        {
            failureReason = "Player 또는 대상의 현재 타일을 찾지 못했습니다.";
            return false;
        }

        HashSet<MapInfo> occupied = CollectOccupiedTiles(player);
        MapInfo bestDestination = null;
        int bestDistance = int.MaxValue;
        foreach (MapInfo candidate in BattleTileRangeCalculator.GetNeighbours(targetTile))
        {
            if (candidate == null || !BattleMapTraversalService.IsWalkable(candidate) || occupied.Contains(candidate))
            {
                continue;
            }

            int distance;
            if (requirePath)
            {
                if (!BattleTileRangeCalculator.TryCalculatePath(
                        playerTile,
                        candidate,
                        BattleMapTraversalService.IsWalkable,
                        occupied,
                        out List<MapInfo> path))
                {
                    continue;
                }

                distance = path.Count;
                if (distance > maxPathDistance)
                {
                    continue;
                }
            }
            else
            {
                distance = BattleTileRangeCalculator.GetDistance(playerTile, candidate, int.MaxValue);
                if (distance < 0)
                {
                    distance = int.MaxValue - 1;
                }
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestDestination = candidate;
            }
        }

        if (bestDestination == null)
        {
            failureReason = "대상과 인접한 착지 가능 빈 타일이 없습니다.";
            return false;
        }

        plan = new MovementPlan(bestDestination);
        return true;
    }

    private static HashSet<MapInfo> CollectOccupiedTiles(GameObject excludedUnit)
    {
        HashSet<MapInfo> occupied = new HashSet<MapInfo>();
        EnemyTurnActor[] enemies = UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(
            FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.gameObject == excludedUnit)
            {
                continue;
            }

            MapInfo tile = FindClosestTile(enemy.transform.position);
            if (tile != null)
            {
                occupied.Add(tile);
            }
        }

        BattleGameManager manager = BattleGameManager.Instance;
        if (manager != null && manager.CurrentPlayer != null && manager.CurrentPlayer != excludedUnit)
        {
            MapInfo playerTile = FindClosestTile(manager.CurrentPlayer.transform.position);
            if (playerTile != null)
            {
                occupied.Add(playerTile);
            }
        }

        return occupied;
    }

    private static MapInfo FindClosestTile(Vector3 position)
    {
        BattleMapRegistry registry = ResolveMapRegistry();
        if (registry != null && registry.Tiles.Count > 0)
        {
            return registry.FindClosestTile(position);
        }

        MapInfo[] tiles = UnityEngine.Object.FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
        return BattleTileLocator.FindClosestXZ(position, tiles);
    }

    private static BattleMapRegistry ResolveMapRegistry()
    {
        return UnityEngine.Object.FindFirstObjectByType<BattleMapRegistry>(FindObjectsInactive.Include);
    }
}
