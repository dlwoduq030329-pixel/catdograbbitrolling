using System.Collections;
using UnityEngine;

/// <summary>
/// BattlePlayerActionController에서 분리한 "이동" 행동 플로우 전용 컴포넌트다.
/// 이동 범위 표시(ShowMoveRange), 목적지 선택(SelectMoveTile), 실제 이동 실행
/// (MovePlayerToSelectedTile), 이동 확정/취소(ConfirmSelectedMove/CancelSelectedMove),
/// 턴 초기화(ResetTurn)를 전담한다.
///
/// 이동 범위와 이동 속도처럼 Player 행동 전체에 필요한 값은 BattlePlayerActionController에서 읽는다.
/// 목적지 화살표 Prefab과 위치 보정값은 표시 책임을 가진 BattleMovePreview가 직접 관리한다.
/// 용병단처럼 조작 가능한 유닛이 여러 개가 되어도 유닛마다 이 컴포넌트 하나씩만 붙이면 이동 로직을
/// 재사용할 수 있도록 만든 것이 분리 목적이다.
/// </summary>
public class BattleUnitMoveFlow : MonoBehaviour
{
    private BattlePlayerActionController owner;
    private GameObject boundPlayer;

    [SerializeField] private BattlePlayerMoveTransaction battleMoveTransaction;
    [SerializeField] private BattleMovePreview battleMovePreview;
    [SerializeField] private BattleMoveThreatPreview battleMoveThreatPreview;

    [Header("이동 확정 입력")]
    [Tooltip("같은 이동 타일을 두 번 좌클릭했을 때 더블 클릭으로 인정할 최대 간격(초).")]
    [SerializeField, Min(0.1f)] private float moveDoubleClickInterval = 0.4f;

    private bool isMoving;
    private MapInfo lastClickedMoveTile;
    private float lastMoveTileClickTime = float.NegativeInfinity;

    /// <summary>이동 실행 코루틴이 진행 중인지(다른 행동을 막아야 하는지) 여부.</summary>
    public bool IsMoving => isMoving;

    /// <summary>목적지 선택 후 확정 대기 중인지 여부.</summary>
    public bool IsAwaitingConfirmation =>
        battleMoveTransaction != null && battleMoveTransaction.IsAwaitingConfirmation;

    /// <summary>현재 확정 대기 중인 목적지 타일(없으면 null).</summary>
    public MapInfo PendingTarget =>
        battleMoveTransaction != null ? battleMoveTransaction.PendingTarget : null;

    /// <summary>
    /// 소유자(BattlePlayerActionController)를 최초 1회 연결한다.
    /// 같은 소유자가 반복 전달되면 전체 초기화를 다시 하지 않고,
    /// 실제 Player가 교체된 경우에만 Player 의존성을 갱신한다.
    /// </summary>
    public void Attach(BattlePlayerActionController controller)
    {
        if (controller == null)
        {
            Debug.LogError("BattleUnitMoveFlow에 연결할 BattlePlayerActionController가 없습니다.", this);
            return;
        }

        bool isFirstOwnerBinding = owner != controller;
        bool hasPlayerChanged = boundPlayer != controller.player;

        if (!isFirstOwnerBinding && !hasPlayerChanged)
        {
            return;
        }

        owner = controller;
        EnsureBattleMoveTransaction();
        boundPlayer = owner.player;

        if (isFirstOwnerBinding)
        {
            TryResolveBattleMovePreview();
            EnsureBattleMoveThreatPreview();
        }
    }

    /// <summary>일반 이동 경로 검증, 이동 연출과 MP 차감을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void EnsureBattleMoveTransaction()
    {
        owner.EnsureBattlePlayerMover();

        battleMoveTransaction = BattleComponentResolver.GetOrAdd(gameObject, battleMoveTransaction);
        battleMoveTransaction.AttachPlayer(owner.player, owner.battlePlayerMover);
    }

    /// <summary>
    /// 같은 GameObject에 Scene 컴포넌트로 연결된 이동 목적지 표시 모듈을 가져온다.
    /// 누락을 숨기기 위해 런타임에 새 컴포넌트를 만들지 않는다.
    /// </summary>
    private bool TryResolveBattleMovePreview()
    {
        if (battleMovePreview == null)
        {
            battleMovePreview = GetComponent<BattleMovePreview>();
        }

        if (battleMovePreview != null)
        {
            return true;
        }

        Debug.LogError(
            "BattleUnitMoveFlow와 같은 GameObject에 BattleMovePreview 컴포넌트가 없습니다.",
            this);
        return false;
    }

    private void EnsureBattleMoveThreatPreview()
    {
        owner.EnsureBattleRaycaster();
        owner.EnsureBattlePlayerRangeController();
        owner.ResolveBattleDataPool();
        battleMoveThreatPreview = BattleComponentResolver.GetOrAdd(gameObject, battleMoveThreatPreview);
        battleMoveThreatPreview.ConfigureDependencies(
            owner.mainCamera,
            owner.battleRaycaster,
            owner.battlePlayerRangeController,
            owner.battleDataPool != null ? owner.battleDataPool.Units : null);
    }

