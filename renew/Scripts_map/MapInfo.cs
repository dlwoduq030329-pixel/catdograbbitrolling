using UnityEngine;

/// <summary>
/// 맵의 각 타일이 가지는 정보
/// </summary>
public class MapInfo : MonoBehaviour
{
    /// <summary>
    /// 맵 상에서의 좌표
    /// </summary>
    public Vector2Int Index { get; private set; }

    /// <summary>
    /// 현재 타일 타입
    /// </summary>
    public TileType Type { get; private set; }

    /// <summary>
    /// 월드 좌표
    /// </summary>
    public Vector3 WorldPos { get; private set; }

    /// <summary>
    /// 상하좌우 인접 타일
    /// 맵 생성이 모두 끝난 후 연결해준다.
    /// </summary>
    public MapInfo Up;
    public MapInfo Down;
    public MapInfo Left;
    public MapInfo Right;

    /// <summary>
    /// 이동 가능한 타일인지
    /// </summary>
    public bool IsWalkable
    {
        get
        {
            return Type == TileType.Road
                || Type == TileType.Store
                || Type == TileType.Box
                || Type == TileType.Exit;
        }
    }

    /// <summary>
    /// 타일 초기화
    /// </summary>
    public void Init(Vector2Int index, TileType type, Vector3 worldPos)
    {
        Index = index;
        Type = type;
        WorldPos = worldPos;
    }

    /// <summary>
    /// 타일 타입 변경
    /// </summary>
    public void SetType(TileType type)
    {
        Type = type;
    }

    /// <summary>
    /// 인접 타일 연결
    /// </summary>
    public void SetNeighbour(
        MapInfo up,
        MapInfo down,
        MapInfo left,
        MapInfo right)
    {
        Up = up;
        Down = down;
        Left = left;
        Right = right;
    }
}