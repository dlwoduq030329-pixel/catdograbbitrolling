using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드의 돌진·순간이동·밀치기를 MapInfo 연결과 Unit 점유 규칙에 맞춰 처리한다.
/// TryCreate 계열은 상태를 바꾸지 않고 Preview와 실행이 공유할 계획을 만들고,
/// Apply 계열만 Transform·점유·상태이상·HP를 실제로 변경한다.
/// </summary>
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
        /// <summary>돌진 또는 순간이동이 끝났을 때 Unit이 위치할 타일.</summary>
        public MapInfo Destination { get; }

        /// <summary>
        /// 현재 계획은 도착 타일만 보관한다. ApplyMovement가 높이 차이를 유지하기 위해 시작 타일을 다시 검색한다.
        /// 이동 연출과 Registry 직접 참조를 정리할 때 StartTile도 계획에 포함하는 것이 기술부채 개선 방향이다.
        /// </summary>
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
        // 돌진은 실제 이동 가능한 경로가 존재해야 하며, 경로 길이가 maxDistance를 넘으면 실패한다.
        return TryCreateAdjacentLandingPlan(
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
        // 순간이동은 중간 경로를 걷지 않으므로 경로 검사를 생략하고 사실상 거리 제한을 두지 않는다.
        // 두 계획 모두 최종적으로 대상과 인접한 빈 타일 하나만 MovementPlan에 저장한다.
        return TryCreateAdjacentLandingPlan(
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

        // MovementPlan에 시작 타일이 없으므로 적용 순간의 Unit 위치에서 현재 타일을 다시 찾는다.
        // 타일 표면과 Unit 사이의 기존 Y 높이 차이를 보존해 모델이 땅속이나 공중으로 튀는 것을 막는다.
        MapInfo startTile = FindNearestMapTile(unit.transform.position);
        float heightOffset = startTile != null
            ? unit.transform.position.y - startTile.transform.position.y
            : 0f;
        unit.transform.position = plan.Destination.transform.position + Vector3.up * heightOffset;
        // Transform 이동과 MapRegistry 점유 갱신을 같은 함수에서 처리해 화면 위치와 논리 점유가 어긋나지 않게 한다.
        FindBattleMapRegistryInScene()?.SetOccupiedTile(unit, plan.Destination);
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

        // 밀리는 Enemy의 런타임 데이터가 있으면 원본 EnemyData의 pushWeight를 사용한다.
        // 데이터가 없는 소환물·임시 대상도 계산할 수 있도록 기본 무게는 1로 취급한다.
        BattleEnemyRuntimeData targetEnemyRuntimeData = target.GetComponent<BattleEnemyRuntimeData>();
        int targetWeight = targetEnemyRuntimeData != null && targetEnemyRuntimeData.Data != null
            ? Mathf.Max(1, targetEnemyRuntimeData.Data.pushWeight)
            : 1;
        int validatedPushForce = Mathf.Max(1, pushForce);

        MapInfo sourceTile = sourceTileOverride != null
            ? sourceTileOverride
            : FindNearestMapTile(source.transform.position);
        MapInfo targetCurrentTile = FindNearestMapTile(target.transform.position);
        if (sourceTile == null || targetCurrentTile == null)
        {
            return false;
        }

        // 미는 힘보다 대상 무게가 크면 위치를 바꾸지 않고 저항 결과만 Preview/실행에 전달한다.
        if (validatedPushForce < targetWeight)
        {
            plan = new PushPlan(
                source, target, PushResult.Resisted, targetCurrentTile, targetCurrentTile,
                null, 0, validatedPushForce, targetWeight);
            return true;
        }

        MapInfo targetStartTile = targetCurrentTile;
        int movedTiles = 0;
        for (int step = 0; step < distanceTiles; step++)
        {
            // 매 스텝마다 Source 반대 방향을 다시 계산해 불규칙한 타일 연결에서도 가장 가까운 진행 방향을 찾는다.
            Vector3 awayDirection =
                targetCurrentTile.transform.position - sourceTile.transform.position;
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                break;
            }

            awayDirection.Normalize();
            MapInfo nextPushTile = null;
            float bestDirectionScore = 0.5f;
            foreach (MapInfo neighbour in BattleTileRangeCalculator.GetNeighbours(targetCurrentTile))
            {
                if (neighbour == null)
                {
                    continue;
                }

                Vector3 candidateDirection =
                    neighbour.transform.position - targetCurrentTile.transform.position;
                candidateDirection.y = 0f;
                float score = candidateDirection.sqrMagnitude > Mathf.Epsilon
                    ? Vector3.Dot(awayDirection, candidateDirection.normalized)
                    : -1f;
                if (score > bestDirectionScore)
                {
                    bestDirectionScore = score;
                    nextPushTile = neighbour;
                }
            }

            // 밀기 방향과 Dot 0.5보다 가깝게 일치하는 이웃 타일이 없으면 벽에 막힌 것으로 처리한다.
            if (nextPushTile == null)
            {
                plan = new PushPlan(
                    source, target, PushResult.WallCollision, targetStartTile, targetCurrentTile,
                    null, movedTiles, validatedPushForce, targetWeight);
                return true;
            }

            // River는 일반 이동 가능 여부보다 먼저 판정하여 진입하지 않고 WaterDefeat 결과를 만든다.
            if (nextPushTile.Type == TileType.River)
            {
                plan = new PushPlan(
                    source, target, PushResult.WaterDefeat, targetStartTile, nextPushTile,
                    null, movedTiles, validatedPushForce, targetWeight);
                return true;
            }

            // 한 밀치기 스텝의 최종 후보 타일 하나가 정해진 뒤에만 다른 Enemy 점유를 검색한다.
            GameObject blockingEnemy = FindBlockingEnemyOnTile(nextPushTile, target);
            if (blockingEnemy != null)
            {
                plan = new PushPlan(
                    source, target, PushResult.EnemyCollision, targetStartTile, targetCurrentTile,
                    blockingEnemy, movedTiles, validatedPushForce, targetWeight);
                return true;
            }

            if (!BattleMapTraversalService.IsWalkable(nextPushTile))
            {
                plan = new PushPlan(
                    source, target, PushResult.WallCollision, targetStartTile, targetCurrentTile,
                    null, movedTiles, validatedPushForce, targetWeight);
                return true;
            }

            targetCurrentTile = nextPushTile;
            movedTiles++;
        }

        PushResult result = movedTiles > 0 ? PushResult.Moved : PushResult.None;
        plan = new PushPlan(
            source, target, result, targetStartTile, targetCurrentTile,
            null, movedTiles, validatedPushForce, targetWeight);
        return true;
    }

    /// <summary>
    /// 밀치기 계획을 생성한 뒤 즉시 적용하고 최종 결과와 실제 이동 칸 수를 반환한다.
    /// 계획 생성 자체가 실패하면 상태를 변경하지 않고 None/0을 반환한다.
    /// Preview가 필요한 호출자는 이 함수 대신 TryCreatePushPlan과 ApplyPushPlan을 분리해 사용한다.
    /// </summary>
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

    /// <summary>미리 계산한 밀치기 계획을 실제 위치·상태·HP에 한 번 적용한다.</summary>
    public static void ApplyPushPlan(PushPlan plan)
    {
        // Preview 이후 대상이 제거됐으면 더 이상 위치·상태·HP를 적용할 수 없다.
        if (plan == null || plan.Target == null)
        {
            return;
        }

        // 충돌 전까지 한 칸 이상 이동했다면 마지막 안전 타일까지 실제 위치를 옮긴다.
        // WaterDefeat는 물 타일 위로 Transform을 옮기지 않고 현재 자리에서 사망 처리만 수행한다.
        if (plan.MovedTiles > 0 &&
            plan.Result != PushResult.WaterDefeat &&
            plan.Destination != null)
        {
            // 시작 타일 기준 Y 오프셋을 유지하여 대상 모델의 발 높이가 바뀌지 않게 한다.
            float heightOffset = plan.StartTile != null
                ? plan.Target.transform.position.y - plan.StartTile.transform.position.y
                : 0f;
            plan.Target.transform.position =
                plan.Destination.transform.position + Vector3.up * heightOffset;
            // 실제 Transform과 논리 점유 타일을 같은 시점에 갱신한다.
            FindBattleMapRegistryInScene()?.SetOccupiedTile(plan.Target, plan.Destination);
        }

        // 계획 단계에서 확정한 결과에 따라 충돌 후속 효과만 적용한다.
        switch (plan.Result)
        {
            case PushResult.EnemyCollision:
                // 연쇄 밀치기는 발생하지 않으며 밀려가던 첫 대상만 기절한다.
                ApplyEnemyCollisionStun(plan.Target);
                break;

            case PushResult.WallCollision:
                ApplyWallCollisionControlEffects(plan.Target);
                break;

            case PushResult.WaterDefeat:
                // 현재 HP와 보호막 합계만큼 피해를 주어 BattleHealth의 기존 사망 이벤트를 통과시킨다.
                // 점유 해제와 사망 연출은 BattleHealth 사망 구독자가 처리해야 한다.
                BattleHealth health = plan.Target.GetComponent<BattleHealth>();
                if (health != null && !health.IsDead)
                {
                    health.TakeDamage(health.CurrentHealth + health.CurrentShield);
                }
                break;
        }

        // Preview UI와 충돌 피드백은 실제 적용이 끝난 계획을 이 이벤트로 전달받는다.
        PushApplied?.Invoke(plan);
    }

    /// <summary>
    /// 지정 타일을 점유한 다른 활성 Enemy를 Scene에서 찾아 반환한다.
    /// 밀치기의 매 이동 스텝마다 다음 목적지 타일이 하나 확정된 뒤 호출된다.
    /// 내부에서 모든 EnemyTurnActor를 검색하고 각 Enemy의 현재 타일까지 다시 계산하므로
    /// 비용은 대략 밀치기 칸 수 × 적 수 × 타일 탐색 비용으로 증가한다.
    /// 향후 BattleMapRegistry의 타일→Unit 직접 조회 API로 교체해야 한다.
    /// </summary>
    private static GameObject FindBlockingEnemyOnTile(
        MapInfo destinationTile,
        GameObject pushedEnemyToExclude)
    {
        EnemyTurnActor[] activeEnemyActors = UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(
            FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemyActor in activeEnemyActors)
        {
            // 현재 밀리고 있는 대상 자신과 비활성 Enemy는 충돌 대상으로 취급하지 않는다.
            if (enemyActor == null ||
                enemyActor.gameObject == pushedEnemyToExclude ||
                !enemyActor.gameObject.activeInHierarchy)
            {
                continue;
            }

            MapInfo enemyCurrentTile = FindNearestMapTile(enemyActor.transform.position);
            if (enemyCurrentTile == destinationTile)
            {
                return enemyActor.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// 다른 Enemy와 충돌한 밀치기 대상에게 1턴 기절을 적용한다.
    /// 2026-09-05: 기절·속박 전용 저장소였던 BattleEnemyControlState는 폐지됐다. Player 카드가 상태이상을
    /// 적용할 때(BattleCardActionController.ApplyStatusToUnit)와 동일하게 BattleStatusEffects.GetOrAdd로
    /// 공용 저장소를 확보해 Apply()로 적용하고, 상태 아이콘 View가 그 저장소를 구독하도록 BindStatusSource로
    /// 다시 연결한다 — Enemy Prefab 참조가 아직 완성되지 않아 두 컴포넌트를 런타임 GetOrAdd로 보완한다.
    /// 최종 Enemy Prefab에는 두 컴포넌트를 직접 부착하고 이 함수는 상태 API 호출만 남겨야 한다.
    /// </summary>
    private static void ApplyEnemyCollisionStun(GameObject pushedEnemy)
    {
        BattleStatusEffects statusEffects = BattleStatusEffects.GetOrAdd(pushedEnemy);
        BattleEnemyStatusView statusView =
            BattleComponentResolver.GetOrAdd<BattleEnemyStatusView>(pushedEnemy, null);
        statusEffects?.Apply(BattleStatusType.Stun, 1);
        statusView?.BindStatusSource(statusEffects);
    }

    /// <summary>
    /// 벽에 충돌한 밀치기 대상에게 1턴 기절과 1턴 이동 불가를 함께 적용한다.
    /// 상태 UI 생성까지 이동 서비스가 담당하는 현재 구조는 임시 호환 경로이며 Prefab 직접 참조로 이전한다.
    /// 2026-09-05: 위 ApplyEnemyCollisionStun과 같은 이유로 BattleStatusEffects.Apply() 두 번으로 통합했다.
    /// </summary>
    private static void ApplyWallCollisionControlEffects(GameObject pushedEnemy)
    {
        BattleStatusEffects statusEffects = BattleStatusEffects.GetOrAdd(pushedEnemy);
        BattleEnemyStatusView statusView =
            BattleComponentResolver.GetOrAdd<BattleEnemyStatusView>(pushedEnemy, null);
        statusEffects?.Apply(BattleStatusType.Stun, 1);
        statusEffects?.Apply(BattleStatusType.Root, 1);
        statusView?.BindStatusSource(statusEffects);
    }

    /// <summary>
    /// 대상과 인접한 타일 중 착지 가능한 빈 타일 하나를 골라 돌진 또는 순간이동 계획을 만든다.
    /// requireWalkablePath가 true인 돌진은 실제 경로와 최대 거리를 검사하고,
    /// false인 순간이동은 중간 경로를 무시한 채 후보의 타일 거리만 비교한다.
    /// 이 함수는 Transform과 점유 정보를 바꾸지 않는다.
    /// </summary>
    private static bool TryCreateAdjacentLandingPlan(
        GameObject player,
        GameObject target,
        int maximumPathDistance,
        bool requireWalkablePath,
        out MovementPlan plan,
        out string failureReason)
    {
        plan = null;
        failureReason = string.Empty;
        // Player와 대상의 월드 위치를 현재 전투 타일로 변환한다.
        MapInfo playerCurrentTile = player != null
            ? FindNearestMapTile(player.transform.position)
            : null;
        MapInfo targetCurrentTile = target != null
            ? FindNearestMapTile(target.transform.position)
            : null;
        if (playerCurrentTile == null || targetCurrentTile == null)
        {
            failureReason = "Player 또는 대상의 현재 타일을 찾지 못했습니다.";
            return false;
        }

        // 이동할 Player 자신을 제외한 Player/Enemy 점유 타일을 수집해 착지 후보에서 제외한다.
        HashSet<MapInfo> tilesOccupiedByOtherUnits =
            CollectTilesOccupiedByBattleUnits(player);
        MapInfo nearestValidLandingTile = null;
        int shortestDistanceToLandingTile = int.MaxValue;

        // 카드 이동은 대상 타일 자체가 아니라 대상 주변의 인접 빈 타일에 착지한다.
        foreach (MapInfo landingCandidate in BattleTileRangeCalculator.GetNeighbours(targetCurrentTile))
        {
            if (landingCandidate == null ||
                !BattleMapTraversalService.IsWalkable(landingCandidate) ||
                tilesOccupiedByOtherUnits.Contains(landingCandidate))
            {
                continue;
            }

            int distanceToCandidate;
            if (requireWalkablePath)
            {
                // 돌진은 현재 타일에서 후보까지 실제로 걸을 수 있는 경로가 있어야 한다.
                if (!BattleTileRangeCalculator.TryCalculatePath(
                        playerCurrentTile,
                        landingCandidate,
                        BattleMapTraversalService.IsWalkable,
                        tilesOccupiedByOtherUnits,
                        out List<MapInfo> path))
                {
                    continue;
                }

                distanceToCandidate = path.Count;
                if (distanceToCandidate > maximumPathDistance)
                {
                    continue;
                }
            }
            else
            {
                // 순간이동은 중간 타일의 통행 가능 여부를 검사하지 않고 후보 간 우선순위에만 맵 거리를 사용한다.
                distanceToCandidate = BattleTileRangeCalculator.GetDistance(
                    playerCurrentTile,
                    landingCandidate,
                    int.MaxValue);
                if (distanceToCandidate < 0)
                {
                    // 연결되지 않은 후보도 순간이동할 수 있으므로 제외하지 않고 정렬상 마지막으로 보낸다.
                    distanceToCandidate = int.MaxValue - 1;
                }
            }

            if (distanceToCandidate < shortestDistanceToLandingTile)
            {
                shortestDistanceToLandingTile = distanceToCandidate;
                nearestValidLandingTile = landingCandidate;
            }
        }

        if (nearestValidLandingTile == null)
        {
            failureReason = "대상과 인접한 착지 가능 빈 타일이 없습니다.";
            return false;
        }

        plan = new MovementPlan(nearestValidLandingTile);
        return true;
    }

    /// <summary>
    /// 이동할 Unit 자신을 제외하고 현재 Player와 활성 Enemy가 점유한 타일을 수집한다.
    /// 돌진·순간이동의 착지 충돌을 막는 용도이며, 현재는 Scene 전체 Enemy 검색과 Manager Player 참조를 혼합한다.
    /// 허수아비·용병·중립 Unit은 포함하지 못하므로 BattleMapRegistry의 공식 점유 목록으로 교체해야 한다.
    /// </summary>
    private static HashSet<MapInfo> CollectTilesOccupiedByBattleUnits(GameObject movingUnitToExclude)
    {
        HashSet<MapInfo> occupiedTiles = new HashSet<MapInfo>();
        EnemyTurnActor[] activeEnemyActors = UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(
            FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemyActor in activeEnemyActors)
        {
            if (enemyActor == null ||
                !enemyActor.gameObject.activeInHierarchy ||
                enemyActor.gameObject == movingUnitToExclude)
            {
                continue;
            }

            MapInfo enemyOccupiedTile = FindNearestMapTile(enemyActor.transform.position);
            if (enemyOccupiedTile != null)
            {
                occupiedTiles.Add(enemyOccupiedTile);
            }
        }

        BattleGameManager manager = BattleGameManager.Instance;
        if (manager != null &&
            manager.CurrentPlayer != null &&
            manager.CurrentPlayer != movingUnitToExclude)
        {
            MapInfo playerOccupiedTile =
                FindNearestMapTile(manager.CurrentPlayer.transform.position);
            if (playerOccupiedTile != null)
            {
                occupiedTiles.Add(playerOccupiedTile);
            }
        }

        return occupiedTiles;
    }

    /// <summary>
    /// 월드 위치에서 XZ 기준으로 가장 가까운 MapInfo를 반환한다.
    /// 우선 BattleMapRegistry의 캐시된 타일 목록을 사용하고, Registry가 없을 때만 Scene 전체 MapInfo를 검색한다.
    /// 현재 Registry 자체를 매 호출마다 다시 찾으므로 최종 구조에서는 이 서비스에 직접 전달해야 한다.
    /// </summary>
    private static MapInfo FindNearestMapTile(Vector3 worldPosition)
    {
        BattleMapRegistry mapRegistry = FindBattleMapRegistryInScene();
        if (mapRegistry != null && mapRegistry.Tiles.Count > 0)
        {
            return mapRegistry.FindClosestTile(worldPosition);
        }

        // 이전 Scene 호환용 폴백이다. 신규 전투 Scene에서는 Registry 직접 참조 누락을 숨기므로 삭제 대상이다.
        MapInfo[] tiles = UnityEngine.Object.FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
        return BattleTileLocator.FindClosestXZ(worldPosition, tiles);
    }

    /// <summary>
    /// 활성/비활성 오브젝트를 포함해 Scene의 BattleMapRegistry를 찾는다.
    /// 이름에 Find를 명시해 캐시나 직접 참조가 아니라 Scene 검색임을 숨기지 않는다.
    /// </summary>
    private static BattleMapRegistry FindBattleMapRegistryInScene()
    {
        return UnityEngine.Object.FindFirstObjectByType<BattleMapRegistry>(FindObjectsInactive.Include);
    }
}
