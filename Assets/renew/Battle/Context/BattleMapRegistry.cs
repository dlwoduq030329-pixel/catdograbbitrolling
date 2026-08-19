using System.Collections.Generic;
using UnityEngine;

/// <summary>현재 생성된 MapInfo 목록과 Unit별 점유 타일 참조를 보관한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleMapRegistry : MonoBehaviour
{
    [SerializeField] private List<MapInfo> tiles = new List<MapInfo>();

    private readonly Dictionary<GameObject, MapInfo> occupiedTiles =
        new Dictionary<GameObject, MapInfo>();

    public IReadOnlyList<MapInfo> Tiles => tiles;
    public IReadOnlyDictionary<GameObject, MapInfo> OccupiedTiles => occupiedTiles;

    /// <summary>MapGenerator가 생성한 타일을 전투 맵의 공식 조회 목록으로 등록한다.</summary>
    public void RegisterTiles(IEnumerable<MapInfo> mapTiles)
    {
        tiles.Clear();
        if (mapTiles == null)
        {
            return;
        }

        foreach (MapInfo tile in mapTiles)
        {
            if (tile != null && !tiles.Contains(tile))
            {
                tiles.Add(tile);
            }
        }
    }

    /// <summary>한 Unit이 현재 점유한 타일을 기록한다. 이동 완료 시 최신 타일로 갱신해야 한다.</summary>
    public void SetOccupiedTile(GameObject unit, MapInfo tile)
    {
        if (unit == null)
        {
            return;
        }

        if (tile == null)
        {
            occupiedTiles.Remove(unit);
            return;
        }

        occupiedTiles[unit] = tile;
    }

    /// <summary>등록된 Unit의 점유 타일을 반환한다. 찾지 못하면 tile은 null이고 false를 반환한다.</summary>
    public bool TryGetOccupiedTile(GameObject unit, out MapInfo tile)
    {
        tile = null;
        return unit != null && occupiedTiles.TryGetValue(unit, out tile);
    }

    /// <summary>등록된 타일 중 월드 XZ 좌표가 가장 가까운 타일을 반환한다.</summary>
    public MapInfo FindClosestTile(Vector3 worldPosition)
    {
        return BattleTileLocator.FindClosestXZ(worldPosition, tiles);
    }

    /// <summary>지정된 Unit 목록의 현재 위치를 가장 가까운 타일로 변환해 결과 집합에 채운다.</summary>
    public void FillOccupiedTiles(IEnumerable<GameObject> units, ISet<MapInfo> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (units == null)
        {
            return;
        }

        foreach (GameObject unit in units)
        {
            if (unit == null || !unit.activeInHierarchy)
            {
                continue;
            }

            MapInfo tile = FindClosestTile(unit.transform.position);
            if (tile != null)
            {
                destination.Add(tile);
            }
        }
    }

    /// <summary>전투에서 제거된 Unit의 점유 기록을 삭제한다.</summary>
    public void RemoveUnit(GameObject unit)
    {
        if (unit != null)
        {
            occupiedTiles.Remove(unit);
        }
    }

    /// <summary>Scene 재구성 전에 타일 목록과 모든 점유 정보를 초기화한다.</summary>
    public void Clear()
    {
        tiles.Clear();
        occupiedTiles.Clear();
    }
}
