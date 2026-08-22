# -*- coding: utf-8 -*-
import sys

path = "Assets/renew/Battle/Player/BattlePlayerActionController.cs"

with open(path, "rb") as f:
    raw = f.read()

content = raw.decode("utf-8")
content = content.replace("\r\n", "\n")

replacements = []

# R1: raycaster/visualizer/rangecontroller/mover fields -> internal
replacements.append((
'''    [InspectorName("전투 레이캐스트 모듈")]
    [SerializeField] private BattleRaycaster battleRaycaster;
    [InspectorName("전투 범위 표시 모듈")]
    [SerializeField] private BattleRangeVisualizer battleRangeVisualizer;
    [InspectorName("플레이어 범위 제어 모듈")]
    [SerializeField] private BattlePlayerRangeController battlePlayerRangeController;
    [InspectorName("플레이어 이동 실행 모듈")]
    [SerializeField] private BattlePlayerMover battlePlayerMover;''',
'''    [InspectorName("전투 레이캐스트 모듈")]
    [SerializeField] internal BattleRaycaster battleRaycaster;
    [InspectorName("전투 범위 표시 모듈")]
    [SerializeField] internal BattleRangeVisualizer battleRangeVisualizer;
    [InspectorName("플레이어 범위 제어 모듈")]
    [SerializeField] internal BattlePlayerRangeController battlePlayerRangeController;
    [InspectorName("플레이어 이동 실행 모듈")]
    [SerializeField] internal BattlePlayerMover battlePlayerMover;'''
))

# R2: battleDataPool -> internal + add moveFlow field
replacements.append((
'''    [InspectorName("전투 데이터 저장소")]
    [SerializeField] private BattleDataPool battleDataPool;''',
'''    [InspectorName("전투 데이터 저장소")]
    [SerializeField] internal BattleDataPool battleDataPool;
    [InspectorName("이동 플로우 모듈")]
    [SerializeField] private BattleUnitMoveFlow moveFlow;'''
))

# R3: mapcontext/turnstate/rangeToggle/isMoving block
replacements.append((
'''    private BattlePlayerMapContext battlePlayerMapContext;
    private readonly BattlePlayerTurnActionState turnActionState = new BattlePlayerTurnActionState();
    private bool rangeVisible;
    /// <summary>R 단축키로 범위 표시를 켠 상태인지 기억한다. 이동으로 범위가 잠깐 꺼져도 이동 후 자동으로 다시 켠다.</summary>
    private bool rangeToggleActive;
    private bool isMoving;
    private PlayerCombatData playerCombatData;''',
'''    internal BattlePlayerMapContext battlePlayerMapContext;
    internal readonly BattlePlayerTurnActionState turnActionState = new BattlePlayerTurnActionState();
    private bool rangeVisible;
    /// <summary>R 단축키로 범위 표시를 켠 상태인지 기억한다. 이동으로 범위가 잠깐 꺼져도 이동 후 자동으로 다시 켠다.</summary>
    internal bool rangeToggleActive;
    private PlayerCombatData playerCombatData;'''
))

# R4: IsAnyActionMoving property
replacements.append((
'''    private bool IsAnyActionMoving =>
        isMoving || (battleBasicAttackController != null && battleBasicAttackController.IsExecuting);''',
'''    private bool IsAnyActionMoving =>
        (moveFlow != null && moveFlow.IsMoving) ||
        (battleBasicAttackController != null && battleBasicAttackController.IsExecuting);'''
))

# R5: Awake body
replacements.append((
'''        EnsureBattlePlayerMover();
        EnsureBattleMoveTransaction();
        EnsureBattleBasicAttackController();
        EnsureBattlePushPreviewView();
        EnsureBattleCardActionController();
        EnsureBattleMovePreview();
        EnsureBattlePlayerInputReader();
        EnsureBattleUnitHoverHighlighter();
        EnsureBattleMoveThreatPreview();

        EnsureBattleActionConfirmView();''',
'''        EnsureBattlePlayerMover();
        EnsureMoveFlow();
        EnsureBattleBasicAttackController();
        EnsureBattlePushPreviewView();
        EnsureBattleCardActionController();
        EnsureBattlePlayerInputReader();
        EnsureBattleUnitHoverHighlighter();

        EnsureBattleActionConfirmView();'''
))

