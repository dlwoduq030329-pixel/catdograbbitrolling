using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 Battle Map에 생성된 MapInfo 목록의 공식 조회 원본이다.
/// 현재는 Unit 점유 정보까지 함께 보관하지만 Unit 존재·점유·해제의 원자성을 보장하기 위해
/// 점유 정보는 BattleUnitRegistry의 Unit 등록 데이터로 통합할 예정이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleMapRegistry : MonoBehaviour
{
    [SerializeField] private List<MapInfo> tiles = new List<MapInfo>();

    private readonly Dictionary<GameObject, MapInfo> occupiedTiles =
        new Dictionary<GameObject, MapInfo>();

    public IReadOnlyList<MapInfo> Tiles => tiles;
    public IReadOnlyDictionary<GameObject, MapInfo> OccupiedTiles => occupiedTiles;

    /// <summary>
    /// MapGenerator가 생성 완료한 타일을 한 번 등록해 이후 AI·이동·공격이 Scene 전체 MapInfo를 다시 찾지 않게 한다.
    /// null과 중복 타일은 공식 목록에 넣지 않는다.
    /// </summary>
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

    /// <summary>
    /// 한 Unit의 최초 배치 또는 이동 완료 타일을 기록한다.
    /// 현재 임시 API이며 Unit 목록과 점유 기록이 따로 제거되는 불일치를 막기 위해 BattleUnitRegistry로 이동한다.
    /// </summary>
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

    /// <summary>
    /// 등록된 Unit의 공식 점유 타일을 반환한다. 등록 누락이면 tile은 null이고 false다.
    /// 호출자는 실패 시 Transform 위치를 조용히 추정하지 말고 Spawn·Move 등록 누락을 수정해야 한다.
    /// </summary>
    public bool TryGetOccupiedTile(GameObject unit, out MapInfo tile)
    {
        tile = null;
        return unit != null && occupiedTiles.TryGetValue(unit, out tile);
    }

    /// <summary>
    /// 등록된 타일 목록에서 월드 XZ 좌표가 가장 가까운 타일을 반환한다.
    /// 이 조회는 위치를 Map 타일로 변환하는 기능이며 Unit 점유 상태를 변경하지 않는다.
    /// </summary>
    public MapInfo FindClosestTile(Vector3 worldPosition)
    {
        return BattleTileLocator.FindClosestXZ(worldPosition, tiles);
    }

    /// <summary>
    /// 전달받은 Unit Transform을 가장 가까운 타일로 다시 계산해 결과 집합에 채운다.
    /// 저장된 occupiedTiles를 사용하지 않아 Registry 등록 누락을 숨기는 임시 호환 함수이며,
    /// Spawn·Move가 공식 점유 타일을 항상 갱신하게 된 뒤 제거한다.
    /// </summary>
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

    /// <summary>
    /// 전투에서 제거된 Unit의 점유 기록만 삭제한다.
    /// UnitRegistry 해제와 별도 호출해야 하는 현재 구조는 불일치 위험이 있으므로 단일 UnregisterUnit API로 통합한다.
    /// </summary>
    public void RemoveUnit(GameObject unit)
    {
        if (unit != null)
        {
            occupiedTiles.Remove(unit);
        }
    }

    /// <summary>Scene 재구성 전에 생성 타일 목록과 현재 임시 점유 기록을 모두 비운다.</summary>
    public void Clear()
    {
        tiles.Clear();
        occupiedTiles.Clear();
    }
}