    /// <summary>새 Player 턴에 이동 관련 상태(대기 중이던 목적지, 화살표, 범위 표시)를 초기화한다.</summary>
    public void ResetTurn()
    {
        EnsureBattleMoveTransaction();
        battleMoveTransaction.ResetTurn();
        ClearMoveArrow();
        ClearMoveRange();
        ResetMoveDoubleClickState();
    }

    /// <summary>
    /// 이동 가능한 타일의 좌클릭을 처리한다.
    /// 첫 클릭은 목적지 선택과 Preview만 수행하고, 같은 타일을 제한 시간 안에 다시 클릭했을 때만
    /// 실제 이동을 확정한다. 다른 타일을 클릭하거나 시간이 초과되면 그 클릭을 새 첫 클릭으로 취급한다.
    /// </summary>
    public void HandleReachableTileLeftClick(MapInfo clickedTile)
    {
        if (clickedTile == null)
        {
            ResetMoveDoubleClickState();
            return;
        }

        bool clickedSameSelectedTileAgain =
            battleMoveTransaction != null &&
            battleMoveTransaction.IsAwaitingConfirmation &&
            battleMoveTransaction.PendingTarget == clickedTile &&
            lastClickedMoveTile == clickedTile &&
            Time.unscaledTime - lastMoveTileClickTime <= moveDoubleClickInterval;

        if (clickedSameSelectedTileAgain)
        {
            ResetMoveDoubleClickState();
            ConfirmSelectedMove();
            return;
        }

        SelectMoveTile(clickedTile);
        lastClickedMoveTile = clickedTile;
        lastMoveTileClickTime = Time.unscaledTime;
    }

    /// <summary>
    /// 주사위 범위와 현재 MP 중 작은 값으로 BFS 범위를 계산하고 타일 색상을 표시한다.
    /// </summary>
    public void ShowMoveRange()
    {
        owner.RefreshMapTiles();
        owner.RestoreAllTileColors();
        owner.EnsureBattlePlayerRangeController();
        owner.battlePlayerRangeController.ClearState();
        owner.ResolveBattleDataPool();
        owner.battlePlayerRangeController.RefreshOccupiedEnemyTiles(
            owner.battleDataPool,
            owner.FindClosestMapTile);

        MapInfo currentTile = owner.FindClosestMapTile(
            owner.player != null ? owner.player.transform.position : Vector3.zero);
        if (currentTile == null)
        {
            Debug.LogWarning("플레이어가 서 있는 맵 타일을 찾지 못했습니다.", this);
            owner.SetRangeVisible(false);
            return;
        }

        BattleUnitMP playerMP = owner.player != null ? owner.player.GetComponent<BattleUnitMP>() : null;
        int mpLimitedRange = playerMP != null && !owner.turnActionState.MovementUsed
            ? Mathf.Min(owner.currentMoveRange, playerMP.CurrentMP)
            : 0;
        System.Collections.Generic.IEnumerable<GameObject> enemies =
            owner.battleDataPool != null && owner.battleDataPool.Units != null
                ? owner.battleDataPool.Units.Enemies
                : null;
        bool shown = owner.battlePlayerRangeController.BuildAndShow(
            owner.battlePlayerMapContext.Tiles,
            currentTile,
            mpLimitedRange,
            owner.GetPlayerAttackRange(),
            BattleMapTraversalService.IsWalkable,
            enemies,
            owner.colorPalette.MovableTileColor,
            owner.colorPalette.BlockedTileColor,
            owner.colorPalette.AttackableTileColor,
            owner.colorPalette.EnemyDetectColor);
        owner.SetRangeVisible(shown);
    }

    /// <summary>이전 선택 색상을 복구한 뒤 새 목적지 강조와 화살표를 표시한다.
    /// 이동 확정은 같은 타일을 제한 시간 안에 다시 좌클릭했을 때 수행한다.</summary>
    public void SelectMoveTile(MapInfo targetTile)
    {
        ShowMoveRange();
        EnsureBattleMoveTransaction();
        if (!battleMoveTransaction.SelectTarget(targetTile))
        {
            return;
        }

        owner.EnsureBattleRangeVisualizer();
        owner.battleRangeVisualizer.ShowSelectedTile(targetTile, owner.colorPalette.SelectedTileColor);
        ShowMoveArrow(targetTile);
        owner.SetActionConfirmText(string.Empty);
        owner.SetConfirmButtonsInteractable(false);
        owner.SetMoveButtonGroupVisible(false);
    }

    /// <summary>확정 대기 중인 목적지가 유효하면 이동 실행 코루틴을 시작한다.</summary>
    public void ConfirmSelectedMove()
    {
        EnsureBattleMoveTransaction();
        if (!battleMoveTransaction.IsAwaitingConfirmation ||
            battleMoveTransaction.PendingTarget == null ||
            !owner.turnActionState.DiceRolled || owner.turnActionState.MovementUsed)
        {
            return;
        }

        owner.SetMoveButtonGroupVisible(false);
        StartCoroutine(MovePlayerToSelectedTile());
    }

