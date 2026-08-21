/// <summary>
/// "이 타일을 지나갈 수 있는가"라는 단 하나의 규칙만 담당하는 공용 판정기다.
/// 범위 계산이나 경로 탐색 자체는 담당하지 않는다.
/// 이 규칙 하나를 위해 별도 static 클래스를 둔 이유: 카드 이동(`BattleCardMovementService`),
/// Player 실제 이동 실행(`BattlePlayerActionController`), 위협 범위 미리보기(`BattleMoveThreatPreview`)
/// 3곳에서 총 8번 호출되는데, "통행 가능 = IsWalkable 이거나 시작 타일"이라는 조건을 각 파일에 따로
/// 인라인으로 적으면 나중에 한쪽만 고치고 다른 쪽을 놓치는 규칙 불일치가 생기기 쉽다. 그 위험을
/// 막기 위한 단일 진실 공급원(single source of truth)이다.
/// </summary>
public static class BattleMapTraversalService
{
    /// <summary>일반 이동 가능 타일(`MapInfo.IsWalkable`)과 시작 타일(`TileType.Start`)을
    /// 모두 통행 가능한 타일로 판정한다.</summary>
    public static bool IsWalkable(MapInfo tile)
    {
        return tile != null && (tile.IsWalkable || tile.Type == TileType.Start);
    }
}
