using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapInfo의 상하좌우 연결을 사용하는 공용 BFS 경로 탐색기다.
/// 경로 데이터만 반환하며 이동 연출이나 MP 차감은 담당하지 않는다.
/// </summary>
public static class MapPathfinder
{
    /// <summary>
    /// 시작 타일을 제외하고 목적지까지 순서대로 정렬된 최단 경로를 반환한다.
    /// blockedTiles에는 다른 Enemy가 점유한 타일처럼 이번 탐색에서 제외할 타일을 전달한다.
    /// </summary>
    public static bool TryFindShortestPath(
        MapInfo start,
        MapInfo target,
        ISet<MapInfo> blockedTiles,
        out List<MapInfo> path)
    {
        path = new List<MapInfo>();

        // 경로 요청자는 시작·목적 타일을 보장하는 것이 원칙이다. 다만 Map 생성/등록 누락이
        // 실제 전투 중 무한 탐색이나 NullReference로 확대되지 않도록 공개 경계에서 한 번 거부한다.
        if (start == null || target == null)
        {
            return false;
        }

        // 이미 목적 타일에 있으면 이동할 칸이 없으므로 빈 경로를 성공으로 반환한다.
        if (start == target)
        {
            return true;
        }

        Queue<MapInfo> queue = new Queue<MapInfo>();
        Dictionary<MapInfo, MapInfo> previous = new Dictionary<MapInfo, MapInfo>();
        queue.Enqueue(start);
        previous[start] = null;

        // BFS는 시작점에서 같은 거리의 타일을 먼저 검사하므로 목적지를 처음 발견한 경로가 최단 경로다.
        while (queue.Count > 0)
        {
            MapInfo current = queue.Dequeue();

            foreach (MapInfo neighbour in GetNeighbours(current))
            {
                if (neighbour == null ||
                    previous.ContainsKey(neighbour) ||
                    !IsWalkable(neighbour) ||
                    (blockedTiles != null && blockedTiles.Contains(neighbour)))
                {
                    continue;
                }

                previous[neighbour] = current;

                if (neighbour == target)
                {
                    // previous를 목적지부터 시작점까지 역추적한 뒤 실제 이동 순서로 뒤집는다.
                    // 반환 경로에는 출발 타일을 넣지 않아 Path.Count가 실제 이동 칸 수와 일치한다.
                    MapInfo pathTile = target;
                    while (pathTile != start)
                    {
                        path.Add(pathTile);
                        pathTile = previous[pathTile];
                    }

                    path.Reverse();
                    return true;
                }

                queue.Enqueue(neighbour);
            }
        }

        return false;
    }

    /// <summary>월드 좌표와 XZ 평면상 가장 가까운 MapInfo 타일을 찾는다.</summary>
    public static MapInfo FindClosestTile(Vector3 worldPosition, IReadOnlyList<MapInfo> mapTiles)
    {
        return BattleTileLocator.FindClosestXZ(worldPosition, mapTiles);
    }

    /// <summary>MapInfo에 연결된 상하좌우 인접 타일을 순서대로 반환한다.</summary>
    private static IEnumerable<MapInfo> GetNeighbours(MapInfo tile)
    {
        yield return tile.Up;
        yield return tile.Down;
        yield return tile.Left;
        yield return tile.Right;
    }

    /// <summary>타일이 존재하고 이동 가능한 지형인지 검사하며 시작 타일은 별도로 허용한다.</summary>
    private static bool IsWalkable(MapInfo tile)
    {
        // 시작 타일은 IsWalkable 값과 관계없이 경로 탐색에 포함한다.
        return tile != null && (tile.IsWalkable || tile.Type == TileType.Start);
    }
}
