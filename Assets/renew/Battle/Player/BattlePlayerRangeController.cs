using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 이동·공격 범위 집합을 생성하고 우선순위에 따라 타일 색상을 표시한다.
/// 입력 처리와 행동 확정은 담당하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerRangeController : MonoBehaviour
{
    private readonly HashSet<MapInfo> reachableTiles = new HashSet<MapInfo>();
    private readonly HashSet<MapInfo> attackableTiles = new HashSet<MapInfo>();
    private readonly HashSet<MapInfo> occupiedEnemyTiles = new HashSet<MapInfo>();
    private readonly Dictionary<MapInfo, int> reachableDistances = new Dictionary<MapInfo, int>();
    private readonly HashSet<MapInfo> enemyThreatTiles = new HashSet<MapInfo>();

    private BattleRangeVisualizer visualizer;

    public IEnumerable<MapInfo> ReachableTiles => reachableTiles;
    public ISet<MapInfo> OccupiedEnemyTiles => occupiedEnemyTiles;
    /// <summary>현재 표시 중인 이동 범위에 지정 타일이 포함되는지 확인한다.</summary>
    public bool IsReachable(MapInfo tile) => tile != null && reachableTiles.Contains(tile);

    /// <summary>계산 결과를 화면에 칠할 전용 시각화 모듈을 연결한다.</summary>
    public void Configure(BattleRangeVisualizer rangeVisualizer)
    {
        visualizer = rangeVisualizer;
    }

    /// <summary>현재 MP와 점유 정보를 기준으로 이동·공격 범위를 다시 계산하고 우선순위 색상으로 표시한다.</summary>
    public bool BuildAndShow(
        IEnumerable<MapInfo> mapTiles,
        MapInfo currentTile,
        int moveRange,
        int attackRange,
        Func<MapInfo, bool> isWalkable,
        IEnumerable<GameObject> enemyObjects,
        Color movableColor,
        Color blockedColor,
        Color attackableColor,
        Color enemyDetectColor)
    {
        ClearCalculatedRanges();
        if (currentTile == null || visualizer == null || isWalkable == null)
        {
            return false;
        }

        BattleTileRangeCalculator.BuildReachableTiles(
            currentTile,
            Mathf.Max(0, moveRange),
            isWalkable,
            occupiedEnemyTiles,
            reachableTiles,
            reachableDistances);
        BattleTileRangeCalculator.BuildAttackableTiles(
            currentTile,
            Mathf.Max(0, attackRange),
            reachableTiles,
            attackableTiles);

        List<EnemyDetector> detectors = CollectEnemyDetectors(enemyObjects);
        foreach (MapInfo tile in mapTiles)
        {
            if (tile == null)
            {
                continue;
            }

            // 원래 통행할 수 없는 지형에는 이동 Ray를 표시하지 않는다.
            if (!isWalkable(tile))
            {
                continue;
            }

            // Enemy가 점유한 통행 가능 타일만 차단 색으로 표시한다.
            if (occupiedEnemyTiles.Contains(tile))
            {
                visualizer.ShowRangeTile(tile, blockedColor);
            }
            else if (reachableTiles.Contains(tile))
            {
                visualizer.ShowRangeTile(tile, movableColor);
            }
            else if (attackableTiles.Contains(tile))
            {
                visualizer.ShowRangeTile(tile, attackableColor);
            }
            else if (IsInEnemyDetectRange(tile.transform.position, detectors))
            {
                visualizer.ShowRangeTile(tile, enemyDetectColor);
            }
        }

        return true;
    }

    /// <summary>
    /// R 단축키 전용 표시. Player 자신의 이동·공격 범위 대신, 활성 Enemy들이 이번 턴에
    /// 이동+공격으로 실제 위협할 수 있는 타일 전체를 BFS로 계산해 한 가지 색으로 표시한다.
    /// </summary>
    public bool BuildAndShowEnemyThreatRange(
        Func<MapInfo, bool> isWalkable,
        IEnumerable<GameObject> enemyObjects,
        Func<Vector3, MapInfo> findClosestTile,
        Color threatColor)
    {
        enemyThreatTiles.Clear();
        if (visualizer == null || isWalkable == null || findClosestTile == null || enemyObjects == null)
        {
            return false;
        }

        List<GameObject> activeEnemies = new List<GameObject>();
        foreach (GameObject enemyObject in enemyObjects)
        {
            if (enemyObject != null && enemyObject.activeInHierarchy)
            {
                activeEnemies.Add(enemyObject);
            }
        }

        if (activeEnemies.Count == 0)
        {
            return false;
        }

        // 다른 Enemy가 서 있는 타일은 이동 계산에서 막되, 계산 대상 본인의 타일은 막지 않는다.
        HashSet<MapInfo> allEnemyTiles = new HashSet<MapInfo>();
        foreach (GameObject enemyObject in activeEnemies)
        {
            MapInfo tile = findClosestTile(enemyObject.transform.position);
            // R 위협 범위는 최하단 정보다. 이동·공격 가능 타일의 색을 덮지 않는다.
            if (tile != null && !reachableTiles.Contains(tile) && !attackableTiles.Contains(tile))
            {
                allEnemyTiles.Add(tile);
            }
        }

        foreach (GameObject enemyObject in activeEnemies)
        {
            EnemyTurnActor actor = enemyObject.GetComponent<EnemyTurnActor>();
            BattleEnemyRuntimeData runtimeData = enemyObject.GetComponent<BattleEnemyRuntimeData>();
            if (actor == null || runtimeData == null || runtimeData.Data == null)
            {
                continue;
            }

            MapInfo enemyTile = findClosestTile(enemyObject.transform.position);
            if (enemyTile == null)
            {
                continue;
            }

            int moveCost = Mathf.Max(1, runtimeData.Data.moveMPCostPerTile);
            int moveRange = runtimeData.Data.maxTurnMP / moveCost;

            HashSet<MapInfo> occupiedForThisEnemy = new HashSet<MapInfo>(allEnemyTiles);
            occupiedForThisEnemy.Remove(enemyTile);

            HashSet<MapInfo> reachable = new HashSet<MapInfo>();
            Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
            BattleTileRangeCalculator.BuildReachableTiles(
                enemyTile,
                moveRange,
                isWalkable,
                occupiedForThisEnemy,
                reachable,
                distances);

            HashSet<MapInfo> attackable = new HashSet<MapInfo>();
            BattleTileRangeCalculator.BuildAttackableTiles(
                enemyTile,
                actor.AttackRangeTiles,
                reachable,
                attackable);

            enemyThreatTiles.Add(enemyTile);
            enemyThreatTiles.UnionWith(reachable);
            enemyThreatTiles.UnionWith(attackable);
        }

        foreach (MapInfo tile in enemyThreatTiles)
        {
            if (tile != null)
            {
                visualizer.ShowRangeTile(tile, threatColor);
            }
        }

        return enemyThreatTiles.Count > 0;
    }

    /// <summary>계산된 범위와 거리 캐시를 비우고 변경했던 모든 타일 색상을 복구한다.</summary>
    public void ClearState()
    {
        ClearCalculatedRanges();
        occupiedEnemyTiles.Clear();
        enemyThreatTiles.Clear();
    }

    /// <summary>새 범위 계산 전에 도달·공격 결과만 비우고 직전에 갱신한 점유 정보는 유지한다.</summary>
    private void ClearCalculatedRanges()
    {
        reachableTiles.Clear();
        attackableTiles.Clear();
        reachableDistances.Clear();
    }

    /// <summary>Registry를 우선 사용해 현재 활성 Enemy의 점유 타일 집합을 갱신한다.</summary>
    public void RefreshOccupiedEnemyTiles(
        BattleDataPool battleDataPool,
        Func<Vector3, MapInfo> findClosestTile)
    {
        occupiedEnemyTiles.Clear();

        if (battleDataPool != null &&
            battleDataPool.Units != null &&
            battleDataPool.Map != null &&
            battleDataPool.Units.Enemies.Count > 0)
        {
            battleDataPool.Map.FillOccupiedTiles(
                battleDataPool.Units.Enemies,
                occupiedEnemyTiles);
            return;
        }

        if (findClosestTile == null)
        {
            return;
        }

        EnemyTurnActor[] enemies = FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            MapInfo tile = findClosestTile(enemy.transform.position);
            if (tile != null)
            {
                occupiedEnemyTiles.Add(tile);
            }
        }
    }

    private static List<EnemyDetector> CollectEnemyDetectors(IEnumerable<GameObject> enemyObjects)
    {
        List<EnemyDetector> detectors = new List<EnemyDetector>();
        if (enemyObjects != null)
        {
            foreach (GameObject enemyObject in enemyObjects)
            {
                if (enemyObject == null || !enemyObject.activeInHierarchy)
                {
                    continue;
                }

                EnemyDetector detector = enemyObject.GetComponentInChildren<EnemyDetector>();
                if (detector != null)
                {
                    detectors.Add(detector);
                }
            }

            return detectors;
        }

        detectors.AddRange(FindObjectsByType<EnemyDetector>(FindObjectsSortMode.None));
        return detectors;
    }

    private static bool IsInEnemyDetectRange(Vector3 tilePosition, IEnumerable<EnemyDetector> detectors)
    {
        foreach (EnemyDetector detector in detectors)
        {
            if (detector != null &&
                Vector3.Distance(detector.transform.position, tilePosition) <= detector.DetectRange)
            {
                return true;
            }
        }

        return false;
    }
}
