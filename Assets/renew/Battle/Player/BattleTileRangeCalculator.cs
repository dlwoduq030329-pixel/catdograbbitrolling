using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapInfo의 상하좌우 연결을 기준으로 이동 범위, 공격 범위, 거리와 최단 경로를 계산한다.
/// 화면 표시와 캐릭터 이동은 처리하지 않는다.
/// </summary>
public static class BattleTileRangeCalculator
{
    /// <summary>
    /// 카드 사용자가 서 있는 타일을 기준으로, 상하좌우 최단거리가 카드 사거리 이하인 타일을 반환한다.
    /// 시작 타일은 일반적인 Enemy·Tile 대상 카드가 사용자 자신을 선택하지 않도록 결과에서 제외한다.
    /// 이 함수는 타일 계산만 담당하며 색상 표시와 카드 선택 상태는 변경하지 않는다.
    /// </summary>
    public static HashSet<MapInfo> FindCardTargetTiles(MapInfo userTile, int maximumCardRange)
    {
        // HashSet은 같은 타일이 여러 경로로 발견돼도 결과에 한 번만 포함되게 한다.
        HashSet<MapInfo> targetableTiles = new HashSet<MapInfo>();
        if (userTile == null)
        {
            return targetableTiles;
        }

        // FIFO Queue를 사용하면 사용자 타일에서 0칸, 1칸, 2칸 순서로 가까운 타일부터 탐색된다.
        Queue<MapInfo> tilesWaitingForSearch = new Queue<MapInfo>();
        Dictionary<MapInfo, int> distanceFromUserByTile = new Dictionary<MapInfo, int>();
        tilesWaitingForSearch.Enqueue(userTile);
        distanceFromUserByTile[userTile] = 0;

        // Queue에서 가까운 타일부터 꺼내는 BFS이므로 처음 기록된 값이 해당 타일의 최단 칸 거리다.
        while (tilesWaitingForSearch.Count > 0)
        {
            MapInfo currentTile = tilesWaitingForSearch.Dequeue();
            int distanceFromUser = distanceFromUserByTile[currentTile];
            // 거리 0은 카드 사용자가 서 있는 시작 타일이다. Self 카드는 별도 대상 규칙을 사용하므로 제외한다.
            if (distanceFromUser > 0)
            {
                targetableTiles.Add(currentTile);
            }

            // 현재 타일은 결과에 포함하되 최대 사거리에 도달했다면 그 바깥 이웃은 탐색하지 않는다.
            if (distanceFromUser >= maximumCardRange)
            {
                continue;
            }

            foreach (MapInfo neighbourTile in GetNeighbours(currentTile))
            {
                if (neighbourTile == null || distanceFromUserByTile.ContainsKey(neighbourTile))
                {
                    continue;
                }

                distanceFromUserByTile[neighbourTile] = distanceFromUser + 1;
                tilesWaitingForSearch.Enqueue(neighbourTile);
            }
        }

        return targetableTiles;
    }

