using UnityEngine;

/// <summary>
/// Player 전용 카드 입력 흐름을 BattlePlayerActionController에서 분리한 컴포넌트다.
/// 손패에서 선택한 카드의 사용 시작, 마우스로 지정한 대상 전달, 같은 대상 재클릭을 통한 확정,
/// 취소와 결과 이벤트 전달을 담당한다. 실제 MP·효과·사거리 계산은 BattleCardActionController에 위임한다.
///
/// 이 컴포넌트는 Player의 손패와 직접 조작을 전제로 하므로 용병과 Enemy가 재사용하지 않는다.
/// 용병은 이동·기본 공격만 사용하고 Enemy는 별도 AI Skill 흐름에서 공용 효과 실행 계층만 사용한다.
/// </summary>
public class BattlePlayerCardFlow : MonoBehaviour
{
    private BattlePlayerActionController owner;

    [SerializeField] private BattleCardActionController battleCardActionController;

    /// <summary>대상 선택 단계인지 여부.</summary>
    public bool IsSelectingTarget =>
        battleCardActionController != null && battleCardActionController.IsSelectingTarget;

    /// <summary>확인 대기 단계인지 여부.</summary>
    public bool IsAwaitingConfirmation =>
        battleCardActionController != null && battleCardActionController.IsAwaitingConfirmation;

    /// <summary>대상 선택 또는 확인 대기 중, 즉 다른 행동을 막아야 하는 상태인지 여부.</summary>
    public bool IsActive => IsSelectingTarget || IsAwaitingConfirmation;

    /// <summary>소유자(BattlePlayerActionController)를 연결하고 하위 컴포넌트를 확보한다.</summary>
    public void Attach(BattlePlayerActionController controller)
    {
        owner = controller;
        InitializeCardActionController();
    }

    private void OnDestroy()
    {
        if (battleCardActionController != null)
        {
            battleCardActionController.TargetSelectionRequested -= HandleTargetSelectionRequested;
            battleCardActionController.Confirmed -= HandleConfirmed;
            battleCardActionController.Cancelled -= HandleCancelled;
            battleCardActionController.RangeVisibilityChanged -= owner.SetRangeVisible;
        }
    }

    /// <summary>카드 대상 선택, 사거리 표시와 MP·손패 확정을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void InitializeCardActionController()
    {
        owner.EnsureBattleRangeVisualizer();
        owner.EnsureBattlePushPreviewView();
        // Push View가 월드 위치를 화면 좌표로 바꾸므로 전투 Camera도 View에 직접 연결한다.
        owner.battlePushPreviewView.ConfigurePreviewDependencies(owner.mainCamera);

        battleCardActionController = BattleComponentResolver.GetOrAdd(gameObject, battleCardActionController);
        battleCardActionController.Configure(
            owner.player,
            owner.battleRangeVisualizer,
            owner.colorPalette.CardRangeTileColor,
            owner.colorPalette.CardEffectAreaTileColor,
            owner.FindClosestMapTile,
            owner.battlePlayerMapContext.Tiles,
            owner.battlePushPreviewView);
        battleCardActionController.TargetSelectionRequested += HandleTargetSelectionRequested;
        battleCardActionController.Confirmed += HandleConfirmed;
        battleCardActionController.Cancelled += HandleCancelled;
        battleCardActionController.RangeVisibilityChanged += owner.SetRangeVisible;
    }

    /// <summary>새 Player 턴에 카드 사용 상태를 초기화한다.</summary>
    public void ResetTurn()
    {
        battleCardActionController?.ResetTurn();
    }

    /// <summary>확인 대기 중인 카드 사용을 확정한다.</summary>
    public void Confirm()
    {
        battleCardActionController?.TryConfirmCardUse();
    }

    /// <summary>대상 선택 또는 확인 대기 중인 카드 사용을 취소한다.</summary>
    public void Cancel()
    {
        battleCardActionController?.Cancel();
    }

    /// <summary>
    /// 손패 클릭으로 만들어진 카드 사용 요청을 실제 대상 선택 흐름에 전달한다.
    /// 호출 경로: BattleCardHandView.SelectCard()
    /// → BattlePlayerActionController.BeginCardUseConfirmation()
    /// → 이 함수
    /// → BattleCardActionController.TryBeginSelectedCardFlow().
    /// 이동 범위를 닫고 현재 턴의 카드 사용 가능 상태를 전달할 뿐, 카드 효과나 MP는 여기서 소비하지 않는다.
    /// </summary>
    public bool TryStartSelectedCardUse(SelectedCardUseInfo cardUse, BattleCardDrawSystem cardDrawSystem)
    {
        owner.moveFlow.ClearMoveRange();
        bool canUseCards = BattleGameManager.Instance != null && BattleGameManager.Instance.CanUsePlayerCards;
        return battleCardActionController != null &&
               battleCardActionController.TryBeginSelectedCardFlow(cardUse, cardDrawSystem, canUseCards);
    }

    /// <summary>카드 대상 유형에 맞는 적 또는 타일을 우클릭으로 선택한다.</summary>
    public void HandleTargetRightClick(Vector2 pointerPosition)
    {
        if (owner.IsAnyActionMoving)
        {
            return;
        }

        if (BattlePlayerInputReader.IsPointerOverInteractiveUI(pointerPosition))
        {
            return;
        }

        BattleCardTargetType targetType = battleCardActionController.TargetType;
        if ((targetType == BattleCardTargetType.Enemy || targetType == BattleCardTargetType.Character) &&
            owner.TryRaycastEnemy(pointerPosition, out EnemyTurnActor enemy))
        {
            MapInfo enemyTile = owner.FindClosestMapTile(enemy.transform.position);
            if (battleCardActionController.TrySelectTargetAndEnterConfirmation(enemy.gameObject, enemyTile))
            {
                return;
            }
        }

        if (targetType == BattleCardTargetType.Tile &&
            owner.TryRaycastMapTile(pointerPosition, out MapInfo tile) &&
            battleCardActionController.TrySelectTargetAndEnterConfirmation(tile.gameObject, tile))
        {
            return;
        }

        Debug.Log("카드 사거리 안의 올바른 대상을 선택해야 합니다.", this);
    }

    private void HandleTargetSelectionRequested(string message)
    {
        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(message);
    }

    private void HandleConfirmed(BattleActionResult result)
    {
        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);
        FindFirstObjectByType<BattleCardPanelToggle>()?.Hide();

        BattleUnitMP playerMP = owner.player != null ? owner.player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"카드 사용 확정: {result.Request.DisplayName}, 소모 {result.ActionMPCost}MP, " +
            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}.",
            this);
    }

    private void HandleCancelled()
    {
        owner.HideActionConfirmationUI();
    }
}
