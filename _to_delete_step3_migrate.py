# -*- coding: utf-8 -*-
path = "Assets/renew/Battle/Player/BattlePlayerActionController.cs"

with open(path, "rb") as f:
    raw = f.read()

content = raw.decode("utf-8")
content = content.replace("\r\n", "\n")

replacements = []

# R1: field block - remove battleBasicAttackController field, add attackFlow field, moveFlow -> internal, playerCombatData -> internal
replacements.append((
'''    [InspectorName("기본 공격 기능 모듈")]
    [SerializeField] private BattleBasicAttackController battleBasicAttackController;
    [InspectorName("카드 행동 기능 모듈")]''',
'''    [InspectorName("카드 행동 기능 모듈")]'''
))

replacements.append((
'''    [InspectorName("이동 플로우 모듈")]
    [SerializeField] private BattleUnitMoveFlow moveFlow;''',
'''    [InspectorName("이동 플로우 모듈")]
    [SerializeField] internal BattleUnitMoveFlow moveFlow;
    [InspectorName("기본 공격 플로우 모듈")]
    [SerializeField] private BattleUnitAttackFlow attackFlow;'''
))

replacements.append((
'''    private PlayerCombatData playerCombatData;''',
'''    internal PlayerCombatData playerCombatData;'''
))

# R2: RaiseActionConfirmed helper right after the event declaration
replacements.append((
'''    /// <summary>확정된 행동 결과를 구독자에게 전달하는 이벤트.</summary>
    public event System.Action<BattleActionResult> ActionConfirmed;''',
'''    /// <summary>확정된 행동 결과를 구독자에게 전달하는 이벤트.</summary>
    public event System.Action<BattleActionResult> ActionConfirmed;

    /// <summary>하위 행동 플로우(BattleUnitAttackFlow 등)가 확정 결과를 대신 발생시킬 때 쓰는 내부 통로.</summary>
    internal void RaiseActionConfirmed(BattleActionResult result) => ActionConfirmed?.Invoke(result);'''
))

# R3: IsBasicAttackActive property
replacements.append((
'''    private bool IsBasicAttackActive =>
        battleBasicAttackController != null &&
        (battleBasicAttackController.IsExecuting || battleBasicAttackController.IsAwaitingConfirmation);

    private bool IsAnyActionMoving =>
        (moveFlow != null && moveFlow.IsMoving) ||
        (battleBasicAttackController != null && battleBasicAttackController.IsExecuting);''',
'''    private bool IsBasicAttackActive => attackFlow != null && attackFlow.IsActive;

    private bool IsAnyActionMoving =>
        (moveFlow != null && moveFlow.IsMoving) ||
        (attackFlow != null && attackFlow.IsExecuting);'''
))

# R4: Awake
replacements.append((
'''        EnsureMoveFlow();
        EnsureBattleBasicAttackController();
        EnsureBattlePushPreviewView();''',
'''        EnsureMoveFlow();
        EnsureAttackFlow();
        EnsureBattlePushPreviewView();'''
))

# R5: SetPlayer
replacements.append((
'''        EnsureBattlePlayerMover();
        EnsureMoveFlow();
        playerCombatData = player != null ? player.GetComponent<PlayerCombatData>() : null;
        EnsureBattleBasicAttackController();
        EnsureBattleCardActionController();''',
'''        EnsureBattlePlayerMover();
        EnsureMoveFlow();
        playerCombatData = player != null ? player.GetComponent<PlayerCombatData>() : null;
        EnsureAttackFlow();
        EnsureBattleCardActionController();'''
))

# R6: OnDestroy - remove battleBasicAttackController unsubscribe block
replacements.append((
'''        if (battleBasicAttackController != null)
        {
            battleBasicAttackController.ConfirmationRequested -= HandleBasicAttackConfirmationRequested;
            battleBasicAttackController.Confirmed -= HandleBasicAttackConfirmed;
            battleBasicAttackController.Cancelled -= HandleBasicAttackCancelled;
        }

        if (battleCardActionController != null)''',
'''        if (battleCardActionController != null)'''
))

# R7: ConfirmMove
replacements.append((
'''        if (battleBasicAttackController != null && battleBasicAttackController.IsAwaitingConfirmation)
        {
            battleBasicAttackController.Confirm();
            return;
        }

        EnsureMoveFlow();
        moveFlow.ConfirmSelectedMove();''',
'''        if (attackFlow.IsAwaitingConfirmation)
        {
            attackFlow.Confirm();
            return;
        }

        EnsureMoveFlow();
        moveFlow.ConfirmSelectedMove();'''
))

# R8: ResetTurnMoveState
replacements.append((
'''        battleBasicAttackController?.ResetTurn();
        battleCardActionController?.ResetTurn();''',
'''        attackFlow.ResetTurn();
        battleCardActionController?.ResetTurn();'''
))

# R9: CancelMoveSelection
replacements.append((
'''        if (battleBasicAttackController != null && battleBasicAttackController.IsAwaitingConfirmation)
        {
            battleBasicAttackController.Cancel();
            return;
        }

        EnsureMoveFlow();
        moveFlow.CancelSelectedMove();''',
'''        if (attackFlow.IsAwaitingConfirmation)
        {
            attackFlow.Cancel();
            return;
        }

        EnsureMoveFlow();
        moveFlow.CancelSelectedMove();'''
))

