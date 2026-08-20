using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player 클릭부터 이동/공격 범위 표시, 목적지 또는 Enemy 선택, 확정/취소까지 담당하는 입력 Controller다.
/// MapInfo의 상하좌우 연결을 사용하며 확정된 이동 경로 1칸당 행동력 1을 차감한다.
/// </summary>
public class BattlePlayerActionController : MonoBehaviour
{
    public static bool IsMoveRangeVisible { get; private set; }
    public static event System.Action<bool> MoveRangeVisibilityChanged;

    [Header("필수 참조")]
    [InspectorName("메인 카메라")]
    public Camera mainCamera;
    [InspectorName("플레이어")]
    public GameObject player;
    [InspectorName("이동 확정 버튼")]
    public Button confirmMoveButton;
    [InspectorName("이동 선택 취소 버튼")]
    public Button quitMoveButton;
    [InspectorName("이동 버튼 묶음")]
    public GameObject moveButtonGroup;
    [InspectorName("행동 확인 안내 텍스트 (선택 사항)")]
    [SerializeField] private TMP_Text actionConfirmText;

    [Header("이동 범위")]
    [InspectorName("최소 이동 범위")]
    public int minMoveRange = 1;
    [InspectorName("최대 이동 범위")]
    public int maxMoveRange = 6;
    [InspectorName("현재 이동 범위")]
    public int currentMoveRange = 3;

    [Header("레이캐스트 설정")]
    [InspectorName("타일 레이어 마스크")]
    public LayerMask tileLayerMask = ~0;
    [InspectorName("전투 레이캐스트 모듈")]
    [SerializeField] private BattleRaycaster battleRaycaster;
    [InspectorName("전투 범위 표시 모듈")]
    [SerializeField] private BattleRangeVisualizer battleRangeVisualizer;
    [InspectorName("플레이어 범위 제어 모듈")]
    [SerializeField] private BattlePlayerRangeController battlePlayerRangeController;
    [InspectorName("플레이어 이동 실행 모듈")]
    [SerializeField] private BattlePlayerMover battlePlayerMover;
    [InspectorName("일반 이동 기능 모듈")]
    [SerializeField] private BattleMovementController battleMovementController;
    [InspectorName("기본 공격 기능 모듈")]
    [SerializeField] private BattleBasicAttackController battleBasicAttackController;
    [InspectorName("카드 행동 기능 모듈")]
    [SerializeField] private BattleCardActionController battleCardActionController;
    [InspectorName("이동 목적지 프리뷰 모듈")]
    [SerializeField] private BattleMovePreview battleMovePreview;
    [InspectorName("행동 확인 화면 모듈")]
    [SerializeField] private BattleActionConfirmView battleActionConfirmView;
    [InspectorName("플레이어 입력 감지 모듈")]
    [SerializeField] private BattlePlayerInputReader battlePlayerInputReader;
    [InspectorName("캐릭터 마우스 오버 강조 모듈")]
    [SerializeField] private BattleUnitHoverHighlighter battleUnitHoverHighlighter;
    [InspectorName("밀치기 결과 사전 예고 화면")]
    [SerializeField] private BattlePushPreviewView battlePushPreviewView;
    [InspectorName("이동 타일 Enemy 위협 연결선")]
    [SerializeField] private BattleMoveThreatPreview battleMoveThreatPreview;
    [InspectorName("전투 데이터 저장소")]
    [SerializeField] private BattleDataPool battleDataPool;

    [Header("이동 연출 시간")]
    [InspectorName("타일당 기본 이동 시간")]
    public float secondsPerTile = 1f;
    [InspectorName("이동 속도 배율")]
    public float moveSpeedMultiplier = 4f;