# R6: SetPlayer body
replacements.append((
'''        EnsureBattleUnitHoverHighlighter();
        EnsureBattlePlayerMover();
        EnsureBattleMoveTransaction();
        playerCombatData = player != null ? player.GetComponent<PlayerCombatData>() : null;''',
'''        EnsureBattleUnitHoverHighlighter();
        EnsureBattlePlayerMover();
        EnsureMoveFlow();
        playerCombatData = player != null ? player.GetComponent<PlayerCombatData>() : null;'''
))

# R7: ConfirmMove
replacements.append((
'''    public void ConfirmMove()
    {
        if (battleCardActionController != null && battleCardActionController.IsAwaitingConfirmation)
        {
            battleCardActionController.Confirm();
            return;
        }

        if (battleBasicAttackController != null && battleBasicAttackController.IsAwaitingConfirmation)
        {
            battleBasicAttackController.Confirm();
            return;
        }

        EnsureBattleMoveTransaction();
        if (!battleMoveTransaction.IsAwaitingConfirmation ||
            battleMoveTransaction.PendingTarget == null ||
            !turnActionState.DiceRolled || turnActionState.MovementUsed)
        {
            return;
        }

        SetMoveButtonGroupVisible(false);
        StartCoroutine(MovePlayerToSelectedTile());
    }''',
'''    public void ConfirmMove()
    {
        if (battleCardActionController != null && battleCardActionController.IsAwaitingConfirmation)
        {
            battleCardActionController.Confirm();
            return;
        }

        if (battleBasicAttackController != null && battleBasicAttackController.IsAwaitingConfirmation)
        {
            battleBasicAttackController.Confirm();
            return;
        }

        EnsureMoveFlow();
        moveFlow.ConfirmSelectedMove();
    }'''
))

# R8: ResetTurnMoveState
replacements.append((
'''        turnActionState.Reset();
        EnsureBattleMoveTransaction();
        battleMoveTransaction.ResetTurn();
        EnsureBattlePlayerRangeController();
        battlePlayerRangeController.ClearState();
        battleBasicAttackController?.ResetTurn();
        battleCardActionController?.ResetTurn();
        ClearMoveArrow();
        ClearMoveRange();

        SetConfirmButtonsInteractable(false);''',
'''        turnActionState.Reset();
        EnsureMoveFlow();
        moveFlow.ResetTurn();
        EnsureBattlePlayerRangeController();
        battlePlayerRangeController.ClearState();
        battleBasicAttackController?.ResetTurn();
        battleCardActionController?.ResetTurn();

        SetConfirmButtonsInteractable(false);'''
))

# R9a: HandleLeftClick - move confirm branch
replacements.append((
'''            if (battleMoveTransaction.IsAwaitingConfirmation)
            {
                Debug.Log($"플레이어 클릭으로 이동 확정: {battleMoveTransaction.PendingTarget?.name}", clickedPlayer);
                ConfirmMove();
                return;
            }''',
'''            if (moveFlow.IsAwaitingConfirmation)
            {
                Debug.Log($"플레이어 클릭으로 이동 확정: {moveFlow.PendingTarget?.name}", clickedPlayer);
                ConfirmMove();
                return;
            }'''
))

# R9b: HandleLeftClick - ShowMoveRange calls
replacements.append((
'''            if (rangeToggleActive)
            {
                // R 위협 범위를 유지하면서 이동 가능 집합도 함께 계산해 Player 이동 입력을 연다.
                ShowMoveRange();
                ShowEnemyThreatRange(true);
            }
            else
                ShowMoveRange();
            return;''',
'''            if (rangeToggleActive)
            {
                // R 위협 범위를 유지하면서 이동 가능 집합도 함께 계산해 Player 이동 입력을 연다.
                moveFlow.ShowMoveRange();
                ShowEnemyThreatRange(true);
            }
            else
                moveFlow.ShowMoveRange();
            return;'''
))

