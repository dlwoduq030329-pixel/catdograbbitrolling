using System.Collections.Generic;
using UnityEngine;

/// <summary>월드 좌표(대부분 어떤 유닛의 현재 transform.position)에 가장 가까운 MapInfo 타일을
/// 찾는 공용 계산기다. "이 유닛이 지금 어느 타일 위/근처에 있는가"를 구하는 용도가 대부분이며,
/// 도착지 좌표를 찾는 함수가 아니다(호출부 확인: `BattleCardMovementService`/`BattleMapRegistry`/
/// `BattlePlayerMapContext`/`BattleMoveThreatPreview`는 전부 `unit.transform.position`을,
/// `EnemySpawner`는 스폰 후보 좌표를 넘긴다). `MapPathfinder.FindClosestTile`을 포함해
/// 실질적으로 6개 파일에서 참조하는 핵심 유틸리티다.</summary>
public static class BattleTileLocator
{
    /// <summary>높이(y)를 무시하고 XZ 평면 거리만으로 가장 가까운 타일을 반환한다. Player/Enemy가
    /// 점프·경사로 등으로 y가 흔들려도 같은 타일로 인식되게 하려는 목적이다.</summary>
    public static MapInfo FindClosestXZ(
        Vector3 worldPosition,
        IReadOnlyList<MapInfo> tiles)
    {
        return FindClosest(worldPosition, tiles, includeHeight: false);
    }

    /// <summary>y까지 포함한 XYZ 전체 거리로 가장 가까운 타일을 반환한다. `EnemySpawner`가 스폰
    /// 위치를 계산할 때처럼 높낮이가 다른 타일을 구분해야 하는 경우에만 사용한다.</summary>
    public static MapInfo FindClosest3D(
        Vector3 worldPosition,
        IReadOnlyList<MapInfo> tiles)
    {
        return FindClosest(worldPosition, tiles, includeHeight: true);
    }

    /// <summary>모든 타일을 순회하며 worldPosition과의 거리(제곱)를 비교해 가장 가까운 하나를 고른다.
    /// 제곱근(sqrt) 계산이 필요한 실제 거리 대신 sqrMagnitude(제곱합)로만 비교하는 이유는, 대소
    /// 비교 목적에서는 제곱값의 순서가 실제 거리의 순서와 같아서 sqrt 연산을 아낄 수 있기 때문이다.
    /// includeHeight가 false면 비교 전에 delta.y를 0으로 지워 y축 차이를 무시한다.</summary>
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
