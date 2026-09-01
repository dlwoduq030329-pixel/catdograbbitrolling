using System;
using UnityEngine;

/// <summary>
/// Player 턴의 주사위 상태와 결과 생성을 전담한다.
/// 턴 진행 가능 여부는 BattleGameManager가 전달하고, 이 클래스는 한 턴 한 번 굴림 규칙과
/// 확정된 숫자, 연출 시작·완료 신호만 관리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDiceSystem : MonoBehaviour
{
    [Header("주사위 범위")]
    [SerializeField, Min(1)] private int minimumDiceValue = 1;
    [SerializeField, Min(1)] private int maximumDiceValue = 6;

    /// <summary>이번 Player 턴에 주사위를 이미 굴렸는지 나타낸다.</summary>
    public bool HasRolledThisTurn { get; private set; }

    /// <summary>현재 확정된 주사위 값. 이동 후 또는 새 턴에는 0이다.</summary>
    public int CurrentDiceValue { get; private set; }

    /// <summary>굴림 성공 여부와 결과값을 연출 계층에 전달한다. 실패한 경우 값은 0이다.</summary>
    public event Action<bool, int> DiceRollResolved;

    /// <summary>주사위 결과 연출이 끝났음을 전투 흐름에 전달한다.</summary>
    public event Action<int> DicePresentationCompleted;

    /// <summary>
    /// Manager가 계산한 현재 입력 가능 여부를 받아 주사위를 굴린다.
    /// 이미 굴린 턴이거나 입력할 수 없는 상태면 false를 반환한다.
    /// </summary>
    public bool TryRollDice(bool canRollNow)
    {
        if (!canRollNow || HasRolledThisTurn)
        {
            DiceRollResolved?.Invoke(false, 0);
            return false;
        }

        int safeMinimum = Mathf.Max(1, minimumDiceValue);
        int safeMaximum = Mathf.Max(safeMinimum, maximumDiceValue);
        HasRolledThisTurn = true;
        CurrentDiceValue = UnityEngine.Random.Range(safeMinimum, safeMaximum + 1);

        if (DiceRollResolved == null)
        {
            // 연출 컴포넌트가 없는 Scene에서도 전투 흐름은 즉시 계속된다.
            CompletePresentation(CurrentDiceValue);
        }
        else
        {
            DiceRollResolved.Invoke(true, CurrentDiceValue);
        }

        return true;
    }

    /// <summary>Presenter가 확정된 값의 연출을 끝냈을 때 호출한다.</summary>
    public void CompletePresentation(int presentedDiceValue)
    {
        if (!HasRolledThisTurn || presentedDiceValue <= 0 ||
            CurrentDiceValue != presentedDiceValue)
            return;

        DicePresentationCompleted?.Invoke(presentedDiceValue);
    }

    /// <summary>새 Player 턴을 시작하거나 턴을 강제로 넘길 때 굴림 상태와 값을 모두 초기화한다.</summary>
    public void ResetForNewTurn()
    {
        HasRolledThisTurn = false;
        CurrentDiceValue = 0;
    }

    /// <summary>이동 후 표시값만 지운다. 굴림 여부는 유지하므로 같은 턴에 다시 굴릴 수 없다.</summary>
    public void ClearDisplayedValueAfterMove()
    {
        CurrentDiceValue = 0;
    }
}
