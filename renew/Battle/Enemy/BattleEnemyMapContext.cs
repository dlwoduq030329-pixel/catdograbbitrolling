using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 행동에 필요한 맵 타일 목록과 다른 Enemy의 점유 타일을 제공한다.
/// 경로 계산, AI 판단과 이동 실행은 담당하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyMapContext : MonoBehaviour
{
    /// <summary>Registry 타일을 우선 반환하고 구형 Scene에서만 전체 MapInfo를 검색한다.</summary>
    public IReadOnlyList<MapInfo> ResolveMapTiles(BattleDataPool dataPool)
    {
        if (dataPool != null && dataPool.Map != null && dataPool.Map.Tiles.Count > 0)
        {
            return dataPool.Map.Tiles;
        }

        return FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
    }

    /// <summary>자신을 제외한 활성 Enemy가 점유 중인 타일을 수집한다.</summary>
    public HashSet<MapInfo> FindOtherEnemyTiles(
        BattleDataPool dataPool,
        EnemyTurnActor owner,
        IReadOnlyList<MapInfo> mapTiles)
    {
        HashSet<MapInfo> occupiedTiles = new HashSet<MapInfo>();
        if (owner == null)
        {
            return occupiedTiles;
        }

        if (dataPool != null && dataPool.Units != null && dataPool.Map != null)
        {
            dataPool.Map.FillOccupiedTiles(dataPool.Units.Enemies, occupiedTiles);

            MapInfo ownTile = dataPool.Map.FindClosestTile(owner.transform.position);
            if (ownTile != null)
            {
                occupiedTiles.Remove(ownTile);
            }

            return occupiedTiles;
        }

        EnemyTurnActor[] enemies = FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            if (enemy == null || enemy == owner || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            MapInfo occupiedTile = MapPathfinder.FindClosestTile(enemy.transform.position, mapTiles);
            if (occupiedTile != null)
            {
                occupiedTiles.Add(occupiedTile);
            }
        }

        return occupiedTiles;
    }
}
