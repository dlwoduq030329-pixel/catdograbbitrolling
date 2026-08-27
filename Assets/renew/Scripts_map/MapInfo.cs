using System;
using UnityEngine;

/// <summary>
/// 타일 사이의 높이 변화
/// </summary>
public enum HeightTransition
{
    Invalid,
    Flat,
    StepUp,
    StepDown,
    Climb,
    Drop
}

/// <summary>
/// 맵의 각 타일이 가지는 정보
/// </summary>
public class MapInfo : MonoBehaviour
{
    public Vector2Int Index { get; private set; }
    public TileType Type { get; private set; }
    public Vector3 WorldPos { get; private set; }

    /// <summary>
    /// River=0, 육지=1~3
    /// </summary>
    public int HeightIndex { get; private set; }

    public float WorldHeight { get; private set; }

    public MapInfo Up;
    public MapInfo Down;
    public MapInfo Left;
    public MapInfo Right;

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

    public event Action<MapInfo, MapInfo> OnFlatMove;
    public event Action<MapInfo, MapInfo> OnStepUp;
    public event Action<MapInfo, MapInfo> OnStepDown;
    public event Action<MapInfo, MapInfo> OnClimb;
    public event Action<MapInfo, MapInfo> OnDrop;

    public void Init(
        Vector2Int index,
        TileType type,
        Vector3 worldPos)
    {
        Init(
            index,
            type,
            worldPos,
            type == TileType.River ? 0 : 1,
            type == TileType.River ? 0f : worldPos.y);
    }

    public void Init(
        Vector2Int index,
        TileType type,
        Vector3 worldPos,
        int heightIndex,
        float worldHeight)
    {
        Index = index;
        Type = type;
        WorldPos = worldPos;

        HeightIndex =
            type == TileType.River
                ? 0
                : Mathf.Clamp(heightIndex, 1, 3);

        WorldHeight =
            type == TileType.River
                ? 0f
                : worldHeight;
    }

    public void SetType(TileType type)
    {
        Type = type;

        if (type == TileType.River)
        {
            HeightIndex = 0;
            WorldHeight = 0f;
        }
    }

    public void SetHeight(
        int heightIndex,
        float worldHeight)
    {
        HeightIndex =
            Type == TileType.River
                ? 0
                : Mathf.Clamp(heightIndex, 1, 3);

        WorldHeight =
            Type == TileType.River
                ? 0f
                : worldHeight;

        Vector3 position = transform.position;
        position.y = WorldHeight;
        transform.position = position;
        WorldPos = position;
    }

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

    public int GetHeightDifference(MapInfo next)
    {
        if (next == null)
            return 0;

        return next.HeightIndex - HeightIndex;
    }

    public HeightTransition GetTransitionTo(MapInfo next)
    {
        if (next == null)
            return HeightTransition.Invalid;

        int difference =
            next.HeightIndex - HeightIndex;

        if (difference == 0)
            return HeightTransition.Flat;

        if (difference == 1)
            return HeightTransition.StepUp;

        if (difference == -1)
            return HeightTransition.StepDown;

        if (difference > 1)
            return HeightTransition.Climb;

        return HeightTransition.Drop;
    }

    public bool TryInvokeMoveEvent(MapInfo next)
    {
        if (next == null || !next.IsWalkable)
            return false;

        switch (GetTransitionTo(next))
        {
            case HeightTransition.Flat:
                OnFlatMove?.Invoke(this, next);
                return true;

            case HeightTransition.StepUp:
                OnStepUp?.Invoke(this, next);
                return true;

            case HeightTransition.StepDown:
                OnStepDown?.Invoke(this, next);
                return true;

            case HeightTransition.Climb:
                OnClimb?.Invoke(this, next);
                return true;

            case HeightTransition.Drop:
                OnDrop?.Invoke(this, next);
                return true;

            default:
                return false;
        }
    }
}
