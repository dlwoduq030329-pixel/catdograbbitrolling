using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BattlePlayerActionController에서 분리한 "카드 사용" 행동 플로우 전용 컴포넌트다.
/// 카드 사용 시작(BeginUseConfirmation), 유효 대상 판정(HasValidTarget), 우클릭 대상 선택
/// (HandleTargetRightClick), 확인 UI 요청/확정/취소를 전담하는 BattleCardActionController의
/// 이벤트를 받아 처리한다.
///
/// BattleUnitMoveFlow/BattleUnitAttackFlow와 마찬가지로 "Inspector에서 세팅하는 값"은 없고
/// (BattleCardActionController 자체가 GetOrAdd로 자동 부착되는 컴포넌트라 Scene에 저장된 실제
/// 데이터가 없었다), 순수하게 카드 사용 로직만 담당한다 — 용병단처럼 조작 가능한 유닛이 여러
/// 개가 되어도 유닛마다 이 컴포넌트 하나씩만 붙이면 카드 로직은 그대로 재사용 가능하도록 만든
/// 것이 분리 목적이다.
/// </summary>
public class BattleUnitCardFlow : MonoBehaviour
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
        EnsureBattleCardActionController();
    }

    private void OnDestroy()
    {
        if (battleCardActionController != null)
        {
            battleCardActionController.TargetSelectionRequested -= HandleTargetSelectionRequested;
            battleCardActionController.ConfirmationRequested -= HandleConfirmationRequested;
            battleCardActionController.Confirmed -= HandleConfirmed;
            battleCardActionController.Cancelled -= HandleCancelled;
            battleCardActionController.RangeVisibilityChanged -= owner.SetRangeVisible;
        }
    }

    /// <summary>카드 대상 선택, 사거리 표시와 MP·손패 확정을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void EnsureBattleCardActionController()
    {
        owner.EnsureBattleRangeVisualizer();
        owner.EnsureBattlePushPreviewView();

        battleCardActionController = BattleComponentResolver.GetOrAdd(gameObject, battleCardActionController);
        battleCardActionController.Configure(
            owner.player,
            owner.battleRangeVisualizer,
            owner.colorPalette.CardRangeTileColor,
            owner.colorPalette.CardEffectAreaTileColor,
            owner.FindClosestMapTile,
            owner.mainCamera,
            owner.battlePushPreviewView);
        battleCardActionController.TargetSelectionRequested -= HandleTargetSelectionRequested;
        battleCardActionController.ConfirmationRequested -= HandleConfirmationRequested;
        battleCardActionController.Confirmed -= HandleConfirmed;
        battleCardActionController.Cancelled -= HandleCancelled;
        battleCardActionController.RangeVisibilityChanged -= owner.SetRangeVisible;
        battleCardActionController.TargetSelectionRequested += HandleTargetSelectionRequested;
        battleCardActionController.ConfirmationRequested += HandleConfirmationRequested;
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
        battleCardActionController?.Confirm();
    }

    /// <summary>대상 선택 또는 확인 대기 중인 카드 사용을 취소한다.</summary>
    public void Cancel()
    {
        battleCardActionController?.Cancel();
    }

    /// <summary>
    /// 이 카드가 실제로 유효한 대상(사거리 안의 살아있는 Enemy 등)을 갖고 있는지 판정한다.
    /// 공격형이 아니거나 명시적으로 Enemy를 겨냥하지 않는 카드는 대상 판정 없이 항상 사용 가능하다.
    /// </summary>
    public bool HasValidTarget(BattleCardData card)
    {
        if (card == null) return false;
        bool explicitlyTargetsEnemy = card.targetType == BattleCardTargetType.Enemy ||
                                      card.targetType == BattleCardTargetType.Character ||
                                      card.targetType == BattleCardTargetType.AllEnemies;
        if (card.category != BattleCardCategory.Attack && !explicitlyTargetsEnemy) return true;

        owner.ResolveBattleDataPool();
        GameObject currentPlayer = owner.player != null ? owner.player : owner.battleDataPool != null ? owner.battleDataPool.CurrentPlayer : null;
        MapInfo playerTile = currentPlayer != null ? owner.FindClosestMapTile(currentPlayer.transform.position) : null;
        if (playerTile == null) return false;

        IEnumerable<GameObject> registered = owner.battleDataPool != null && owner.battleDataPool.Units != null
            ? owner.battleDataPool.Units.Enemies : null;
        if (registered == null)
        {
            List<GameObject> fallback = new List<GameObject>();
            foreach (EnemyTurnActor enemy in FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None))
                if (enemy != null) fallback.Add(enemy.gameObject);
            registered = fallback;
        }

        int range = card.targetType == BattleCardTargetType.Self
            ? Mathf.Max(1, card.areaSizeTiles)
            : Mathf.Max(1, card.rangeTiles);
        foreach (GameObject enemy in registered)
        {
            if (enemy == null || !enemy.activeInHierarchy) continue;
            BattleHealth health = enemy.GetComponent<BattleHealth>();
            if (health != null && health.IsDead) continue;
            if (card.targetType == BattleCardTargetType.AllEnemies) return true;
            MapInfo enemyTile = owner.FindClosestMapTile(enemy.transform.position);
            int distance = BattleTileRangeCalculator.GetDistance(playerTile, enemyTile, range);
            if (distance >= 0 && distance <= range) return true;
        }
        return false;
    }

    /// <summary>카드 사용 확인 단계를 열고 공용 확인·취소 버튼을 표시한다.
    /// 다른 행동이 진행 중인지 같은 라우팅 판단은 owner(BattlePlayerActionController)의
    /// BeginCardUseConfirmation이 먼저 하고, 이 메서드는 "실제로 카드를 시작하는" 부분만 담당한다.</summary>
    public bool BeginUseConfirmation(PendingBattleCardUse cardUse, BattleCardDrawSystem cardDrawSystem)
    {
        if (!HasValidTarget(cardUse.CardData))
        {
            Debug.Log("Card use blocked: no valid enemy is within this card's range.", this);
            return false;
        }

        owner.moveFlow.ClearMoveRange();
        EnsureBattleCardActionController();
        bool canUseCards = BattleGameManager.Instance != null && BattleGameManager.Instance.CanUsePlayerCards;
        return battleCardActionController.Begin(cardUse, cardDrawSystem, canUseCards);
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
            if (battleCardActionController.TrySelectTarget(enemy.gameObject, enemyTile))
            {
                return;
            }
        }

        if (targetType == BattleCardTargetType.Tile &&
            owner.TryRaycastMapTile(pointerPosition, out MapInfo tile) &&
            battleCardActionController.TrySelectTarget(tile.gameObject, tile))
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

    private void HandleConfirmationRequested(string message) => owner.ShowActionConfirmationUI(message);

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
