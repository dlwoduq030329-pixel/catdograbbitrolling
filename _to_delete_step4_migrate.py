# -*- coding: utf-8 -*-
path = "Assets/renew/Battle/Player/BattlePlayerActionController.cs"

with open(path, "rb") as f:
    raw = f.read()

content = raw.decode("utf-8")
content = content.replace("\r\n", "\n")

replacements = []

# R1: field block - remove battleCardActionController field, add cardFlow field
replacements.append((
'''    [InspectorName("카드 행동 기능 모듈")]
    [SerializeField] private BattleCardActionController battleCardActionController;
    [InspectorName("행동 확인 화면 모듈")]''',
'''    [InspectorName("행동 확인 화면 모듈")]'''
))

replacements.append((
'''    [InspectorName("기본 공격 플로우 모듈")]
    [SerializeField] private BattleUnitAttackFlow attackFlow;''',
'''    [InspectorName("기본 공격 플로우 모듈")]
    [SerializeField] private BattleUnitAttackFlow attackFlow;
    [InspectorName("카드 플로우 모듈")]
    [SerializeField] private BattleUnitCardFlow cardFlow;'''
))

# R2: battlePushPreviewView field -> internal
replacements.append((
'''    [InspectorName("밀치기 결과 사전 예고 화면")]
    [SerializeField] private BattlePushPreviewView battlePushPreviewView;''',
'''    [InspectorName("밀치기 결과 사전 예고 화면")]
    [SerializeField] internal BattlePushPreviewView battlePushPreviewView;'''
))

# R3: delete HasValidTargetForCard, widen IsAnyActionMoving/IsBasicAttackActive stay, IsCardActionActive -> cardFlow
replacements.append((
'''    public bool HasValidTargetForCard(BattleCardData card)
    {
        if (card == null) return false;
        bool explicitlyTargetsEnemy = card.targetType == BattleCardTargetType.Enemy ||
                                      card.targetType == BattleCardTargetType.Character ||
                                      card.targetType == BattleCardTargetType.AllEnemies;
        if (card.category != BattleCardCategory.Attack && !explicitlyTargetsEnemy) return true;

        ResolveBattleDataPool();
        GameObject currentPlayer = player != null ? player : battleDataPool != null ? battleDataPool.CurrentPlayer : null;
        MapInfo playerTile = currentPlayer != null ? FindClosestMapTile(currentPlayer.transform.position) : null;
        if (playerTile == null) return false;

        IEnumerable<GameObject> registered = battleDataPool != null && battleDataPool.Units != null
            ? battleDataPool.Units.Enemies : null;
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
            MapInfo enemyTile = FindClosestMapTile(enemy.transform.position);
            int distance = BattleTileRangeCalculator.GetDistance(playerTile, enemyTile, range);
            if (distance >= 0 && distance <= range) return true;
        }
        return false;
    }

    private bool IsBasicAttackActive => attackFlow != null && attackFlow.IsActive;

    private bool IsAnyActionMoving =>
        (moveFlow != null && moveFlow.IsMoving) ||
        (attackFlow != null && attackFlow.IsExecuting);

    private bool IsCardActionActive =>
        battleCardActionController != null &&
        (battleCardActionController.IsSelectingTarget || battleCardActionController.IsAwaitingConfirmation);''',
'''    private bool IsBasicAttackActive => attackFlow != null && attackFlow.IsActive;

    /// <summary>이동 실행 코루틴 또는 기본 공격 실행이 진행 중인지. BattleUnitCardFlow의 우클릭 대상
    /// 선택 처리에서도 같은 판단이 필요해 internal로 열어뒀다.</summary>
    internal bool IsAnyActionMoving =>
        (moveFlow != null && moveFlow.IsMoving) ||
        (attackFlow != null && attackFlow.IsExecuting);

    private bool IsCardActionActive => cardFlow != null && cardFlow.IsActive;'''
))

# R4: Awake
replacements.append((
'''        EnsureMoveFlow();
        EnsureAttackFlow();
        EnsureBattlePushPreviewView();
        EnsureBattleCardActionController();
        EnsureBattlePlayerInputReader();''',
'''        EnsureMoveFlow();
        EnsureAttackFlow();
        EnsureBattlePushPreviewView();
        EnsureCardFlow();
        EnsureBattlePlayerInputReader();'''
))

# R5: SetPlayer
replacements.append((
'''        EnsureAttackFlow();
        EnsureBattleCardActionController();
        if (player != null && playerCombatData == null)''',
'''        EnsureAttackFlow();
        EnsureCardFlow();
        if (player != null && playerCombatData == null)'''
))

