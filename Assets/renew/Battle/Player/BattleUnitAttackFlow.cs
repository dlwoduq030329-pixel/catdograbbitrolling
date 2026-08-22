using UnityEngine;

/// <summary>
/// BattlePlayerActionController에서 분리한 "기본 공격" 행동 플로우 전용 컴포넌트다.
/// 우클릭으로 Enemy를 골라 공격을 계획(TryBegin)하고, 확인 UI 요청/확정/취소를
/// 전담하는 BattleBasicAttackController의 이벤트를 받아 처리한다.
///
/// BattleUnitMoveFlow와 마찬가지로 "Inspector에서 세팅하는 값"은 없고(BattleBasicAttackController
/// 자체가 GetOrAdd로 자동 부착되는 컴포넌트라 Scene에 저장된 실제 데이터가 없었다), 순수하게
/// 기본 공격 로직만 담당한다 — 용병단처럼 조작 가능한 유닛이 여러 개가 되어도 유닛마다 이
/// 컴포넌트 하나씩만 붙이면 기본 공격 로직은 그대로 재사용 가능하도록 만든 것이 분리 목적이다.
/// </summary>
public class BattleUnitAttackFlow : MonoBehaviour
{
    private BattlePlayerActionController owner;

    [SerializeField] private BattleBasicAttackController battleBasicAttackController;

    /// <summary>확인 대기 단계인지 여부.</summary>
    public bool IsAwaitingConfirmation =>
        battleBasicAttackController != null && battleBasicAttackController.IsAwaitingConfirmation;

    /// <summary>공격 실행(임시 이동 포함) 중인지 여부.</summary>
    public bool IsExecuting =>
        battleBasicAttackController != null && battleBasicAttackController.IsExecuting;

    /// <summary>확인 대기 또는 실행 중, 즉 다른 행동을 막아야 하는 상태인지 여부.</summary>
    public bool IsActive => IsAwaitingConfirmation || IsExecuting;

    /// <summary>소유자(BattlePlayerActionController)를 연결하고 하위 컴포넌트를 확보한다.</summary>
    public void Attach(BattlePlayerActionController controller)
    {
        owner = controller;
        EnsureBattleBasicAttackController();
    }

    private void OnDestroy()
    {
        if (battleBasicAttackController != null)
        {
            battleBasicAttackController.ConfirmationRequested -= HandleConfirmationRequested;
            battleBasicAttackController.Confirmed -= HandleConfirmed;
            battleBasicAttackController.Cancelled -= HandleCancelled;
        }
    }

    /// <summary>기본 공격의 계획, 임시 이동과 MP 확정을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void EnsureBattleBasicAttackController()
    {
        owner.EnsureBattlePlayerMover();

        battleBasicAttackController = BattleComponentResolver.GetOrAdd(gameObject, battleBasicAttackController);
        battleBasicAttackController.Configure(
            owner.player,
            owner.playerCombatData,
            owner.battlePlayerMover,
            owner.FindClosestMapTile,
            BattleMapTraversalService.IsWalkable);
        battleBasicAttackController.ConfirmationRequested -= HandleConfirmationRequested;
        battleBasicAttackController.Confirmed -= HandleConfirmed;
        battleBasicAttackController.Cancelled -= HandleCancelled;
        battleBasicAttackController.ConfirmationRequested += HandleConfirmationRequested;
        battleBasicAttackController.Confirmed += HandleConfirmed;
        battleBasicAttackController.Cancelled += HandleCancelled;
    }

    /// <summary>새 Player 턴에 기본 공격 상태를 초기화한다.</summary>
    public void ResetTurn()
    {
        battleBasicAttackController?.ResetTurn();
    }

    /// <summary>확인 대기 중인 기본 공격을 확정한다.</summary>
    public void Confirm()
    {
        battleBasicAttackController?.Confirm();
    }

    /// <summary>확인 대기 중인 기본 공격을 취소한다.</summary>
    public void Cancel()
    {
        battleBasicAttackController?.Cancel();
    }

    /// <summary>
    /// 우클릭한 Enemy를 공격할 수 있는 후보 타일 중 이동 경로가 가장 짧은 타일을 선택한다.
    /// </summary>
    public void TryBegin(EnemyTurnActor enemy)
    {
        EnsureBattleBasicAttackController();
        owner.EnsureBattlePlayerRangeController();
        battleBasicAttackController.Begin(
            enemy,
            owner.battlePlayerRangeController.ReachableTiles,
            owner.battlePlayerRangeController.OccupiedEnemyTiles);
    }

    private void HandleConfirmationRequested(string message) => owner.ShowActionConfirmationUI(message);

    private void HandleConfirmed(BattleActionResult result)
    {
        if (result.MovementMPCost > 0)
        {
            owner.turnActionState.MarkMovementUsed();
            BattleGameManager.Instance?.ResetDiceOnMove();
        }

        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);

        BattleUnitMP playerMP = owner.player != null ? owner.player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"기본 공격 확정: 이동 {result.MovementMPCost}MP + 공격 {result.ActionMPCost}MP, " +
            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}. 피해 적용은 아직 연결하지 않았습니다.",
            this);
        owner.moveFlow.ShowMoveRange();
    }

    private void HandleCancelled()
    {
        owner.HideActionConfirmationUI();
        owner.moveFlow.ShowMoveRange();
    }
}
