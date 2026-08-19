using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 행동에서 사용하는 현재 맵 타일 목록과 최근접 타일 조회를 제공한다.
/// 이동 가능 여부, 범위 계산과 입력 상태는 처리하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerMapContext : MonoBehaviour
{
    private readonly List<MapInfo> tiles = new List<MapInfo>();

    public IReadOnlyList<MapInfo> Tiles => tiles;

    /// <summary>Registry를 우선 사용하고 구형 Scene에서는 활성 MapInfo를 수집한다.</summary>
    public void Refresh(BattleDataPool dataPool, BattleRangeVisualizer visualizer)
    {
        tiles.Clear();
        if (dataPool != null && dataPool.Map != null && dataPool.Map.Tiles.Count > 0)
        {
            tiles.AddRange(dataPool.Map.Tiles);
        }
        else
        {
            tiles.AddRange(FindObjectsByType<MapInfo>(FindObjectsSortMode.None));
        }

        visualizer?.CacheOriginalColors(tiles);
    }

    /// <summary>Registry 조회를 우선하고 등록 전에는 수집한 타일 목록을 사용한다.</summary>
    public MapInfo FindClosest(BattleDataPool dataPool, Vector3 worldPosition)
    {
        if (dataPool != null && dataPool.Map != null && dataPool.Map.Tiles.Count > 0)
        {
            return dataPool.Map.FindClosestTile(worldPosition);
        }

        return BattleTileLocator.FindClosestXZ(worldPosition, tiles);
    }
}