    [Header("이동 범위 색상")]
    [InspectorName("이동 가능 타일 색상")]
    public Color movableTileColor = new Color(0.25f, 0.9f, 0.25f, 1f);
    [InspectorName("이동 불가 타일 색상")]
    public Color blockedTileColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [InspectorName("적 감지 범위 타일 색상")]
    public Color enemyDetectColor = new Color(1f, 0.6f, 0.15f, 1f);
    [InspectorName("선택 타일 색상")]
    public Color selectedTileColor = new Color(1f, 0.95f, 0.35f, 1f);
    [InspectorName("도착 타일 색상")]
    public Color landedTileColor = new Color(0.55f, 1f, 0.85f, 1f);
    [InspectorName("이동 후 공격 가능 타일 색상")]
    public Color attackableTileColor = new Color(0.9f, 0.2f, 0.25f, 1f);
    [InspectorName("카드 사용 가능 타일 색상")]
    public Color cardRangeTileColor = new Color(0.35f, 0.45f, 1f, 1f);
    [InspectorName("카드 실제 효과 범위 색상")]
    public Color cardEffectAreaTileColor = new Color(0.15f, 0.9f, 0.95f, 1f);
    [InspectorName("R 토글 - 적 위협 범위 색상")]
    public Color enemyThreatTileColor = new Color(0.75f, 0.15f, 0.85f, 1f);
    [Header("이동 범위 가시성 조절")]
    [InspectorName("범위 색상 혼합 강도")]
    [SerializeField, Range(0f, 1f)] private float rangeColorBlend = 0.3f;
    [InspectorName("선택 타일 색상 혼합 강도")]
    [SerializeField, Range(0f, 1f)] private float selectedColorBlend = 0.65f;
    [InspectorName("도착 타일 색상 혼합 강도")]
    [SerializeField, Range(0f, 1f)] private float landedColorBlend = 0.5f;
    [InspectorName("도착 타일 강조 시간")]
    public float landedHighlightDuration = 0.6f;
    [InspectorName("이동 화살표 프리팹")]
    public GameObject moveArrowPrefab;
    [InspectorName("화살표 위치 보정값")]
    public Vector3 arrowOffset = new Vector3(0f, 0.45f, 0f);

    private BattlePlayerMapContext battlePlayerMapContext;
    private readonly BattlePlayerTurnActionState turnActionState = new BattlePlayerTurnActionState();
    private bool rangeVisible;
    /// <summary>R 단축키로 범위 표시를 켠 상태인지 기억한다. 이동으로 범위가 잠깐 꺼져도 이동 후 자동으로 다시 켠다.</summary>
    private bool rangeToggleActive;
    private bool isMoving;
    private PlayerCombatData playerCombatData;

    /// <summary>확정된 행동 결과를 구독자에게 전달하는 이벤트.</summary>
    public event System.Action<BattleActionResult> ActionConfirmed;

    /// <summary>전투 정지·재개 시 Player의 마우스와 키보드 입력 감지를 함께 켜거나 끕니다.</summary>
    public void SetBattleInputEnabled(bool enabled)
    {
        EnsureBattlePlayerInputReader();
        battlePlayerInputReader.enabled = enabled;
    }

    public bool HasValidTargetForCard(BattleCardData card)
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

    private bool IsBasicAttackActive =>
        battleBasicAttackController != null &&
        (battleBasicAttackController.IsExecuting || battleBasicAttackController.IsAwaitingConfirmation);

    private bool IsAnyActionMoving =>
        isMoving || (battleBasicAttackController != null && battleBasicAttackController.IsExecuting);

    private bool IsCardActionActive =>
        battleCardActionController != null &&
        (battleCardActionController.IsSelectingTarget || battleCardActionController.IsAwaitingConfirmation);

    /// <summary>카메라와 확인 화면 참조를 보완하고 확인·취소 버튼 이벤트를 연결한다.</summary>
    private void Awake()
    {
        ResolveBattleDataPool();
        RefreshCamera();
        EnsureBattleRaycaster();
        EnsureBattleRangeVisualizer();
        EnsureBattlePlayerMapContext();
        EnsureBattlePlayerRangeController();
        EnsureBattlePlayerMover();
        EnsureBattleMovementController();
        EnsureBattleBasicAttackController();
        EnsureBattlePushPreviewView();
        EnsureBattleCardActionController();
        EnsureBattleMovePreview();
        EnsureBattlePlayerInputReader();
        EnsureBattleUnitHoverHighlighter();
        EnsureBattleMoveThreatPreview();

        EnsureBattleActionConfirmView();

        if (confirmMoveButton != null)
            confirmMoveButton.interactable = false;
        if (quitMoveButton != null)
            quitMoveButton.interactable = false;

        SetMoveButtonGroupVisible(false);
    }

    /// <summary>맵 생성 후 사용할 타일 목록과 원본 표시 상태를 처음 수집한다.</summary>
    private void Start()
    {
        RefreshMapTiles();
    }

    private void OnDestroy()
    {
        if (battlePlayerInputReader != null)
        {
            battlePlayerInputReader.LeftClickRequested -= HandleLeftClick;
            battlePlayerInputReader.DoubleLeftClickRequested -= HandleDebugDoubleClick;
            battlePlayerInputReader.RightClickRequested -= HandleRightClick;
            battlePlayerInputReader.CancelRequested -= HandleCancelInput;
            battlePlayerInputReader.RangeToggleRequested -= HandleRangeToggleRequested;
        }

        if (battleBasicAttackController != null)
        {
            battleBasicAttackController.ConfirmationRequested -= HandleBasicAttackConfirmationRequested;
            battleBasicAttackController.Confirmed -= HandleBasicAttackConfirmed;
            battleBasicAttackController.Cancelled -= HandleBasicAttackCancelled;
        }

        if (battleCardActionController != null)
        {
            battleCardActionController.TargetSelectionRequested -= HandleCardTargetSelectionRequested;
            battleCardActionController.ConfirmationRequested -= HandleCardConfirmationRequested;
            battleCardActionController.Confirmed -= HandleCardConfirmed;
            battleCardActionController.Cancelled -= HandleCardCancelled;
            battleCardActionController.RangeVisibilityChanged -= SetRangeVisible;
        }
    }