    /// <summary>
    /// 선택한 중심 타일을 기준으로 카드의 실제 효과 모양에 포함되는 타일을 반환한다.
    /// Square는 등록된 전체 맵 타일을 좌표로 검사하고, Cross·Line·일반 범위는 중심에서 BFS로 탐색한다.
    /// CreateArea 같은 효과 종류는 판단하지 않으며, 카드 데이터가 결정한 범위 모양만 계산한다.
    /// </summary>
    public static HashSet<MapInfo> FindCardEffectAreaTiles(
        MapInfo effectCenterTile,
        MapInfo cardUserTile,
        BattleCardAreaType areaType,
        int areaSizeInTiles,
        bool useSquareArea,
        IEnumerable<MapInfo> allMapTiles = null)
    {
        HashSet<MapInfo> effectAreaTiles = new HashSet<MapInfo>();
        if (effectCenterTile == null || areaType == BattleCardAreaType.Single)
        {
            return effectAreaTiles;
        }

        // 잘못된 0 이하 데이터가 들어와도 최소 한 칸짜리 효과 Preview는 유지한다.
        int maximumEffectDistance = Mathf.Max(1, areaSizeInTiles);
        // Square는 상하좌우 이동 거리보다 X/Y 좌표 차이로 판정해야 모서리 타일까지 포함된다.
        // 그래서 연결 타일 BFS 대신 등록된 전체 MapInfo를 한 번 순회한다.
        if (useSquareArea && allMapTiles != null)
        {
            foreach (MapInfo mapTile in allMapTiles)
            {
                if (mapTile == null) continue;
                Vector2Int offsetFromCenter = mapTile.Index - effectCenterTile.Index;
                if (Mathf.Abs(offsetFromCenter.x) <= maximumEffectDistance &&
                    Mathf.Abs(offsetFromCenter.y) <= maximumEffectDistance)
                {
                    effectAreaTiles.Add(mapTile);
                }
            }

            return effectAreaTiles;
        }

        // Cross·Line·일반 범위는 중심에서 연결된 타일만 따라가도록 BFS 후보를 만든다.
        Queue<MapInfo> tilesWaitingForSearch = new Queue<MapInfo>();
        Dictionary<MapInfo, int> distanceFromCenterByTile = new Dictionary<MapInfo, int>();
        tilesWaitingForSearch.Enqueue(effectCenterTile);
        distanceFromCenterByTile[effectCenterTile] = 0;

        while (tilesWaitingForSearch.Count > 0)
        {
            MapInfo candidateTile = tilesWaitingForSearch.Dequeue();
            int distanceFromCenter = distanceFromCenterByTile[candidateTile];
            // BFS로 찾은 거리 후보 중 카드의 AreaType 모양에 실제로 속하는 타일만 결과에 넣는다.
            if (IsTileInsideCardEffectShape(
                    candidateTile,
                    effectCenterTile,
                    cardUserTile,
                    areaType,
                    distanceFromCenter,
                    maximumEffectDistance))
            {
                effectAreaTiles.Add(candidateTile);
            }

            if (distanceFromCenter >= maximumEffectDistance) continue;
            foreach (MapInfo neighbourTile in GetNeighbours(candidateTile))
            {
                if (neighbourTile == null || distanceFromCenterByTile.ContainsKey(neighbourTile)) continue;
                distanceFromCenterByTile[neighbourTile] = distanceFromCenter + 1;
                tilesWaitingForSearch.Enqueue(neighbourTile);
            }
        }

        return effectAreaTiles;
    }

    /// <summary>
    /// BFS가 찾은 후보 타일이 Cross·Line·일반 범위 중 현재 카드 모양에 포함되는지 판정한다.
    /// Line은 카드 사용자에서 선택 중심으로 향하는 방향 중 변화량이 큰 축을 직선 방향으로 사용한다.
    /// </summary>
    private static bool IsTileInsideCardEffectShape(
        MapInfo candidateTile,
        MapInfo effectCenterTile,
        MapInfo cardUserTile,
        BattleCardAreaType areaType,
        int distanceFromCenter,
        int maximumEffectDistance)
    {
        if (candidateTile == null || distanceFromCenter > maximumEffectDistance) return false;

        switch (areaType)
        {
            case BattleCardAreaType.Cross:
                // 중심과 같은 행 또는 같은 열인 타일만 남겨 십자 모양을 만든다.
                Vector2Int crossOffset = candidateTile.Index - effectCenterTile.Index;
                return crossOffset.x == 0 || crossOffset.y == 0;
            case BattleCardAreaType.Line:
                if (cardUserTile == null) return false;
                // 사용자→선택 중심의 변화가 큰 축을 공격 방향으로 선택한다.
                // X 변화가 크면 가로선, Y 변화가 크면 세로선만 결과에 포함한다.
                Vector2Int directionToCenter = effectCenterTile.Index - cardUserTile.Index;
                Vector2Int lineOffset = candidateTile.Index - effectCenterTile.Index;
                return Mathf.Abs(directionToCenter.x) >= Mathf.Abs(directionToCenter.y)
                    ? lineOffset.y == 0
                    : lineOffset.x == 0;
            default:
                return true;
        }
    }

