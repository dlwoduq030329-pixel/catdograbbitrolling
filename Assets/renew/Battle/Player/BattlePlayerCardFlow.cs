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

    /// <summary>
    /// Player의 전체 입력 흐름을 가진 BattlePlayerActionController를 이 카드 흐름의 소유자로 연결한다.
    /// BattlePlayerActionController.EnsureCardFlow()는 초기 생성과 Player 등록 갱신 시 호출된다.
    /// Player가 교체될 때도 안전하도록 이전 이벤트 연결을 먼저 해제한 뒤 같은 소유자로 다시 구성한다.
    /// 카드 클릭 경로에서는 Attach를 다시 호출하지 않고 이미 연결된 이 흐름을 그대로 사용한다.
    /// </summary>
    public void Attach(BattlePlayerActionController controller)
    {
        DisconnectCardActionEvents();
        owner = controller;
        InitializeCardActionController();
    }

    /// <summary>
    /// Player 또는 Scene이 파괴될 때 BattleCardActionController에 등록했던 모든 이벤트를 해제한다.
    /// 구독을 남기면 파괴된 BattlePlayerCardFlow의 Handler가 이후 카드 이벤트에서 다시 호출될 수 있다.
    /// 실제 카드 상태를 초기화하거나 효과를 취소하는 함수는 아니며 이벤트 연결만 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        DisconnectCardActionEvents();
    }

    /// <summary>
    /// 카드 행동에 필요한 표현 컴포넌트를 준비하고 BattleCardActionController에 Player 환경을 전달한다.
    /// 이 함수가 직접 사거리·MP·효과를 계산하지는 않는다. Player, 범위 표시기, 색상, 타일 검색 함수,
    /// 등록된 맵 타일과 Push Preview View를 한 번 모아 실제 카드 규칙 Controller에 연결한다.
    /// 마지막 이벤트 연결은 카드 Controller의 상태 변화를 Player 입력 UI로 되돌려 보내는 통로다.
    /// </summary>
    private void InitializeCardActionController()
    {
        // 사거리 색상과 Push 결과를 표시할 View가 준비되도록 Player Controller에 요청한다.
        owner.EnsureBattleRangeVisualizer();
        owner.EnsureBattlePushPreviewView();
        // Push View가 월드 위치를 화면 좌표로 바꾸므로 전투 Camera도 View에 직접 연결한다.
        owner.battlePushPreviewView.ConfigurePreviewDependencies(owner.mainCamera);

        // 현재는 이전 Scene 호환을 위해 없으면 같은 Player Object에 자동 추가한다.
        // Player Prefab 직접 참조가 확정되면 GetOrAdd 경로를 제거할 정적 리뷰 대상이다.
        battleCardActionController = BattleComponentResolver.GetOrAdd(gameObject, battleCardActionController);
        // 카드 Controller가 자체적으로 Scene을 다시 검색하지 않도록 Player 쪽에서 이미 알고 있는 참조를 전달한다.
        battleCardActionController.Configure(
            owner.player,
            owner.battleRangeVisualizer,
            owner.colorPalette.CardRangeTileColor,
            owner.colorPalette.CardEffectAreaTileColor,
            owner.FindClosestMapTile,
            owner.battlePlayerMapContext.Tiles,
            owner.battlePushPreviewView);

        // 대상 선택 안내·사용 성공·취소·사거리 표시 상태를 Player UI와 입력 흐름에 반영한다.
        battleCardActionController.TargetSelectionRequested += HandleTargetSelectionRequested;
        battleCardActionController.ConfirmationRequested += HandleConfirmationRequested;
        battleCardActionController.Confirmed += HandleConfirmed;
        battleCardActionController.Cancelled += HandleCancelled;
        battleCardActionController.RangeVisibilityChanged += owner.SetRangeVisible;
    }

    /// <summary>
    /// 새 Player 턴이 시작될 때 이전 턴에 남은 선택 대상, 확인 대기와 카드 사거리 Preview를 초기화한다.
    /// BattlePlayerActionController.ResetPlayerTurnActions()가 이동·공격 상태와 함께 이 함수를 호출한다.
    /// 손패를 새로 뽑는 역할은 BattleCardDrawSystem이 담당하므로 여기서는 카드 선택 상태만 비운다.
    /// </summary>
    public void ResetTurn()
    {
        battleCardActionController?.ResetTurn();
    }

    /// <summary>
    /// 대상 선택을 마치고 확인 대기 중인 카드 사용을 최종 확정한다.
    /// 실제 유효성 재검사, MP 소비, 손패 제거와 효과 실행은 BattleCardActionController에서 수행한다.
    /// </summary>
    public void Confirm()
    {
        battleCardActionController?.TryConfirmCardUse();
    }

    /// <summary>
    /// 현재 진행 중인 카드 대상 선택 또는 사용 확인을 취소한다.
    /// BattleCardActionController가 저장된 카드·대상·타일과 Preview를 비우고 Cancelled 이벤트를 보낸다.
    /// </summary>
    public void Cancel()
    {
        battleCardActionController?.Cancel();
    }

    /// <summary>
    /// 손패 클릭으로 만들어진 카드 사용 요청을 실제 대상 선택 흐름에 전달한다.
    /// 호출 경로: BattleCardHandView.SelectCard()
    /// → BattlePlayerActionController.TryStartCardUseFromHand()
    /// → 이 함수
    /// → BattleCardActionController.TryBeginSelectedCardFlow().
    /// 이동 범위를 닫고 현재 턴의 카드 사용 가능 상태를 전달할 뿐, 카드 효과나 MP는 여기서 소비하지 않는다.
    /// </summary>
    public bool TryStartSelectedCardUse(SelectedCardUseInfo cardUse, BattleCardDrawSystem cardDrawSystem)
    {
        // 이동 범위와 카드 사거리가 같은 타일에 동시에 표시되지 않도록 카드 흐름 진입 전에 이동 Preview를 닫는다.
        owner.moveFlow.ClearMoveRange();
        // 전투 Manager가 Player 턴·주사위·Overlay 상태를 종합해 현재 카드 입력 허용 여부를 제공한다.
        bool canUseCards = BattleGameManager.Instance != null && BattleGameManager.Instance.CanUsePlayerCards;
        // 여기서는 선택된 손패 정보와 DrawSystem을 전달할 뿐 MP 차감이나 카드 소비는 아직 발생하지 않는다.
        return battleCardActionController != null &&
               battleCardActionController.TryBeginSelectedCardFlow(cardUse, cardDrawSystem, canUseCards);
    }

    /// <summary>
    /// BattlePlayerActionController.HandleLeftClick()이 카드 대상 선택 중(cardFlow.IsSelectingTarget)일 때
    /// 최우선으로 전달하는 화면 좌표로 카드 대상 Enemy 또는 Tile을 찾는다.
    /// 이동 연출 중이거나 Pointer가 UI 위에 있으면 월드 선택을 막는다. 카드 데이터의 TargetType에 따라
    /// Enemy Raycast와 Tile Raycast 중 필요한 것만 실행하고, 유효한 대상을 카드 Controller에 저장한다.
    /// 2026-09-05: 기본 공격 대상 지정과 함께 우클릭에서 좌클릭으로 통합했다(이름도 함께 변경).
    /// </summary>
    public void HandleTargetClick(Vector2 pointerPosition)
    {
        if (owner.IsAnyActionMoving)
        {
            // 이동 Coroutine 중 새 대상을 선택하면 Player 위치와 카드 사거리 기준점이 어긋날 수 있다.
            return;
        }

        if (BattlePlayerInputReader.IsPointerOverInteractiveUI(pointerPosition))
        {
            // 카드·상점·상태 UI Click이 뒤쪽 Enemy/Tile 선택으로 동시에 전달되는 것을 막는다.
            return;
        }

        BattleCardTargetType targetType = battleCardActionController.TargetType;
        if ((targetType == BattleCardTargetType.Enemy || targetType == BattleCardTargetType.Character) &&
            owner.TryRaycastEnemy(pointerPosition, out EnemyTurnActor enemy))
        {
            // 효과 Pipeline에는 대상 GameObject뿐 아니라 사거리와 효과 중심 계산에 사용할 타일도 함께 필요하다.
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

    /// <summary>
    /// BattleCardActionController가 수동 대상 선택을 시작했음을 알리면 이동 관련 Button을 숨기고
    /// 어떤 대상을 골라야 하는지 안내 문구를 Player 행동 UI에 전달한다.
    /// </summary>
    private void HandleTargetSelectionRequested(string message)
    {
        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(message);
    }

    /// <summary>
    /// 카드 대상 선택이 끝나 확인 대기 상태로 들어갔음을 BattleCardActionController가 알리면,
    /// 기본 공격과 같은 방식으로 확인(사용) 버튼을 다시 띄운다. 이 이벤트가 연결되기 전에는
    /// 대상을 고른 뒤 확정할 방법이 UI에 전혀 없었다.
    /// </summary>
    private void HandleConfirmationRequested(string message) => owner.ShowActionConfirmationUI(message);

    /// <summary>
    /// 카드 효과와 자원 소비가 모두 성공한 뒤 호출된다. 확인 안내를 비우고 카드 패널을 숨긴 다음,
    /// 결과에 기록된 카드 이름·소모 MP와 현재 Player MP를 QA용 Console Log로 남긴다.
    /// 피해·회복·이동 효과는 이 Handler 전에 이미 실행됐으며 여기서는 결과 UI만 정리한다.
    /// </summary>
    private void HandleConfirmed(BattleActionResult result)
    {
        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);
        // TODO(UI-CARD-01): 카드 패널 직접 참조가 준비되면 Scene 검색을 SerializeField 참조로 교체한다.
        FindFirstObjectByType<BattleCardPanelToggle>()?.Hide();

        // 현재 MP 조회는 성공 결과 Log 출력용이며 MP 차감 계산 자체는 카드 Controller가 담당한다.
        BattleUnitMP playerMP = owner.player != null ? owner.player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"카드 사용 확정: {result.Request.DisplayName}, 소모 {result.ActionMPCost}MP, " +
            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}.",
            this);
    }

    /// <summary>
    /// 카드 Controller가 취소를 완료한 뒤 Player 행동 확인 문구와 관련 UI를 닫는다.
    /// 카드 상태와 사거리 Preview 초기화는 BattleCardActionController.Cancel()에서 이미 처리된다.
    /// </summary>
    private void HandleCancelled()
    {
        owner.HideActionConfirmationUI();
    }

    /// <summary>
    /// 현재 BattleCardActionController에 연결된 이벤트를 안전하게 해제한다.
    /// Attach가 반복 호출될 때 중복 구독을 막고 OnDestroy에서도 같은 해제 순서를 재사용한다.
    /// owner가 아직 없으면 owner의 SetRangeVisible Handler만 건너뛴다.
    /// </summary>
    private void DisconnectCardActionEvents()
    {
        if (battleCardActionController == null)
        {
            return;
        }

        battleCardActionController.TargetSelectionRequested -= HandleTargetSelectionRequested;
        battleCardActionController.ConfirmationRequested -= HandleConfirmationRequested;
        battleCardActionController.Confirmed -= HandleConfirmed;
        battleCardActionController.Cancelled -= HandleCancelled;
        if (owner != null)
        {
            battleCardActionController.RangeVisibilityChanged -= owner.SetRangeVisible;
        }
    }
}