    /// <summary>BattleGameManager가 생성된 실제 Player 인스턴스를 전달한다.</summary>
    public void SetPlayer(GameObject targetPlayer)
    {
        player = targetPlayer;
        EnsureBattleRaycaster();
        battleRaycaster.SetPlayer(targetPlayer);
        EnsureBattleUnitHoverHighlighter();
        EnsureBattlePlayerMover();
        EnsureBattleMovementController();
        playerCombatData = player != null ? player.GetComponent<PlayerCombatData>() : null;
        EnsureBattleBasicAttackController();
        EnsureBattleCardActionController();
        if (player != null && playerCombatData == null)
        {
            Debug.LogError("플레이어 전투 데이터가 초기화되지 않았습니다. BattleGameManager를 통해 플레이어를 등록해야 합니다.", player);
        }

        Debug.Log(targetPlayer != null
            ? $"이동 시스템 플레이어 등록 완료: {targetPlayer.name}"
            : "이동 시스템의 플레이어 참조를 해제했습니다.", this);
    }

    /// <summary>주사위 결과를 이번 턴 이동 상한으로 저장하고 이동 입력을 허용한다.</summary>
    public void SetMoveRange(int moveRange)
    {
        currentMoveRange = Mathf.Clamp(moveRange, minMoveRange, maxMoveRange);
        turnActionState.MarkDiceRolled();
        Debug.Log($"이동 범위 설정: {currentMoveRange}칸", this);
        ShowMoveRange();
    }

    /// <summary>QA 전투에서 주사위 이동 범위 상한을 빠르게 확장한다.</summary>
    public void ConfigureDebugMoveRange(int maximumRange)
    {
        maxMoveRange = Mathf.Max(minMoveRange, maximumRange);
        currentMoveRange = Mathf.Clamp(currentMoveRange, minMoveRange, maxMoveRange);
    }

    /// <summary>현재 선택된 목적지가 유효하면 실제 이동 Coroutine을 시작한다.</summary>
    public void ConfirmMove()
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

        EnsureBattleMovementController();
        if (!battleMovementController.IsAwaitingConfirmation ||
            battleMovementController.PendingTarget == null ||
            !turnActionState.DiceRolled || turnActionState.MovementUsed)
        {
            return;
        }