# R6: OnDestroy - remove battleCardActionController unsubscribe block
replacements.append((
'''        if (battleCardActionController != null)
        {
            battleCardActionController.TargetSelectionRequested -= HandleCardTargetSelectionRequested;
            battleCardActionController.ConfirmationRequested -= HandleCardConfirmationRequested;
            battleCardActionController.Confirmed -= HandleCardConfirmed;
            battleCardActionController.Cancelled -= HandleCardCancelled;
            battleCardActionController.RangeVisibilityChanged -= SetRangeVisible;
        }
    }''',
'''    }'''
))

# R7: ConfirmMove card branch
replacements.append((
'''        if (battleCardActionController != null && battleCardActionController.IsAwaitingConfirmation)
        {
            battleCardActionController.Confirm();
            return;
        }

        if (attackFlow.IsAwaitingConfirmation)''',
'''        if (cardFlow.IsAwaitingConfirmation)
        {
            cardFlow.Confirm();
            return;
        }

        if (attackFlow.IsAwaitingConfirmation)'''
))

# R8: ResetTurnMoveState
replacements.append((
'''        attackFlow.ResetTurn();
        battleCardActionController?.ResetTurn();''',
'''        attackFlow.ResetTurn();
        cardFlow.ResetTurn();'''
))

# R9: HandleRightClick card branch
replacements.append((
'''        if (battleCardActionController != null && battleCardActionController.IsSelectingTarget)
        {
            HandleCardTargetRightClick(pointerPosition);
            return;
        }''',
'''        if (cardFlow.IsSelectingTarget)
        {
            cardFlow.HandleTargetRightClick(pointerPosition);
            return;
        }'''
))

# R10: delete HandleCardTargetRightClick method
replacements.append((
'''    /// <summary>카드 대상 유형에 맞는 적 또는 타일을 우클릭으로 선택한다.</summary>
    private void HandleCardTargetRightClick(Vector2 pointerPosition)
    {
        if (IsAnyActionMoving)
        {
            return;
        }

        if (BattlePlayerInputReader.IsPointerOverInteractiveUI(pointerPosition))
        {
            return;
        }

        BattleCardTargetType targetType = battleCardActionController.TargetType;
        if ((targetType == BattleCardTargetType.Enemy || targetType == BattleCardTargetType.Character) &&
            TryRaycastEnemy(pointerPosition, out EnemyTurnActor enemy))
        {
            MapInfo enemyTile = FindClosestMapTile(enemy.transform.position);
            if (battleCardActionController.TrySelectTarget(enemy.gameObject, enemyTile))
            {
                return;
            }
        }

        if (targetType == BattleCardTargetType.Tile &&
            TryRaycastMapTile(pointerPosition, out MapInfo tile) &&
            battleCardActionController.TrySelectTarget(tile.gameObject, tile))
        {
            return;
        }

        Debug.Log("카드 사거리 안의 올바른 대상을 선택해야 합니다.", this);
    }

''',
''
))

# R11: TryRaycastEnemy / TryRaycastMapTile -> internal
replacements.append((
'''    private bool TryRaycastEnemy(Vector2 pointerPosition, out EnemyTurnActor enemy)''',
'''    internal bool TryRaycastEnemy(Vector2 pointerPosition, out EnemyTurnActor enemy)'''
))
replacements.append((
'''    private bool TryRaycastMapTile(Vector2 pointerPosition, out MapInfo tile)''',
'''    internal bool TryRaycastMapTile(Vector2 pointerPosition, out MapInfo tile)'''
))

# R12: BeginCardUseConfirmation
replacements.append((
'''    /// <summary>카드 사용 확인 단계를 열고 공용 확인·취소 버튼을 표시한다.</summary>
    public bool BeginCardUseConfirmation(
        PendingBattleCardUse cardUse,
        BattleCardDrawSystem cardDrawSystem)
    {
        if (cardUse == null || cardDrawSystem == null || player == null ||
            IsAnyActionMoving || moveFlow.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)
        {
            return false;
        }

        if (!HasValidTargetForCard(cardUse.CardData))
        {
            Debug.Log("Card use blocked: no valid enemy is within this card's range.", this);
            return false;
        }

        moveFlow.ClearMoveRange();
        EnsureBattleCardActionController();
        bool canUseCards = BattleGameManager.Instance != null && BattleGameManager.Instance.CanUsePlayerCards;
        return battleCardActionController.Begin(cardUse, cardDrawSystem, canUseCards);
    }''',
'''    /// <summary>카드 사용 확인 단계를 열고 공용 확인·취소 버튼을 표시한다. 다른 행동이 진행
    /// 중인지 라우팅 판단만 여기서 하고, 실제 시작 처리는 BattleUnitCardFlow에 위임한다.</summary>
    public bool BeginCardUseConfirmation(
        PendingBattleCardUse cardUse,
        BattleCardDrawSystem cardDrawSystem)
    {
        if (cardUse == null || cardDrawSystem == null || player == null ||
            IsAnyActionMoving || moveFlow.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)
        {
            return false;
        }

        EnsureCardFlow();
        return cardFlow.BeginUseConfirmation(cardUse, cardDrawSystem);
    }'''
))