# R9c: HandleLeftClick - SelectMoveTile call
replacements.append((
'''                SelectMoveTile(clickedTile);
                return;''',
'''                moveFlow.SelectMoveTile(clickedTile);
                return;'''
))

# R10: HandleRangeToggleRequested guard
replacements.append((
'''        if (battleMoveTransaction.IsAwaitingConfirmation || IsBasicAttackActive)''',
'''        if (moveFlow.IsAwaitingConfirmation || IsBasicAttackActive)'''
))

# R11: HandleRightClick guard
replacements.append((
'''        if (!turnActionState.DiceRolled || !rangeVisible ||
            IsAnyActionMoving || battleMoveTransaction.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)''',
'''        if (!turnActionState.DiceRolled || !rangeVisible ||
            IsAnyActionMoving || moveFlow.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)'''
))

# R12a: BeginCardUseConfirmation guard
replacements.append((
'''        if (cardUse == null || cardDrawSystem == null || player == null ||
            IsAnyActionMoving || battleMoveTransaction.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)''',
'''        if (cardUse == null || cardDrawSystem == null || player == null ||
            IsAnyActionMoving || moveFlow.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)'''
))

# R12b: BeginCardUseConfirmation ClearMoveRange call
replacements.append((
'''        ClearMoveRange();
        EnsureBattleCardActionController();''',
'''        moveFlow.ClearMoveRange();
        EnsureBattleCardActionController();'''
))

# R13: CancelMoveSelection
replacements.append((
'''    public void CancelMoveSelection()
    {
        if (IsCardActionActive)
        {
            battleCardActionController.Cancel();
            return;
        }

        if (battleBasicAttackController != null && battleBasicAttackController.IsAwaitingConfirmation)
        {
            battleBasicAttackController.Cancel();
            return;
        }

        EnsureBattleMoveTransaction();
        bool returnToMoveRange = battleMoveTransaction.CancelSelection();
        ClearMoveArrow();

        SetConfirmButtonsInteractable(false);
        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(string.Empty);

        if (returnToMoveRange)
        {
            ShowMoveRange();
        }
        else
        {
            rangeToggleActive = false;
            ClearMoveRange();
        }
    }''',
'''    public void CancelMoveSelection()
    {
        if (IsCardActionActive)
        {
            battleCardActionController.Cancel();
            return;
        }

        if (battleBasicAttackController != null && battleBasicAttackController.IsAwaitingConfirmation)
        {
            battleBasicAttackController.Cancel();
            return;
        }

        EnsureMoveFlow();
        moveFlow.CancelSelectedMove();
    }'''
))

# R14: HandleBasicAttackConfirmed / HandleBasicAttackCancelled ShowMoveRange calls
replacements.append((
'''            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}. 피해 적용은 아직 연결하지 않았습니다.",
            this);
        ShowMoveRange();
    }

    private void HandleBasicAttackCancelled()
    {
        HideActionConfirmationUI();
        ShowMoveRange();
    }''',
'''            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}. 피해 적용은 아직 연결하지 않았습니다.",
            this);
        moveFlow.ShowMoveRange();
    }

    private void HandleBasicAttackCancelled()
    {
        HideActionConfirmationUI();
        moveFlow.ShowMoveRange();
    }'''
))

