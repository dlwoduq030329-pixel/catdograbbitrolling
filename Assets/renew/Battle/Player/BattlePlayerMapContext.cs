using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 행동에서 사용하는 현재 맵 타일 목록과 최근접 타일 조회를 제공한다.
/// 이동 가능 여부, 범위 계산과 입력 상태는 처리하지 않는다.
///
/// 타일 소스는 항상 BattleDataPool.Map(BattleMapRegistry)을 우선한다. 거기 등록된 타일이
/// 하나도 없을 때만(Registry에 아직 아무것도 등록되지 않은 구형/누락 Scene 대비) 씬 전체를
/// FindObjectsByType&lt;MapInfo&gt;로 훑어서 대신 채운다 — 즉 Registry가 정상적으로 채워진
/// 일반적인 상황에서는 씬 전체 탐색이 실행되지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerMapContext : MonoBehaviour
{
    // Refresh() 호출 시점의 타일 목록 스냅샷. Registry에서 가져왔든 씬 탐색으로 가져왔든
    // 항상 여기 채워두고, FindClosest()가 Registry를 못 쓸 때 이 캐시로 대체 조회한다.
    private readonly List<MapInfo> cachedTiles = new List<MapInfo>();

    public IReadOnlyList<MapInfo> Tiles => cachedTiles;

    /// <summary>
    /// 현재 맵의 타일 목록을 다시 채운다. BattleDataPool.Map(Registry)에 타일이 있으면 그대로 복사하고,
    /// 비어 있을 때만 씬을 훑어 활성 MapInfo를 전부 수집한다(구형/Registry 미등록 Scene 대비 fallback).
    /// </summary>
    public void Refresh(BattleDataPool battleDataPool, BattleRangeVisualizer rangeVisualizer)
    {
        cachedTiles.Clear();
        if (battleDataPool != null && battleDataPool.Map != null && battleDataPool.Map.Tiles.Count > 0)
        {
            cachedTiles.AddRange(battleDataPool.Map.Tiles);
        }
        else
        {
            cachedTiles.AddRange(FindObjectsByType<MapInfo>(FindObjectsSortMode.None));
        }

        rangeVisualizer?.CacheOriginalColors(cachedTiles);
    }

    /// <summary>
    /// worldPosition에서 가장 가까운 타일을 찾는다. BattleDataPool.Map(Registry)이 채워져 있으면
    /// Registry의 최근접 탐색을 그대로 쓰고, 없을 때만 Refresh()로 모아둔 cachedTiles를 직접 순회한다.
    /// </summary>
    public MapInfo FindClosest(BattleDataPool battleDataPool, Vector3 worldPosition)
    {
        if (battleDataPool != null && battleDataPool.Map != null && battleDataPool.Map.Tiles.Count > 0)
        {
            return battleDataPool.Map.FindClosestTile(worldPosition);
        }

        return BattleTileLocator.FindClosestXZ(worldPosition, cachedTiles);
    }
}
