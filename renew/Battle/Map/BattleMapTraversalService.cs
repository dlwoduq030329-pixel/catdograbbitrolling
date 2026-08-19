/// <summary>
/// Player 이동과 경로 계산에서 사용하는 공용 타일 통행 규칙을 제공한다.
/// 범위 계산이나 경로 탐색은 담당하지 않는다.
/// </summary>
public static class BattleMapTraversalService
{
    /// <summary>일반 이동 가능 타일과 시작 타일을 통행 가능한 타일로 판정한다.</summary>
    public static bool IsWalkable(MapInfo tile)
    {
        return tile != null && (tile.IsWalkable || tile.Type == TileType.Start);
    }
}