# R15: delete ShowMoveRange method, change ShowEnemyThreatRange to internal
replacements.append((
'''    /// <summary>
    /// 주사위 범위와 현재 MP 중 작은 값으로 BFS 범위를 계산하고 타일 색상을 표시한다.
    /// </summary>
    private void ShowMoveRange()
    {
        RefreshMapTiles();
        RestoreAllTileColors();
        EnsureBattlePlayerRangeController();
        battlePlayerRangeController.ClearState();
        ResolveBattleDataPool();
        battlePlayerRangeController.RefreshOccupiedEnemyTiles(
            battleDataPool,
            FindClosestMapTile);

        MapInfo currentTile = FindClosestMapTile(player != null ? player.transform.position : Vector3.zero);
        if (currentTile == null)
        {
            Debug.LogWarning("플레이어가 서 있는 맵 타일을 찾지 못했습니다.", this);
            SetRangeVisible(false);
            return;
        }

        BattleUnitMP playerMP = player != null ? player.GetComponent<BattleUnitMP>() : null;
        int mpLimitedRange = playerMP != null && !turnActionState.MovementUsed
            ? Mathf.Min(currentMoveRange, playerMP.CurrentMP)
            : 0;
        IEnumerable<GameObject> enemies = battleDataPool != null && battleDataPool.Units != null
            ? battleDataPool.Units.Enemies
            : null;
        bool shown = battlePlayerRangeController.BuildAndShow(
            battlePlayerMapContext.Tiles,
            currentTile,
            mpLimitedRange,
            GetPlayerAttackRange(),
            BattleMapTraversalService.IsWalkable,
            enemies,
            colorPalette.MovableTileColor,
            colorPalette.BlockedTileColor,
            colorPalette.AttackableTileColor,
            colorPalette.EnemyDetectColor);
        SetRangeVisible(shown);
    }

    /// <summary>
    /// R 단축키 전용 표시. Player 자신의 이동·공격 범위 대신
    /// 활성 Enemy들이 이번 턴에 실제로 위협할 수 있는 타일을 계산해 보여준다.
    /// </summary>
    private void ShowEnemyThreatRange(bool preserveMoveRange = false)''',
'''    /// <summary>
    /// R 단축키 전용 표시. Player 자신의 이동·공격 범위 대신
    /// 활성 Enemy들이 이번 턴에 실제로 위협할 수 있는 타일을 계산해 보여준다.
    /// </summary>
    internal void ShowEnemyThreatRange(bool preserveMoveRange = false)'''
))

# R16: SelectMoveTile deletion
replacements.append((
'''    /// <summary>이전 선택 색상을 복구한 뒤 새 목적지 강조와 화살표를 표시한다.
    /// 이동 확정은 별도 UI가 아니라 Player 본체 좌클릭으로 수행한다.</summary>
    private void SelectMoveTile(MapInfo targetTile)
    {
        // 다른 타일을 다시 선택할 때 이전 선택 강조를 먼저 제거한다.
        ShowMoveRange();
        EnsureBattleMoveTransaction();
        if (!battleMoveTransaction.SelectTarget(targetTile))
        {
            return;
        }

        // ShowSelectedTile은 BattleRangeVisualizer.SetBlendStrengths()로 미리 설정해둔
        // "선택 강조 강도(selectedColorBlend)"를 그대로 쓰므로 여기서 강도를 다시 넘길 필요가 없다.
        EnsureBattleRangeVisualizer();
        battleRangeVisualizer.ShowSelectedTile(targetTile, colorPalette.SelectedTileColor);
        ShowMoveArrow(targetTile);
        SetActionConfirmText(string.Empty);
        SetConfirmButtonsInteractable(false);
        SetMoveButtonGroupVisible(false);
    }

''',
''
))