# R13: CancelMoveSelection card branch
replacements.append((
'''        if (IsCardActionActive)
        {
            battleCardActionController.Cancel();
            return;
        }''',
'''        if (IsCardActionActive)
        {
            cardFlow.Cancel();
            return;
        }'''
))

# R14: insert EnsureCardFlow after EnsureAttackFlow
replacements.append((
'''    /// <summary>기본 공격 플로우 전담 컴포넌트(BattleUnitAttackFlow)를 확보하고 소유자 참조를 연결한다.</summary>
    private void EnsureAttackFlow()
    {
        attackFlow = BattleComponentResolver.GetOrAdd(gameObject, attackFlow);
        attackFlow.Attach(this);
    }
''',
'''    /// <summary>기본 공격 플로우 전담 컴포넌트(BattleUnitAttackFlow)를 확보하고 소유자 참조를 연결한다.</summary>
    private void EnsureAttackFlow()
    {
        attackFlow = BattleComponentResolver.GetOrAdd(gameObject, attackFlow);
        attackFlow.Attach(this);
    }

    /// <summary>카드 플로우 전담 컴포넌트(BattleUnitCardFlow)를 확보하고 소유자 참조를 연결한다.</summary>
    private void EnsureCardFlow()
    {
        cardFlow = BattleComponentResolver.GetOrAdd(gameObject, cardFlow);
        cardFlow.Attach(this);
    }
'''
))

# R15: delete EnsureBattleCardActionController + 4 Handle* methods
replacements.append((
'''    /// <summary>카드 대상 선택, 사거리 표시와 MP·손패 확정을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void EnsureBattleCardActionController()
    {
        EnsureBattleRangeVisualizer();

        battleCardActionController = BattleComponentResolver.GetOrAdd(gameObject, battleCardActionController);
        battleCardActionController.Configure(
            player,
            battleRangeVisualizer,
            colorPalette.CardRangeTileColor,
            colorPalette.CardEffectAreaTileColor,
            FindClosestMapTile,
            mainCamera,
            battlePushPreviewView);
        battleCardActionController.TargetSelectionRequested -= HandleCardTargetSelectionRequested;
        battleCardActionController.ConfirmationRequested -= HandleCardConfirmationRequested;
        battleCardActionController.Confirmed -= HandleCardConfirmed;
        battleCardActionController.Cancelled -= HandleCardCancelled;
        battleCardActionController.RangeVisibilityChanged -= SetRangeVisible;
        battleCardActionController.TargetSelectionRequested += HandleCardTargetSelectionRequested;
        battleCardActionController.ConfirmationRequested += HandleCardConfirmationRequested;
        battleCardActionController.Confirmed += HandleCardConfirmed;
        battleCardActionController.Cancelled += HandleCardCancelled;
        battleCardActionController.RangeVisibilityChanged += SetRangeVisible;
    }

    private void HandleCardTargetSelectionRequested(string message)
    {
        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(message);
    }

    private void HandleCardConfirmationRequested(string message) => ShowActionConfirmationUI(message);

    private void HandleCardConfirmed(BattleActionResult result)
    {
        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(string.Empty);
        FindFirstObjectByType<BattleCardPanelToggle>()?.Hide();
        ActionConfirmed?.Invoke(result);

        BattleUnitMP playerMP = player != null ? player.GetComponent<BattleUnitMP>() : null;
        Debug.Log(
            $"카드 사용 확정: {result.Request.DisplayName}, 소모 {result.ActionMPCost}MP, " +
            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}.",
            this);
    }

    private void HandleCardCancelled()
    {
        HideActionConfirmationUI();
    }

''',
''
))

# R16: EnsureBattlePushPreviewView -> internal
replacements.append((
'''    private void EnsureBattlePushPreviewView()''',
'''    internal void EnsureBattlePushPreviewView()'''
))

for i, (old, new) in enumerate(replacements, start=1):
    count = content.count(old)
    assert count == 1, (i, count, old[:80])
    content = content.replace(old, new, 1)

content = content.replace("\n", "\r\n")

with open(path, "wb") as f:
    f.write(content.encode("utf-8"))

print("OK, total replacements:", len(replacements))
