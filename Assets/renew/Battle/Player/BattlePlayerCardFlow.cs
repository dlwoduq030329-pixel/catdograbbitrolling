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
    private BattlePlayerActionController playerController;

    [SerializeField] private BattleCardActionController battleCardActionController;

    /// <summary>대상 선택 단계인지 여부.</summary>
    public bool IsSelectingTarget =>
        battleCardActionController != null && battleCardActionController.IsSelectingTarget;

    /// <summary>확인 대기 단계인지 여부.</summary>
    public bool IsAwaitingConfirmation =>
        battleCardActionController != null && battleCardActionController.IsAwaitingConfirmation;

    /// <summary>대상 선택, 확인 대기, 주사위 판정 연출 중 다른 행동을 막아야 하는지 여부.</summary>
    public bool IsActive =>
        battleCardActionController != null && battleCardActionController.IsActionActive;

    /// <summary>Player 입력 Controller와 카드 사용 흐름을 연결합니다.</summary>
    public void Attach(BattlePlayerActionController controller)
    {
        DisconnectCardActionEvents();
        playerController = controller;
        SetupCardController();
        ConnectCardEvents();
    }

    /// <summary>파괴된 객체로 카드 이벤트가 전달되지 않도록 연결을 해제합니다.</summary>
    private void OnDestroy()
    {
        DisconnectCardActionEvents();
    }

    /// <summary>카드 사용에 필요한 Player, 맵, UI 참조를 준비합니다.</summary>
    private void SetupCardController()
    {
        // 사거리 색상과 Push 결과를 표시할 View가 준비되도록 Player Controller에 요청한다.
        playerController.EnsureBattleRangeVisualizer();
        playerController.EnsureBattlePushPreviewView();
        if (playerController.battlePushPreviewView == null)
        {
            return;
        }
        // Push View가 월드 위치를 화면 좌표로 바꾸므로 전투 Camera도 View에 직접 연결한다.
        playerController.battlePushPreviewView.ConfigurePreviewDependencies(playerController.mainCamera);

        // Battle Player Action Controller 오브젝트에 미리 붙어 있어야 한다. 없으면 자동으로 붙이지 않고
        // 바로 알 수 있게 멈춘다.
        if (battleCardActionController == null)
        {
            battleCardActionController = GetComponent<BattleCardActionController>();
            if (battleCardActionController == null)
            {
                Debug.LogError($"[{nameof(BattlePlayerCardFlow)}] {gameObject.name}에 BattleCardActionController가 없습니다. Scene에 미리 추가해야 합니다.", this);
                return;
            }
        }
        // BattlePlayerActionController의 초기화 순서상 EnsureBattlePlayerMapContext가 EnsureCardFlow보다
        // 먼저 실행돼 지금은 항상 채워져 있지만, 이 클래스에서 그 private 메서드를 직접 호출할 수는
        // 없어 순서가 바뀌면 조용히 깨질 수 있다. 방어적으로 한 번 더 확인한다.
        if (playerController.battlePlayerMapContext == null)
        {
            Debug.LogError($"[{nameof(BattlePlayerCardFlow)}] {gameObject.name}: BattlePlayerMapContext가 아직 준비되지 않았습니다.", this);
            return;
        }

        // 카드 Controller가 자체적으로 Scene을 다시 검색하지 않도록 Player 쪽에서 이미 알고 있는 참조를 전달한다.
        battleCardActionController.Setup(
            playerController.player,
            playerController.battleRangeVisualizer,
            playerController.colorPalette.CardRangeTileColor,
            playerController.colorPalette.CardEffectAreaTileColor,
            playerController.FindClosestMapTile,
            playerController.battlePlayerMapContext.Tiles,
            playerController.battlePushPreviewView,
            BattleGameManager.Instance != null ? BattleGameManager.Instance.DiceSystem : null);

    }

    /// <summary>카드 상태 변경을 Player 입력과 UI에 연결합니다.</summary>
    private void ConnectCardEvents()
    {
        battleCardActionController.TargetSelectionRequested += HandleTargetSelectionRequested;
        battleCardActionController.ConfirmationRequested += HandleConfirmationRequested;
        battleCardActionController.Confirmed += HandleConfirmed;
        battleCardActionController.Cancelled += HandleCancelled;
        battleCardActionController.RangeVisibilityChanged += playerController.SetRangeVisible;
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
        battleCardActionController?.TryConfirmUse();
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
    /// → BattleCardActionController.TryStartCardUse().
    /// 이동 범위를 닫고 현재 턴의 카드 사용 가능 상태를 전달할 뿐, 카드 효과나 MP는 여기서 소비하지 않는다.
    /// </summary>
    public bool TryStartSelectedCardUse(SelectedCardUseInfo cardUse, BattleCardDrawSystem cardDrawSystem)
    {
        // 이동 범위와 카드 사거리가 같은 타일에 동시에 표시되지 않도록 카드 흐름 진입 전에 이동 Preview를 닫는다.
        playerController.moveFlow.ClearMoveRange();
        // 전투 Manager가 Player 턴·주사위·Overlay 상태를 종합해 현재 카드 입력 허용 여부를 제공한다.
        bool canUseCards = BattleGameManager.Instance != null && BattleGameManager.Instance.CanUsePlayerCards;
        // 여기서는 선택된 손패 정보와 DrawSystem을 전달할 뿐 MP 차감이나 카드 소비는 아직 발생하지 않는다.
        return battleCardActionController != null &&
               battleCardActionController.TryStartCardUse(cardUse, cardDrawSystem, canUseCards);
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
        if (playerController.IsAnyActionMoving)
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
            playerController.TryRaycastEnemy(pointerPosition, out EnemyTurnActor enemy))
        {
            // 효과 Pipeline에는 대상 GameObject뿐 아니라 사거리와 효과 중심 계산에 사용할 타일도 함께 필요하다.
            MapInfo enemyTile = playerController.FindClosestMapTile(enemy.transform.position);
            if (battleCardActionController.TrySelectTarget(enemy.gameObject, enemyTile))
            {
                return;
            }
        }

        if (targetType == BattleCardTargetType.Tile &&
            playerController.TryRaycastMapTile(pointerPosition, out MapInfo tile) &&
            battleCardActionController.TrySelectTarget(tile.gameObject, tile))
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
        playerController.SetMoveButtonGroupVisible(false);
        playerController.SetActionConfirmText(message);
    }

    /// <summary>
    /// 카드 대상 선택이 끝나 확인 대기 상태로 들어갔음을 BattleCardActionController가 알리면,
    /// 기본 공격과 같은 방식으로 확인(사용) 버튼을 다시 띄운다. 이 이벤트가 연결되기 전에는
    /// 대상을 고른 뒤 확정할 방법이 UI에 전혀 없었다.
    /// </summary>
    private void HandleConfirmationRequested(string message) => playerController.ShowActionConfirmationUI(message);

    /// <summary>
    /// 카드 효과와 자원 소비가 모두 성공한 뒤 호출된다. 확인 안내를 비우고 카드 패널을 숨긴 다음,
    /// 결과에 기록된 카드 이름·소모 MP와 현재 Player MP를 QA용 Console Log로 남긴다.
    /// 피해·회복·이동 효과는 이 Handler 전에 이미 실행됐으며 여기서는 결과 UI만 정리한다.
    /// </summary>
    private void HandleConfirmed(BattleActionResult result)
    {
        playerController.SetMoveButtonGroupVisible(false);
        playerController.SetActionConfirmText(string.Empty);

        // 현재 MP 조회는 성공 결과 Log 출력용이며 MP 차감 계산 자체는 카드 Controller가 담당한다.
        BattleUnitMP playerMP = playerController.player != null ? playerController.player.GetComponent<BattleUnitMP>() : null;
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
        playerController.HideActionConfirmationUI();
    }

    /// <summary>중복 호출과 파괴에 대비해 카드 이벤트 연결을 해제합니다.</summary>
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
        if (playerController != null)
        {
            battleCardActionController.RangeVisibilityChanged -= playerController.SetRangeVisible;
        }
    }
}