# R17: MovePlayerToSelectedTile deletion
replacements.append((
'''    /// <summary>
    /// 목적지까지 최단 경로를 따라 이동하고 완료 후 경로 칸 수만큼 MP를 차감한다.
    /// 취소나 경로 실패에는 MP를 차감하지 않는다.
    /// </summary>
    private IEnumerator MovePlayerToSelectedTile()
    {
        EnsureBattleMoveTransaction();
        MapInfo targetTile = battleMoveTransaction.PendingTarget;
        if (player == null || targetTile == null || turnActionState.MovementUsed || !turnActionState.DiceRolled)
        {
            yield break;
        }

        // 목적지 선택 뒤 Enemy 등록 또는 위치가 바뀌었을 수 있으므로 실제 이동 직전에 다시 검증한다.
        ResolveBattleDataPool();
        EnsureBattlePlayerRangeController();
        battlePlayerRangeController.RefreshOccupiedEnemyTiles(
            battleDataPool,
            FindClosestMapTile);

        MapInfo startTile = FindClosestMapTile(player.transform.position);
        BattleUnitMP playerMP = player.GetComponent<BattleUnitMP>();
        int maxMovementTiles = playerMP != null
            ? Mathf.Min(currentMoveRange, playerMP.CurrentMP)
            : 0;
        isMoving = true;
        EnsureBattleMoveTransaction();
        BattleMovementResult movementResult = null;
        yield return battleMoveTransaction.TryExecutePendingMove(
            startTile,
            BattleMapTraversalService.IsWalkable,
            battlePlayerRangeController.OccupiedEnemyTiles,
            maxMovementTiles,
            result => movementResult = result);
        isMoving = false;

        if (movementResult == null || !movementResult.Success)
        {
            Debug.LogWarning(
                movementResult != null ? movementResult.FailureReason : "이동 결과를 받지 못했습니다.",
                this);
            CancelMoveSelection();
            yield break;
        }

        turnActionState.MarkMovementUsed();

        BattleGameManager.Instance?.ChestRewardSystem?.TryOpen(targetTile);
        BattleGameManager.Instance?.CardShopSystem?.TryEnter(targetTile);

        ClearMoveRange();
        ClearMoveArrow();

        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.ResetDiceOnMove();
        }

        SetConfirmButtonsInteractable(false);
        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(string.Empty);

        // R 단축키로 범위를 켠 상태였다면 이동으로 범위가 꺼진 뒤에도 자동으로 다시 켠다.
        if (rangeToggleActive)
        {
            ShowEnemyThreatRange();
        }

        // 최신 범위를 먼저 그린 다음 착지 강조를 올려야 강조 종료 시 현재 범위 색으로 복원된다.
        EnsureBattleRangeVisualizer();
        battleRangeVisualizer.ShowLandedTileForDuration(
            targetTile,
            colorPalette.LandedTileColor,
            landedHighlightDuration);
    }

''',
''
))

# R18: EnsureBattlePlayerMover modifier + insert EnsureMoveFlow + delete EnsureBattleMoveTransaction
replacements.append((
'''    /// <summary>Player 이동 연출 컴포넌트를 확보하고 현재 이동 속도 설정을 전달한다.</summary>
    private void EnsureBattlePlayerMover()
    {
        battlePlayerMover = BattleComponentResolver.GetOrAdd(gameObject, battlePlayerMover);
        battlePlayerMover.Configure(player, secondsPerTile, moveSpeedMultiplier);
    }

    /// <summary>일반 이동 경로 검증, 이동 연출과 MP 차감을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void EnsureBattleMoveTransaction()
    {
        EnsureBattlePlayerMover();

        battleMoveTransaction = BattleComponentResolver.GetOrAdd(gameObject, battleMoveTransaction);
        battleMoveTransaction.AttachPlayer(player, battlePlayerMover);
    }

''',
'''    /// <summary>Player 이동 연출 컴포넌트를 확보하고 현재 이동 속도 설정을 전달한다.</summary>
    internal void EnsureBattlePlayerMover()
    {
        battlePlayerMover = BattleComponentResolver.GetOrAdd(gameObject, battlePlayerMover);
        battlePlayerMover.Configure(player, secondsPerTile, moveSpeedMultiplier);
    }

    /// <summary>이동 플로우 전담 컴포넌트(BattleUnitMoveFlow)를 확보하고 소유자 참조를 연결한다.</summary>
    private void EnsureMoveFlow()
    {
        moveFlow = BattleComponentResolver.GetOrAdd(gameObject, moveFlow);
        moveFlow.Attach(this);
    }

'''
))