# R10: HandleRightClick - TryBeginBasicAttack call
replacements.append((
'''        if (TryRaycastEnemy(pointerPosition, out EnemyTurnActor enemy))
        {
            TryBeginBasicAttack(enemy);
            return;
        }''',
'''        if (TryRaycastEnemy(pointerPosition, out EnemyTurnActor enemy))
        {
            attackFlow.TryBegin(enemy);
            return;
        }'''
))

# R11: delete TryBeginBasicAttack method
replacements.append((
'''    /// <summary>
    /// 우클릭한 Enemy를 공격할 수 있는 후보 타일 중 이동 경로가 가장 짧은 타일을 선택한다.
    /// </summary>
    private void TryBeginBasicAttack(EnemyTurnActor enemy)
    {
        EnsureBattleBasicAttackController();
        EnsureBattlePlayerRangeController();
        battleBasicAttackController.Begin(
            enemy,
            battlePlayerRangeController.ReachableTiles,
            battlePlayerRangeController.OccupiedEnemyTiles);
    }

''',
''
))

# R12: EnsureBattlePlayerMover -> insert EnsureAttackFlow right after EnsureMoveFlow definition
replacements.append((
'''    /// <summary>이동 플로우 전담 컴포넌트(BattleUnitMoveFlow)를 확보하고 소유자 참조를 연결한다.</summary>
    private void EnsureMoveFlow()
    {
        moveFlow = BattleComponentResolver.GetOrAdd(gameObject, moveFlow);
        moveFlow.Attach(this);
    }
''',
'''    /// <summary>이동 플로우 전담 컴포넌트(BattleUnitMoveFlow)를 확보하고 소유자 참조를 연결한다.</summary>
    private void EnsureMoveFlow()
    {
        moveFlow = BattleComponentResolver.GetOrAdd(gameObject, moveFlow);
        moveFlow.Attach(this);
    }

    /// <summary>기본 공격 플로우 전담 컴포넌트(BattleUnitAttackFlow)를 확보하고 소유자 참조를 연결한다.</summary>
    private void EnsureAttackFlow()
    {
        attackFlow = BattleComponentResolver.GetOrAdd(gameObject, attackFlow);
        attackFlow.Attach(this);
    }
'''
))

# R13: delete EnsureBattleBasicAttackController + the 3 Handle* methods
replacements.append((
'''    /// <summary>기본 공격의 계획, 임시 이동과 MP 확정을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void EnsureBattleBasicAttackController()
    {
        EnsureBattlePlayerMover();

        battleBasicAttackController = BattleComponentResolver.GetOrAdd(gameObject, battleBasicAttackController);
        battleBasicAttackController.Configure(
            player,
            playerCombatData,
            battlePlayerMover,
            FindClosestMapTile,
            BattleMapTraversalService.IsWalkable);
        battleBasicAttackController.ConfirmationRequested -= HandleBasicAttackConfirmationRequested;
        battleBasicAttackController.Confirmed -= HandleBasicAttackConfirmed;
        battleBasicAttackController.Cancelled -= HandleBasicAttackCancelled;
        battleBasicAttackController.ConfirmationRequested += HandleBasicAttackConfirmationRequested;
        battleBasicAttackController.Confirmed += HandleBasicAttackConfirmed;
        battleBasicAttackController.Cancelled += HandleBasicAttackCancelled;
    }

    private void HandleBasicAttackConfirmationRequested(string message) => ShowActionConfirmationUI(message);

    private void HandleBasicAttackConfirmed(BattleActionResult result)
    {
        if (result.MovementMPCost > 0)
        {
            turnActionState.MarkMovementUsed();
            BattleGameManager.Instance?.ResetDiceOnMove();
        }

        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(string.Empty);
        ActionConfirmed?.Invoke(result);

        BattleUnitMP playerMP = player != null ? player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"기본 공격 확정: 이동 {result.MovementMPCost}MP + 공격 {result.ActionMPCost}MP, " +
            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}. 피해 적용은 아직 연결하지 않았습니다.",
            this);
        moveFlow.ShowMoveRange();
    }

    private void HandleBasicAttackCancelled()
    {
        HideActionConfirmationUI();
        moveFlow.ShowMoveRange();
    }

''',
''
))

# R14: ShowActionConfirmationUI / HideActionConfirmationUI -> internal
replacements.append((
'''    private void ShowActionConfirmationUI(string message)''',
'''    internal void ShowActionConfirmationUI(string message)'''
))
replacements.append((
'''    private void HideActionConfirmationUI()''',
'''    internal void HideActionConfirmationUI()'''
))

for i, (old, new) in enumerate(replacements, start=1):
    count = content.count(old)
    assert count == 1, (i, count, old[:80])
    content = content.replace(old, new, 1)

content = content.replace("\n", "\r\n")

with open(path, "wb") as f:
    f.write(content.encode("utf-8"))

print("OK, total replacements:", len(replacements))