    /// <summary>
    /// 시작 타일에서 이동 비용 안에 도달 가능한 타일과 각 타일까지의 최단거리를 BFS로 계산한다.
    /// 벽과 점유 타일에서는 탐색을 중단하며, 결과는 호출자가 전달한 두 Collection에 누적한다.
    /// 화면 색상이나 유닛 위치는 바꾸지 않는다.
    /// </summary>
    public static void BuildReachableTiles(
        MapInfo startTile,
        int maxDistance,
        Func<MapInfo, bool> isWalkable,
        ISet<MapInfo> occupiedTiles,
        ISet<MapInfo> reachableTiles,
        IDictionary<MapInfo, int> reachableDistances)
    {
        if (startTile == null)
        {
            return;
        }

        // startTile은 거리 계산 기준일 뿐 이동 후보에는 넣지 않고 거리 Dictionary에만 0으로 기록한다.
        Queue<MapInfo> queue = new Queue<MapInfo>();
        queue.Enqueue(startTile);
        reachableDistances[startTile] = 0;

        while (queue.Count > 0)
        {
            MapInfo current = queue.Dequeue();
            int distance = reachableDistances[current];
            if (distance >= maxDistance)
            {
                continue;
            }

            foreach (MapInfo neighbour in GetNeighbours(current))
            {
                if (neighbour == null ||
                    reachableDistances.ContainsKey(neighbour) ||
                    !isWalkable(neighbour) ||
                    occupiedTiles.Contains(neighbour))
                {
                    continue;
                }

                int nextDistance = distance + 1;
                reachableDistances[neighbour] = nextDistance;
                reachableTiles.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }
    }

    /// <summary>이동 가능 지점들의 가장자리에서 공격 사거리 안에 포함되는 타일을 수집한다.
    /// isWalkable/occupiedTiles를 넘기면 벽이나 다른 유닛이 막고 있는 타일 너머로는 사거리가
    /// 확장되지 않는다(둘 다 생략하면 기존과 동일하게 장애물을 무시하고 순수 칸수로만 계산한다).</summary>
    public static void BuildAttackableTiles(
        MapInfo currentTile,
        int attackRange,
        IEnumerable<MapInfo> reachableTiles,
        ISet<MapInfo> attackableTiles,
        Func<MapInfo, bool> isWalkable = null,
        ISet<MapInfo> occupiedTiles = null)
    {
        HashSet<MapInfo> origins = new HashSet<MapInfo>(reachableTiles) { currentTile };
        foreach (MapInfo origin in origins)
        {
            Queue<MapInfo> queue = new Queue<MapInfo>();
            Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
            queue.Enqueue(origin);
            distances[origin] = 0;

            while (queue.Count > 0)
            {
                MapInfo current = queue.Dequeue();
                int distance = distances[current];
                if (distance > 0)
                {
                    attackableTiles.Add(current);
                }

                if (distance >= attackRange)
                {
                    continue;
                }

                foreach (MapInfo neighbour in GetNeighbours(current))
                {
                    if (neighbour == null || distances.ContainsKey(neighbour))
                    {
                        continue;
                    }

                    // 공격 사거리도 이동 범위와 마찬가지로 벽/점유 타일 너머로는 뻗어나가지 않는다.
                    if (isWalkable != null && !isWalkable(neighbour))
                    {
                        continue;
                    }
                    if (occupiedTiles != null && occupiedTiles.Contains(neighbour))
                    {
                        continue;
                    }

                    distances[neighbour] = distance + 1;
                    queue.Enqueue(neighbour);
                }
            }
        }

        attackableTiles.ExceptWith(reachableTiles);
        attackableTiles.Remove(currentTile);
    }

    /// <summary>상하좌우 연결 기준 최단 칸 거리를 반환하며 제한 거리 안에 없으면 음수를 반환한다.
    /// isWalkable/occupiedTiles를 넘기면 벽이나 다른 유닛이 가로막아 실제로 돌아갈 수 없는 경로는
    /// 최단 거리 계산에서 제외한다(둘 다 생략하면 기존과 동일하게 장애물을 무시한 순수 칸수 거리).
    /// 단, targetTile 자신은 점유 여부와 무관하게 항상 도달 지점으로 인정한다 — 공격 대상 Enemy가
    /// 서 있는 바로 그 타일이 "점유된 타일"이라는 이유로 거리 계산에서 제외되면 안 되기 때문이다.</summary>
    public static int GetDistance(
        MapInfo startTile,
        MapInfo targetTile,
        int maxDistance,
        Func<MapInfo, bool> isWalkable = null,
        ISet<MapInfo> occupiedTiles = null)
    {
        if (startTile == null || targetTile == null)
        {
            return -1;
        }

        if (startTile == targetTile)
        {
            return 0;
        }

        Queue<MapInfo> queue = new Queue<MapInfo>();
        Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
        queue.Enqueue(startTile);
        distances[startTile] = 0;

        while (queue.Count > 0)
        {
            MapInfo current = queue.Dequeue();
            int distance = distances[current];
            if (distance >= maxDistance)
            {
                continue;
            }

            foreach (MapInfo neighbour in GetNeighbours(current))
            {
                if (neighbour == null || distances.ContainsKey(neighbour))
                {
                    continue;
                }

                int nextDistance = distance + 1;
                if (neighbour == targetTile)
                {
                    return nextDistance;
                }

                if (isWalkable != null && !isWalkable(neighbour))
                {
                    continue;
                }
                if (occupiedTiles != null && occupiedTiles.Contains(neighbour))
                {
                    continue;
                }

                distances[neighbour] = nextDistance;
                queue.Enqueue(neighbour);
            }
        }

        return -1;
    }

    /// <summary>차단 타일을 제외한 최단 경로를 계산한다. 성공한 경로에는 시작과 목적 타일이 포함된다.</summary>
    public static bool TryCalculatePath(
        MapInfo startTile,
        MapInfo targetTile,
        Func<MapInfo, bool> isWalkable,
        ISet<MapInfo> occupiedTiles,
        out List<MapInfo> path)
    {
        path = new List<MapInfo>();
        if (startTile == null || targetTile == null)
        {
            return false;
        }

        if (startTile == targetTile)
        {
            return true;
        }

        Queue<MapInfo> queue = new Queue<MapInfo>();
        Dictionary<MapInfo, MapInfo> previousTiles = new Dictionary<MapInfo, MapInfo>();
        queue.Enqueue(startTile);
        previousTiles[startTile] = null;

        while (queue.Count > 0)
        {
            MapInfo current = queue.Dequeue();
            foreach (MapInfo neighbour in GetNeighbours(current))
            {
                if (neighbour == null ||
                    previousTiles.ContainsKey(neighbour) ||
                    !isWalkable(neighbour) ||
                    occupiedTiles.Contains(neighbour))
                {
                    continue;
                }

                previousTiles[neighbour] = current;
                if (neighbour == targetTile)
                {
                    MapInfo pathTile = targetTile;
                    while (pathTile != startTile)
                    {
                        path.Add(pathTile);
                        pathTile = previousTiles[pathTile];
                    }

                    path.Reverse();
                    return true;
                }

                queue.Enqueue(neighbour);
            }
        }

        return false;
    }

    /// <summary>
    /// MapInfo가 보관한 상·하·좌·우 연결을 순서대로 반환한다.
    /// null 연결도 그대로 반환하므로 호출자는 맵 가장자리의 null을 건너뛰어야 한다.
    /// 대각선은 전투 거리와 경로 계산 규칙에 포함하지 않는다.
    /// </summary>
    public static IEnumerable<MapInfo> GetNeighbours(MapInfo tile)
    {
        yield return tile.Up;
        yield return tile.Down;
        yield return tile.Left;
        yield return tile.Right;
    }
}
