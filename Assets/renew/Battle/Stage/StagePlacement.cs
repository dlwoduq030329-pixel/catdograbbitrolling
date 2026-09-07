using System.Collections.Generic;
using UnityEngine;

/// <summary>배치에 사용하는 좌표와 순서 계산. 씬 검색·오브젝트 생성·전투 상태 변경을 하지 않는다.</summary>
public static class StagePlacement
{
    public static bool IsInsideSquare(Vector2Int tile, Vector2Int center, int radius)
    {
        return Mathf.Abs(tile.x - center.x) <= radius && Mathf.Abs(tile.y - center.y) <= radius;
    }

    public static void Shuffle<T>(List<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T value = values[i]; values[i] = values[j]; values[j] = value;
        }
    }

    /// <summary>보스 중심 3×3이면 radius=1. 중심·점유·특수 타일은 소환 위치에서 제외한다.</summary>
    public static void CollectSummonTiles(IEnumerable<MapInfo> tiles, MapInfo center,
        ISet<MapInfo> occupied, int radius, List<MapInfo> result)
    {
        if (result == null) return;
        result.Clear();
        if (center == null || tiles == null || occupied == null || radius < 1) return; 
        foreach (MapInfo tile in tiles)
            if (tile != null && tile != center && tile.Type == TileType.Road && tile.IsWalkable &&
                !occupied.Contains(tile) && IsInsideSquare(tile.Index, center.Index, radius) && !result.Contains(tile))
                result.Add(tile);
    }
}
