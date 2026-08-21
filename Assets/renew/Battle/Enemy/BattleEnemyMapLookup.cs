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
    /// 타일 집합을 "이동 금지 타일"로 구한다. MapPathfinder가 이 집합을 피해서 길을 찾는다.
    /// 정상 경로에서는 dataPool.Map.FillOccupiedTiles를 통해 모든 Enemy(owner 포함)의 점유 타일을 먼저
    /// 계산한 뒤 owner 자신의 타일만 제외한다. FillOccupiedTiles 자체는 저장된 점유 기록을 쓰지 않고
    /// 매 호출마다 각 Enemy의 현재 위치로 가장 가까운 타일을 다시 계산하는 임시 함수다
    /// (BattleMapRegistry.cs 참고, Registry 등록 누락을 숨길 수 있어 최종적으로는 제거 예정).
    /// dataPool 계열이 없으면 Scene을 직접 검색하는 fallback으로 같은 결과를 만든다.
    /// </summary>
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