# R19: EnsureBattleMovePreview deletion
replacements.append((
'''    /// <summary>이동 목적지 화살표 표시 컴포넌트를 확보하고 프리팹 설정을 전달한다.</summary>
    private void EnsureBattleMovePreview()
    {
        battleMovePreview = BattleComponentResolver.GetOrAdd(gameObject, battleMovePreview);
        battleMovePreview.SetArrowPrefab(moveArrowPrefab, arrowOffset);
    }

''',
''
))

# R20: EnsureBattleMoveThreatPreview deletion
replacements.append((
'''    private void EnsureBattleMoveThreatPreview()
    {
        battleMoveThreatPreview = BattleComponentResolver.GetOrAdd(
            gameObject,
            battleMoveThreatPreview);
        battleMoveThreatPreview.Configure(
            mainCamera,
            battleRaycaster,
            battlePlayerRangeController);
    }

''',
''
))

# R21: modifier changes - simple signature swaps
simple_modifiers = [
    ("    private void EnsureBattleRaycaster()", "    internal void EnsureBattleRaycaster()"),
    ("    private void EnsureBattleRangeVisualizer()", "    internal void EnsureBattleRangeVisualizer()"),
    ("    private void EnsureBattlePlayerRangeController()", "    internal void EnsureBattlePlayerRangeController()"),
    ("    private void SetMoveButtonGroupVisible(bool visible)", "    internal void SetMoveButtonGroupVisible(bool visible)"),
    ("    private void SetConfirmButtonsInteractable(bool interactable)", "    internal void SetConfirmButtonsInteractable(bool interactable)"),
    ("    private void RefreshMapTiles()", "    internal void RefreshMapTiles()"),
    ("    private void ResolveBattleDataPool()", "    internal void ResolveBattleDataPool()"),
    ("    private void RestoreAllTileColors()", "    internal void RestoreAllTileColors()"),
    ("    private void SetRangeVisible(bool visible)", "    internal void SetRangeVisible(bool visible)"),
    ("    private MapInfo FindClosestMapTile(Vector3 worldPosition)", "    internal MapInfo FindClosestMapTile(Vector3 worldPosition)"),
    ("    private int GetPlayerAttackRange()", "    internal int GetPlayerAttackRange()"),
    ("    private void SetActionConfirmText(string message)", "    internal void SetActionConfirmText(string message)"),
]
for old, new in simple_modifiers:
    replacements.append((old, new))

# R22: ClearMoveRange deletion
replacements.append((
'''    /// <summary>타일 색상, 도달 가능 집합, 전역 범위 표시 상태를 초기화한다.</summary>
    private void ClearMoveRange()
    {
        RestoreAllTileColors();
        EnsureBattlePlayerRangeController();
        battlePlayerRangeController.ClearState();
        SetRangeVisible(false);
    }

''',
''
))

# R23: ShowMoveArrow + ClearMoveArrow deletion (end of file, keep closing brace)
replacements.append((
'''    /// <summary>목적지 위에 화살표 프리팹을 생성 또는 재사용해 표시한다.</summary>
    private void ShowMoveArrow(MapInfo targetTile)
    {
        EnsureBattleMovePreview();
        battleMovePreview.Show(targetTile);
        EnsureBattleMoveThreatPreview();
        battleMoveThreatPreview.ShowSelectedDestination(targetTile);
    }

    /// <summary>화살표 인스턴스를 파괴하지 않고 비활성화해 재사용한다.</summary>
    private void ClearMoveArrow()
    {
        EnsureBattleMovePreview();
        battleMovePreview.Hide();
        EnsureBattleMoveThreatPreview();
        battleMoveThreatPreview.ClearSelectedDestination();
    }
}''',
'''}'''
))

for i, (old, new) in enumerate(replacements, start=1):
    count = content.count(old)
    assert count == 1, (i, count, old[:80])
    content = content.replace(old, new, 1)

content = content.replace("\n", "\r\n")

with open(path, "wb") as f:
    f.write(content.encode("utf-8"))

print("OK, total replacements:", len(replacements))
