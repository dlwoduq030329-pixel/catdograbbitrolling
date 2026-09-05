using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 행동에 필요한 맵 타일 목록과 다른 Enemy의 점유 타일을 제공한다.
/// 경로 계산, AI 판단과 이동 실행은 담당하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyMapLookup : MonoBehaviour
{
    /// <summary>
    /// EnemyTurnActor.TakeTurn()이 매 턴 이동 경로를 계산하기 전에 호출해 이번 턴에 쓸 맵 타일 목록을 얻는다.
    /// BattleSceneInstaller가 Scene 시작 시 dataPool.Map(BattleMapRegistry)에 타일을 미리 등록해두므로,
    /// 정상적으로 구성된 Scene에서는 항상 그 등록된 목록(dataPool.Map.Tiles)을 반환한다.
    /// BattleSceneInstaller를 아직 배치하지 않아 Registry가 비어 있는 Scene에서만 FindObjectsByType로
    /// Scene 전체를 검색하는 fallback을 탄다(2026-08-21 확인: 현재 존재하는 4개 Battle Scene은 전부
    /// BattleSceneInstaller가 배치되어 있어 이 fallback을 타지 않는다 — 새 Scene 제작 시 대비용 안전망).
    /// </summary>
    public IReadOnlyList<MapInfo> GetMapTiles(BattleDataPool dataPool)
    {
        if (dataPool != null && dataPool.Map != null && dataPool.Map.Tiles.Count > 0)
        {
            return dataPool.Map.Tiles;
        }

        return FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
    }

    /// <summary>
    /// EnemyTurnActor.TakeTurn()이 경로탐색 전에 호출해, 자신을 제외한 다른 활성 Enemy가 지금 서 있는
    /// 타일과 Shop·Chest 타일을 합쳐 "이동 금지 타일" 집합을 구한다. MapPathfinder가 이 집합을 피해서 길을 찾는다.
    /// 정상 경로에서는 dataPool.Map.FillOccupiedTiles를 통해 모든 Enemy(owner 포함)의 점유 타일을 먼저
    /// 계산한 뒤 owner 자신의 타일만 제외한다. FillOccupiedTiles 자체는 저장된 점유 기록을 쓰지 않고
    /// 매 호출마다 각 Enemy의 현재 위치로 가장 가까운 타일을 다시 계산하는 임시 함수다
    /// (BattleMapRegistry.cs 참고, Registry 등록 누락을 숨길 수 있어 최종적으로는 제거 예정).
    /// dataPool 계열이 없으면 Scene을 직접 검색하는 fallback으로 같은 결과를 만든다.
    /// 2026-09-05: Shop(TileType.Store)·Chest(TileType.Box) 타일도 이 집합에 추가했다. 두 타일은
    /// MapInfo.IsWalkable이 true라 Player처럼 Enemy도 경로탐색에서 지나갈 수 있는 타일로 취급됐는데,
    /// 실제로는 그 타일 프리팹 자체에 상점·상자 모델이 이미 서 있어서 Enemy가 그 위를 지나가거나
    /// 멈추면 모델끼리 시각적으로 겹쳐 보이는 문제("Enemy가 chest/shop이랑 겹쳐지더라" 피드백)가 있었다.
    /// Player는 상점/상자를 열려면 그 타일에 직접 서야 하므로 계속 걸어갈 수 있어야 하지만, Enemy는
    /// 그 타일에 볼일이 없으므로 다른 Enemy 점유 타일과 똑같이 우회 대상으로 묶었다.
    /// reachableTargetTile은 지금 이 탐색이 실제로 도달하려는 목적 타일(추격 대상 Player/허수아비가
    /// 서 있는 타일)이다. 만약 그 목적 타일이 하필 Store·Box 타일이면(플레이어가 상점 타일 위에 서
    /// 있는 채로 도망 다니는 경우 등) 위 우회 규칙 때문에 그 타일 자체가 막혀 영원히 도달·공격할 수
    /// 없게 되는 걸 막기 위해 blockedTiles에서 다시 빼준다. Wander처럼 특정 목적지가 없는 호출은
    /// null을 넘기면 된다.
    /// </summary>
    public HashSet<MapInfo> FindOtherEnemyTiles(
        BattleDataPool dataPool,
        EnemyTurnActor owner,
        IReadOnlyList<MapInfo> mapTiles,
        MapInfo reachableTargetTile = null)
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
        }
        else
        {
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
        }

        AddShopAndChestTiles(mapTiles, occupiedTiles);
        if (reachableTargetTile != null)
        {
            occupiedTiles.Remove(reachableTargetTile);
        }

        return occupiedTiles;
    }

    /// <summary>
    /// mapTiles 중 Shop(TileType.Store)·Chest(TileType.Box) 타일을 blockedTiles 집합에 추가한다.
    /// </summary>
    private static void AddShopAndChestTiles(IReadOnlyList<MapInfo> mapTiles, HashSet<MapInfo> blockedTiles)
    {
        if (mapTiles == null)
        {
            return;
        }

        for (int i = 0; i < mapTiles.Count; i++)
        {
            MapInfo tile = mapTiles[i];
            if (tile != null && (tile.Type == TileType.Store || tile.Type == TileType.Box))
            {
                blockedTiles.Add(tile);
            }
        }
    }
}
