/// <summary>
/// 한 Player 턴 안에서 이동 입력 활성화 여부와 이동 사용 여부만 보관한다.
/// 턴 진행, 이동 실행, AP/MP 계산은 담당하지 않는다.
/// (2026-09-10: DiceRolled/MarkDiceRolled -> MovementActivated/MarkMovementActivated로 리네임.
/// 주사위를 굴려야 이동이 열리던 구조가 없어지고 턴 시작과 동시에 AP가 채워지면서 이동이 바로
/// 열리므로, "주사위"라는 이름이 더 이상 맞지 않았다.)
/// </summary>
public sealed class BattlePlayerTurnActionState
{
    public bool MovementActivated { get; private set; }
    public bool MovementUsed { get; private set; }

    /// <summary>이번 턴 이동 입력이 열린 상태로 전환한다.</summary>
    public void MarkMovementActivated()
    {
        MovementActivated = true;
    }

    /// <summary>이번 턴의 이동 행동을 사용한 상태로 전환한다.</summary>
    public void MarkMovementUsed()
    {
        MovementUsed = true;
    }

    /// <summary>새 Player 턴을 위해 이동 활성화·사용 상태를 초기화한다.</summary>
    public void Reset()
    {
        MovementActivated = false;
        MovementUsed = false;
    }
}
