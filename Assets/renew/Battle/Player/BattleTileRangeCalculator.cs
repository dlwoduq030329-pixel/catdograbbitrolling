using System;
using System.Collections.Generic;

/// <summary>
/// MapInfo의 상하좌우 연결을 기준으로 이동 범위, 공격 범위, 거리와 최단 경로를 계산한다.
/// 화면 표시와 캐릭터 이동은 처리하지 않는다.
/// </summary>
public static class BattleTileRangeCalculator
{
    /// <summary>시작 타일에서 이동 비용 안에 도달 가능한 타일과 각 타일까지의 거리를 BFS로 계산한다.</summary>
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

    /// <summary>MapInfo에 연결된 유효한 상하좌우 인접 타일만 열거한다.</summary>
    public static IEnumerable<MapInfo> GetNeighbours(MapInfo tile)
    {
        yield return tile.Up;
        yield return tile.Down;
        yield return tile.Left;
        yield return tile.Right;
    }
}