    /// <summary>
    /// 목적지까지 최단 경로를 따라 이동하고 완료 후 경로 칸 수만큼 MP를 차감한다.
    /// 취소나 경로 실패에는 MP를 차감하지 않는다.
    /// </summary>
    private IEnumerator MovePlayerToSelectedTile()
    {
        EnsureBattleMoveTransaction();
        MapInfo targetTile = battleMoveTransaction.PendingTarget;
        if (owner.player == null || targetTile == null ||
            owner.turnActionState.MovementUsed || !owner.turnActionState.DiceRolled)
        {
            yield break;
        }

        // 목적지 선택 뒤 Enemy 등록 또는 위치가 바뀌었을 수 있으므로 실제 이동 직전에 다시 검증한다.
        owner.ResolveBattleDataPool();
        owner.EnsureBattlePlayerRangeController();
        owner.battlePlayerRangeController.RefreshOccupiedEnemyTiles(
            owner.battleDataPool,
            owner.FindClosestMapTile);

        MapInfo startTile = owner.FindClosestMapTile(owner.player.transform.position);
        BattleUnitMP playerMP = owner.player.GetComponent<BattleUnitMP>();
        int maxMovementTiles = playerMP != null
            ? Mathf.Min(owner.currentMoveRange, playerMP.CurrentMP)
            : 0;
        isMoving = true;
        EnsureBattleMoveTransaction();
        BattleMovementResult movementResult = null;
        yield return battleMoveTransaction.TryExecutePendingMove(
            startTile,
            BattleMapTraversalService.IsWalkable,
            owner.battlePlayerRangeController.OccupiedEnemyTiles,
            maxMovementTiles,
            result => movementResult = result);
        isMoving = false;

        if (movementResult == null || !movementResult.Success)
        {
            Debug.LogWarning(
                movementResult != null ? movementResult.FailureReason : "이동 결과를 받지 못했습니다.",
                this);
            owner.CancelCurrentPlayerAction();
            yield break;
        }

        owner.turnActionState.MarkMovementUsed();

        BattleGameManager.Instance?.ChestRewardSystem?.TryOpen(targetTile);
        BattleGameManager.Instance?.CardShopSystem?.TryEnter(targetTile);

        ClearMoveRange();
        ClearMoveArrow();

        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.ResetDiceOnMove();
        }

        owner.SetConfirmButtonsInteractable(false);
        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);

        // R 단축키로 범위를 켠 상태였다면 이동으로 범위가 꺼진 뒤에도 자동으로 다시 켠다.
        if (owner.rangeToggleActive)
        {
            owner.ShowEnemyThreatRange();
        }

        // 최신 범위를 먼저 그린 다음 착지 강조를 올려야 강조 종료 시 현재 범위 색으로 복원된다.
        owner.EnsureBattleRangeVisualizer();
        owner.battleRangeVisualizer.ShowLandedTileForDuration(
            targetTile,
            owner.colorPalette.LandedTileColor,
            owner.landedHighlightDuration);
    }

    /// <summary>
    /// 확정 대기 단계에서는 이동 범위 단계로 돌아가고, 범위 단계에서는 표시를 완전히 닫는다.
    /// </summary>
    public void CancelSelectedMove()
    {
        EnsureBattleMoveTransaction();
        bool returnToMoveRange = battleMoveTransaction.CancelSelection();
        ClearMoveArrow();
        ResetMoveDoubleClickState();

        owner.SetConfirmButtonsInteractable(false);
        owner.SetMoveButtonGroupVisible(false);
        owner.SetActionConfirmText(string.Empty);

        if (returnToMoveRange)
        {
            ShowMoveRange();
        }
        else
        {
            owner.rangeToggleActive = false;
            ClearMoveRange();
        }
    }


    /// <summary>이전 타일 클릭이 다음 클릭과 잘못 묶이지 않도록 더블 클릭 판정 상태를 지운다.</summary>
    private void ResetMoveDoubleClickState()
    {
        lastClickedMoveTile = null;
        lastMoveTileClickTime = float.NegativeInfinity;
    }

    /// <summary>타일 색상, 도달 가능 집합, 전역 범위 표시 상태를 초기화한다.</summary>
    public void ClearMoveRange()
    {
        owner.RestoreAllTileColors();
        owner.EnsureBattlePlayerRangeController();
        owner.battlePlayerRangeController.ClearState();
        owner.SetRangeVisible(false);
    }

    /// <summary>목적지 위에 화살표 프리팹을 생성 또는 재사용해 표시한다.</summary>
    public void ShowMoveArrow(MapInfo targetTile)
    {
        if (TryResolveBattleMovePreview())
        {
            battleMovePreview.Show(targetTile);
        }

        EnsureBattleMoveThreatPreview();
        battleMoveThreatPreview.ShowSelectedDestination(targetTile);
    }

    /// <summary>화살표 인스턴스를 파괴하지 않고 비활성화해 재사용한다.</summary>
    public void ClearMoveArrow()
    {
        if (TryResolveBattleMovePreview())
        {
            battleMovePreview.Hide();
        }

        EnsureBattleMoveThreatPreview();
        battleMoveThreatPreview.ClearSelectedDestination();
    }
}
