using System.Collections.Generic;
using UnityEngine;

/// <summary>월드 위치에서 가장 가까운 MapInfo 타일을 찾는 공용 계산기다.</summary>
public static class BattleTileLocator
{
    /// <summary>높이를 제외한 XZ 평면 거리로 가장 가까운 타일을 반환한다.</summary>
    public static MapInfo FindClosestXZ(
        Vector3 worldPosition,
        IReadOnlyList<MapInfo> tiles)
    {
        return FindClosest(worldPosition, tiles, includeHeight: false);
    }

    /// <summary>XYZ 전체 거리로 가장 가까운 타일을 반환한다.</summary>
    public static MapInfo FindClosest3D(
        Vector3 worldPosition,
        IReadOnlyList<MapInfo> tiles)
    {
        return FindClosest(worldPosition, tiles, includeHeight: true);
    }

    private static MapInfo FindClosest(
        Vector3 worldPosition,
        IReadOnlyList<MapInfo> tiles,
        bool includeHeight)
    {
        if (tiles == null)
        {
            return null;
        }

        MapInfo closest = null;
        float closestSqrDistance = float.MaxValue;
        foreach (MapInfo tile in tiles)
        {
            if (tile == null)
            {
                continue;
            }

            Vector3 delta = tile.transform.position - worldPosition;
            if (!includeHeight)
            {
                delta.y = 0f;
            }

            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closest = tile;
        }

        return closest;
    }
}