        SetMoveButtonGroupVisible(false);
        StartCoroutine(MovePlayerToSelectedTile());
    }

    /// <summary>새 Player 턴에 주사위, 이동 사용, 선택, 표시 상태를 전부 초기화한다.</summary>
    public void ResetTurnMoveState()
    {
        // R 토글은 사용자가 직접 끄거나(CancelMoveSelection의 완전 닫힘 분기) 명시적으로 끄기 전까지는
        // 턴이 바뀌어도 유지한다. 여기서는 상태만 초기화하고 필요하면 마지막에 다시 켠다.
        turnActionState.Reset();
        EnsureBattleMovementController();
        battleMovementController.ResetTurn();
        EnsureBattlePlayerRangeController();
        battlePlayerRangeController.ClearState();
        battleBasicAttackController?.ResetTurn();
        battleCardActionController?.ResetTurn();
        ClearMoveArrow();
        ClearMoveRange();

        if (confirmMoveButton != null)
        {
            confirmMoveButton.interactable = false;
        }


        if (quitMoveButton != null)
        {
            quitMoveButton.interactable = false;
        }

        SetMoveButtonGroupVisible(false);

        // 새 턴에서도 R 토글이 켜져 있던 상태라면 적 위협 범위 표시를 바로 되살린다.
        if (rangeToggleActive)
        {
            ShowEnemyThreatRange();
        }
    }

    /// <summary>ESC 입력을 현재 단계에 맞는 취소 동작으로 전달한다.</summary>
    private void HandleCancelInput()
    {
        if (!IsAnyActionMoving)
        {
            CancelMoveSelection();
        }
    }

    /// <summary>
    /// 이동 가능 타일 좌클릭으로 목적지를 고르고, 목적지가 선택된 상태에서 Player를
    /// 좌클릭하면 이동을 확정한다. 주사위 이후 이동 범위는 별도 버튼 없이 계속 유지한다.
    /// </summary>
    private void HandleLeftClick(Vector2 pointerPosition)
    {
        if (IsAnyActionMoving)
        {
            return;
        }

        if (BattlePlayerInputReader.IsPointerOverInteractiveUI(pointerPosition))
        {
            return;
        }

        if (TryRaycastPlayer(pointerPosition, out GameObject clickedPlayer))
        {
            if (battleMovementController.IsAwaitingConfirmation)
            {
                Debug.Log($"플레이어 클릭으로 이동 확정: {battleMovementController.PendingTarget?.name}", clickedPlayer);
                ConfirmMove();
                return;
            }

            if (IsBasicAttackActive || IsCardActionActive)
            {
                return;
            }

            Debug.Log($"플레이어 클릭: {clickedPlayer.name}, 주사위 굴림={turnActionState.DiceRolled}", clickedPlayer);

            if (!turnActionState.DiceRolled)
            {
                Debug.Log("이동 범위를 표시하려면 먼저 주사위를 굴려야 합니다.", this);
                return;
            }

            if (rangeToggleActive)
            {
                // R 위협 범위를 유지하면서 이동 가능 집합도 함께 계산해 Player 이동 입력을 연다.
                ShowMoveRange();
                ShowEnemyThreatRange(true);
            }
            else
                ShowMoveRange();
            return;
        }

        if (rangeVisible)
        {
            bool clickedReachableTile =
                TryRaycastMapTile(pointerPosition, out MapInfo clickedTile) &&
                battlePlayerRangeController.IsReachable(clickedTile);

            if (clickedReachableTile && !turnActionState.MovementUsed &&
                !IsBasicAttackActive && !IsCardActionActive)
            {
                SelectMoveTile(clickedTile);
                return;
            }

            // R 적 위협 범위는 빈 바닥 클릭으로 닫지 않는다. 일반 이동 범위에서는
            // 범위 밖 클릭 시 목적지 선택만 취소하고 이동 가능 범위를 다시 표시한다.
            if (!clickedReachableTile && !rangeToggleActive)
            {
                CancelMoveSelection();
            }
        }
    }

    /// <summary>디버그 QA 모드에서 더블 클릭한 타일로 MP와 이동 규칙을 소비하지 않고 즉시 이동한다.</summary>
    private void HandleDebugDoubleClick(Vector2 pointerPosition)
    {
        BattleGameManager manager = BattleGameManager.Instance;
        if (manager == null || !manager.IsDebugQaBoostEnabled || IsAnyActionMoving ||
            BattlePlayerInputReader.IsPointerOverInteractiveUI(pointerPosition) ||
            !TryRaycastMapTile(pointerPosition, out MapInfo targetTile) || player == null)
            return;

        MapInfo currentTile = FindClosestMapTile(player.transform.position);
        float heightOffset = currentTile != null
            ? player.transform.position.y - currentTile.transform.position.y
            : 0f;

        CancelMoveSelection();
        player.transform.position = targetTile.transform.position + Vector3.up * heightOffset;
        BattleCharacterAnimationBridge.PlayIdle(player);
        Debug.Log($"[Debug] Player teleported to tile {targetTile.Index}.", targetTile);
    }

    /// <summary>
    /// Player를 직접 클릭하지 않고 단축키만으로 이동·공격 사거리를 켜고 끈다.
    /// 클릭으로 여는 경우와 동일한 조건(주사위 굴림, 다른 행동 진행 중 여부)을 그대로 적용한다.
    /// </summary>
    private void HandleRangeToggleRequested()
    {
        if (IsAnyActionMoving)
        {
            return;
        }

        // 카드 사거리 표시·대상 선택 중에도 R 토글을 쓸 수 있도록 IsCardActionActive는 더 이상 막지 않는다.
        if (battleMovementController.IsAwaitingConfirmation || IsBasicAttackActive)
        {
            return;
        }

        if (!turnActionState.DiceRolled)
        {
            Debug.Log("이동 범위를 표시하려면 먼저 주사위를 굴려야 합니다.", this);
            return;
        }

        if (rangeVisible)
        {
            rangeToggleActive = false;
            CancelMoveSelection();
        }
        else
        {
            rangeToggleActive = true;
            ShowEnemyThreatRange();
        }
    }

    /// <summary>우클릭은 카드 대상 및 Enemy 기본 공격 선택에만 사용한다. 일반 이동은 좌클릭 전용이다.</summary>
    private void HandleRightClick(Vector2 pointerPosition)
    {
        if (battleCardActionController != null && battleCardActionController.IsSelectingTarget)
        {
            HandleCardTargetRightClick(pointerPosition);
            return;
        }

        if (!turnActionState.DiceRolled || !rangeVisible ||
            IsAnyActionMoving || battleMovementController.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)
        {
            return;
        }

        if (BattlePlayerInputReader.IsPointerOverInteractiveUI(pointerPosition))
        {
            return;
        }

        if (TryRaycastEnemy(pointerPosition, out EnemyTurnActor enemy))
        {
            TryBeginBasicAttack(enemy);
            return;
        }

        // 일반 이동 목적지는 좌클릭으로만 선택한다. 빈 바닥 우클릭은 현재 이동 선택을
        // 변경하거나 범위를 닫지 않는다.
    }

    /// <summary>카드 대상 유형에 맞는 적 또는 타일을 우클릭으로 선택한다.</summary>
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

    /// <summary>Player 본체 또는 자식 Collider가 클릭됐는지 검사한다.</summary>
    private bool TryRaycastPlayer(Vector2 pointerPosition, out GameObject clickedPlayer)
    {
        RefreshCamera();
        EnsureBattleRaycaster();
        return battleRaycaster.TryGetPlayer(pointerPosition, out clickedPlayer);
    }

    /// <summary>마우스 아래 Collider의 부모에서 활성 EnemyTurnActor를 찾는다.</summary>
    private bool TryRaycastEnemy(Vector2 pointerPosition, out EnemyTurnActor enemy)
    {
        RefreshCamera();
        EnsureBattleRaycaster();
        return battleRaycaster.TryGetEnemy(pointerPosition, out enemy);
    }

    /// <summary>마우스 아래 충돌체의 부모에서 MapInfo를 찾는다.</summary>
    private bool TryRaycastMapTile(Vector2 pointerPosition, out MapInfo tile)
    {
        RefreshCamera();
        EnsureBattleRaycaster();
        return battleRaycaster.TryGetMapTile(pointerPosition, out tile);
    }

    /// <summary>
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

        CharacterMP playerMP = player != null ? player.GetComponent<CharacterMP>() : null;
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
            movableTileColor,
            blockedTileColor,
            attackableTileColor,
            enemyDetectColor);
        SetRangeVisible(shown);
    }

    /// <summary>
    /// R 단축키 전용 표시. Player 자신의 이동·공격 범위 대신
    /// 활성 Enemy들이 이번 턴에 실제로 위협할 수 있는 타일을 계산해 보여준다.
    /// </summary>
    private void ShowEnemyThreatRange(bool preserveMoveRange = false)
    {
        RefreshMapTiles();
        EnsureBattlePlayerRangeController();
        if (!preserveMoveRange)
        {
            RestoreAllTileColors();
            battlePlayerRangeController.ClearState();
        }
        ResolveBattleDataPool();

        IEnumerable<GameObject> enemies = battleDataPool != null && battleDataPool.Units != null
            ? battleDataPool.Units.Enemies
            : null;
        bool shown = battlePlayerRangeController.BuildAndShowEnemyThreatRange(
            BattleMapTraversalService.IsWalkable,
            enemies,
            FindClosestMapTile,
            enemyThreatTileColor);
        SetRangeVisible(preserveMoveRange ? rangeVisible || shown : shown);
    }

    /// <summary>월드 좌표와 XZ 평면상 가장 가까운 MapInfo 타일을 찾는다.</summary>
    private MapInfo FindClosestMapTile(Vector3 worldPosition)
    {
        ResolveBattleDataPool();
        EnsureBattlePlayerMapContext();
        if (battlePlayerMapContext.Tiles.Count == 0)
        {
            RefreshMapTiles();
        }

        return battlePlayerMapContext.FindClosest(battleDataPool, worldPosition);
    }

    /// <summary>현재 PlayerCombatData의 기본 공격 사거리를 반환한다.</summary>
    private int GetPlayerAttackRange()
    {
        if (playerCombatData == null && player != null)
        {
            playerCombatData = player.GetComponent<PlayerCombatData>();
        }

        return playerCombatData != null ? playerCombatData.BasicAttackRangeTiles : 1;
    }

    /// <summary>
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

    /// <summary>이전 선택 색상을 복구한 뒤 새 목적지 강조와 화살표를 표시한다.
    /// 이동 확정은 별도 UI가 아니라 Player 본체 좌클릭으로 수행한다.</summary>
    private void SelectMoveTile(MapInfo targetTile)
    {
        // 다른 타일을 다시 선택할 때 이전 선택 강조를 먼저 제거한다.
        ShowMoveRange();
        EnsureBattleMovementController();
        if (!battleMovementController.SelectTarget(targetTile))
        {
            return;
        }

        SetTileColor(targetTile, selectedTileColor, selectedColorBlend);
        ShowMoveArrow(targetTile);
        SetActionConfirmText(string.Empty);
        if (confirmMoveButton != null) confirmMoveButton.interactable = false;
        if (quitMoveButton != null) quitMoveButton.interactable = false;
        SetMoveButtonGroupVisible(false);
    }

    /// <summary>
    /// 목적지까지 최단 경로를 따라 이동하고 완료 후 경로 칸 수만큼 MP를 차감한다.
    /// 취소나 경로 실패에는 MP를 차감하지 않는다.
    /// </summary>
    private IEnumerator MovePlayerToSelectedTile()
    {
        EnsureBattleMovementController();
        MapInfo targetTile = battleMovementController.PendingTarget;
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
        CharacterMP playerMP = player.GetComponent<CharacterMP>();
        int maxMovementTiles = playerMP != null
            ? Mathf.Min(currentMoveRange, playerMP.CurrentMP)
            : 0;
        isMoving = true;
        EnsureBattleMovementController();
        BattleMovementResult movementResult = null;
        yield return battleMovementController.ExecutePending(
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

        if (confirmMoveButton != null)
        {
            confirmMoveButton.interactable = false;
        }

        if (quitMoveButton != null)
        {
            quitMoveButton.interactable = false;
        }

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
            landedTileColor,
            landedHighlightDuration);
    }

    /// <summary>카드 사용 확인 단계를 열고 공용 확인·취소 버튼을 표시한다.</summary>
    public bool BeginCardUseConfirmation(
        PendingBattleCardUse cardUse,
        BattleCardDrawSystem cardDrawSystem)
    {
        if (cardUse == null || cardDrawSystem == null || player == null ||
            IsAnyActionMoving || battleMovementController.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)
        {
            return false;
        }

        if (!HasValidTargetForCard(cardUse.CardData))
        {
            Debug.Log("Card use blocked: no valid enemy is within this card's range.", this);
            return false;
        }

        ClearMoveRange();
        EnsureBattleCardActionController();
        bool canUseCards = BattleGameManager.Instance != null && BattleGameManager.Instance.CanUsePlayerCards;
        return battleCardActionController.Begin(cardUse, cardDrawSystem, canUseCards);
    }

    /// <summary>이동 또는 공격 확정 단계의 안내 문구를 전투 확인 텍스트에 표시한다.</summary>
    private void SetActionConfirmText(string message)
    {
        EnsureBattleActionConfirmView();
        battleActionConfirmView.SetMessage(message);
    }

    /// <summary>
    /// 확정 대기 단계에서는 이동 범위 단계로 돌아가고, 범위 단계에서는 표시를 완전히 닫는다.
    /// </summary>
    public void CancelMoveSelection()
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

        EnsureBattleMovementController();
        bool returnToMoveRange = battleMovementController.CancelSelection();
        ClearMoveArrow();

        if (confirmMoveButton != null)
        {
            confirmMoveButton.interactable = false;
        }

        if (quitMoveButton != null)
        {
            quitMoveButton.interactable = false;
        }

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
    }

    /// <summary>참조 Camera가 없거나 비활성화됐으면 현재 Main Camera를 다시 찾는다.</summary>
    private void RefreshCamera()
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
        {
            mainCamera = Camera.main;
        }
    }

    /// <summary>Raycast 전용 컴포넌트를 확보하고 현재 참조를 전달한다.</summary>
    private void EnsureBattleRaycaster()
    {
        battleRaycaster = BattleComponentResolver.GetOrAdd(gameObject, battleRaycaster);
        battleRaycaster.Configure(mainCamera, player, tileLayerMask);
    }

    /// <summary>범위 표시 전용 컴포넌트를 확보하고 현재 색상 혼합 설정을 전달한다.</summary>
    private void EnsureBattleRangeVisualizer()
    {
        battleRangeVisualizer = BattleComponentResolver.GetOrAdd(gameObject, battleRangeVisualizer);
        battleRangeVisualizer.Configure(rangeColorBlend, selectedColorBlend, landedColorBlend);
    }

    /// <summary>Player 이동·공격 범위 생성과 표시를 담당하는 모듈을 확보한다.</summary>
    private void EnsureBattlePlayerRangeController()
    {
        battlePlayerRangeController = BattleComponentResolver.GetOrAdd(gameObject, battlePlayerRangeController);
        EnsureBattleRangeVisualizer();
        battlePlayerRangeController.Configure(battleRangeVisualizer);
    }

    /// <summary>Player 이동 연출 컴포넌트를 확보하고 현재 이동 속도 설정을 전달한다.</summary>
    private void EnsureBattlePlayerMover()
    {
        battlePlayerMover = BattleComponentResolver.GetOrAdd(gameObject, battlePlayerMover);
        battlePlayerMover.Configure(player, secondsPerTile, moveSpeedMultiplier);
    }

    /// <summary>일반 이동 경로 검증, 이동 연출과 MP 차감을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void EnsureBattleMovementController()
    {
        EnsureBattlePlayerMover();

        battleMovementController = BattleComponentResolver.GetOrAdd(gameObject, battleMovementController);
        battleMovementController.Configure(player, battlePlayerMover);
    }

    /// <summary>기본 공격의 계획, 임시 이동과 MP 확정을 담당하는 기능 컴포넌트를 확보한다.</summary>
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

    private void HandleBasicAttackConfirmationRequested(string message)
    {
        if (confirmMoveButton != null)
            confirmMoveButton.interactable = true;
        if (quitMoveButton != null)
            quitMoveButton.interactable = true;

        SetActionConfirmText(message);
        SetMoveButtonGroupVisible(true);
    }

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

        CharacterMP playerMP = player != null ? player.GetComponent<CharacterMP>() : null;
        Debug.Log(
            $"기본 공격 확정: 이동 {result.MovementMPCost}MP + 공격 {result.ActionMPCost}MP, " +
            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}. 피해 적용은 아직 연결하지 않았습니다.",
            this);
        ShowMoveRange();
    }

    private void HandleBasicAttackCancelled()
    {
        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(string.Empty);
        ShowMoveRange();
    }

    /// <summary>카드 대상 선택, 사거리 표시와 MP·손패 확정을 담당하는 기능 컴포넌트를 확보한다.</summary>
    private void EnsureBattleCardActionController()
    {
        EnsureBattleRangeVisualizer();

        battleCardActionController = BattleComponentResolver.GetOrAdd(gameObject, battleCardActionController);
        battleCardActionController.Configure(
            player,
            battleRangeVisualizer,
            cardRangeTileColor,
            cardEffectAreaTileColor,
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

    private void HandleCardConfirmationRequested(string message)
    {
        if (confirmMoveButton != null)
            confirmMoveButton.interactable = true;
        if (quitMoveButton != null)
            quitMoveButton.interactable = true;

        SetActionConfirmText(message);
        SetMoveButtonGroupVisible(true);
    }

    private void HandleCardConfirmed(BattleActionResult result)
    {
        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(string.Empty);
        FindFirstObjectByType<BattleCardPanelToggle>()?.Hide();
        ActionConfirmed?.Invoke(result);

        CharacterMP playerMP = player != null ? player.GetComponent<CharacterMP>() : null;
        Debug.Log(
            $"카드 사용 확정: {result.Request.DisplayName}, 소모 {result.ActionMPCost}MP, " +
            $"남은 MP {(playerMP != null ? playerMP.CurrentMP : 0)}.",
            this);
    }

    private void HandleCardCancelled()
    {
        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(string.Empty);
    }

    /// <summary>이동 목적지 화살표 표시 컴포넌트를 확보하고 프리팹 설정을 전달한다.</summary>
    private void EnsureBattleMovePreview()
    {
        battleMovePreview = BattleComponentResolver.GetOrAdd(gameObject, battleMovePreview);
        battleMovePreview.Configure(moveArrowPrefab, arrowOffset);
    }

    /// <summary>공용 확인·취소 화면 컴포넌트를 확보하고 UI 참조와 행동을 연결한다.</summary>
    private void EnsureBattleActionConfirmView()
    {
        battleActionConfirmView = BattleComponentResolver.GetOrAdd(gameObject, battleActionConfirmView);
        battleActionConfirmView.Bind(
            confirmMoveButton,
            quitMoveButton,
            moveButtonGroup,
            actionConfirmText,
            ConfirmMove,
            CancelMoveSelection);

        confirmMoveButton = battleActionConfirmView.ConfirmButton;
        quitMoveButton = battleActionConfirmView.CancelButton;
        moveButtonGroup = battleActionConfirmView.ButtonGroup;
        actionConfirmText = battleActionConfirmView.MessageText;
    }

    /// <summary>Player 원시 입력 감지 컴포넌트를 확보하고 입력 요청 이벤트를 연결한다.</summary>
    private void EnsureBattlePlayerInputReader()
    {
        battlePlayerInputReader = BattleComponentResolver.GetOrAdd(gameObject, battlePlayerInputReader);
        battlePlayerInputReader.LeftClickRequested -= HandleLeftClick;
        battlePlayerInputReader.DoubleLeftClickRequested -= HandleDebugDoubleClick;
        battlePlayerInputReader.RightClickRequested -= HandleRightClick;
        battlePlayerInputReader.CancelRequested -= HandleCancelInput;
        battlePlayerInputReader.RangeToggleRequested -= HandleRangeToggleRequested;
        battlePlayerInputReader.LeftClickRequested += HandleLeftClick;
        battlePlayerInputReader.DoubleLeftClickRequested += HandleDebugDoubleClick;
        battlePlayerInputReader.RightClickRequested += HandleRightClick;
        battlePlayerInputReader.CancelRequested += HandleCancelInput;
        battlePlayerInputReader.RangeToggleRequested += HandleRangeToggleRequested;
    }

    /// <summary>현재 전투 카메라와 Player를 진영별 마우스 오버 강조 모듈에 연결한다.</summary>
    private void EnsureBattleUnitHoverHighlighter()
    {
        battleUnitHoverHighlighter = BattleComponentResolver.GetOrAdd(
            gameObject,
            battleUnitHoverHighlighter);
        battleUnitHoverHighlighter.Configure(mainCamera, player);
    }

    private void EnsureBattleMoveThreatPreview()
    {
        battleMoveThreatPreview = BattleComponentResolver.GetOrAdd(
            gameObject,
            battleMoveThreatPreview);
        battleMoveThreatPreview.Configure(
            mainCamera,
            battleRaycaster,
            battlePlayerRangeController);
    }

    /// <summary>카드 Confirm 전에 밀치기 결과를 표시할 전용 View를 확보한다.</summary>
    private void EnsureBattlePushPreviewView()
    {
        battlePushPreviewView = BattleComponentResolver.GetOrAdd(
            gameObject,
            battlePushPreviewView);
        battlePushPreviewView.Configure(mainCamera);
    }

    /// <summary>확정/취소 부모를 우선 토글하고, 참조가 없으면 개별 버튼을 토글한다.</summary>
    private void SetMoveButtonGroupVisible(bool visible)
    {
        EnsureBattleActionConfirmView();
        battleActionConfirmView.SetVisible(visible);
    }

    /// <summary>현재 생성된 MapInfo 목록을 다시 수집하고 원본 Material 색상을 보관한다.</summary>
    private void RefreshMapTiles()
    {
        ResolveBattleDataPool();
        EnsureBattlePlayerMapContext();
        EnsureBattleRangeVisualizer();
        battlePlayerMapContext.Refresh(battleDataPool, battleRangeVisualizer);
    }

    /// <summary>씬 설치기가 등록한 전투 데이터 저장소를 참조한다.</summary>
    private void ResolveBattleDataPool()
    {
        if (battleDataPool == null)
        {
            battleDataPool = FindFirstObjectByType<BattleDataPool>(FindObjectsInactive.Include);
        }
    }

    /// <summary>Player 맵 타일 수집과 최근접 타일 조회 모듈을 확보한다.</summary>
    private void EnsureBattlePlayerMapContext()
    {
        battlePlayerMapContext = BattleComponentResolver.GetOrAdd(gameObject, battlePlayerMapContext);
    }

    /// <summary>원본색과 표시색을 지정 강도로 혼합해 원본 맵 가시성을 유지한다.</summary>
    private void SetTileColor(MapInfo tile, Color color, float blendStrength)
    {
        EnsureBattleRangeVisualizer();
        battleRangeVisualizer.SetTileColor(tile, color, blendStrength);
    }

    /// <summary>플레이어 행동 제어기가 변경한 모든 타일 Renderer 색상을 원본으로 복구한다.</summary>
    private void RestoreAllTileColors()
    {
        EnsureBattleRangeVisualizer();
        battleRangeVisualizer.RestoreAllTileColors();
    }

    /// <summary>타일 색상, 도달 가능 집합, 전역 범위 표시 상태를 초기화한다.</summary>
    private void ClearMoveRange()
    {
        RestoreAllTileColors();
        EnsureBattlePlayerRangeController();
        battlePlayerRangeController.ClearState();
        SetRangeVisible(false);
    }

    /// <summary>AI 경로 Debug 표시와 충돌하지 않도록 범위 가시성 변경 이벤트를 보낸다.</summary>
    private void SetRangeVisible(bool visible)
    {
        rangeVisible = visible;

        if (IsMoveRangeVisible == visible)
        {
            return;
        }

        IsMoveRangeVisible = visible;
        MoveRangeVisibilityChanged?.Invoke(visible);
    }

    /// <summary>목적지 위에 화살표 프리팹을 생성 또는 재사용해 표시한다.</summary>
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
}
