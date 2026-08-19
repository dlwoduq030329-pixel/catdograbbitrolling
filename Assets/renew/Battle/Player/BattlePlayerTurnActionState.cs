/// <summary>
/// 한 Player 턴 안에서 주사위 입력과 이동 사용 여부만 보관한다.
/// 턴 진행, 이동 실행, MP 계산은 담당하지 않는다.
/// </summary>
public sealed class BattlePlayerTurnActionState
{
    public bool DiceRolled { get; private set; }
    public bool MovementUsed { get; private set; }

    /// <summary>주사위 결과가 전달되어 Player 행동 입력이 열린 상태로 전환한다.</summary>
    public void MarkDiceRolled()
    {
        DiceRolled = true;
    }

    /// <summary>이번 턴의 이동 행동을 사용한 상태로 전환한다.</summary>
    public void MarkMovementUsed()
    {
        MovementUsed = true;
    }

    /// <summary>새 Player 턴을 위해 주사위와 이동 사용 상태를 초기화한다.</summary>
    public void Reset()
    {
        DiceRolled = false;
        MovementUsed = false;
    }
}
