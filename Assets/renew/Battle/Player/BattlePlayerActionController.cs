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
    [SerializeField] internal BattleRaycaster battleRaycaster;
    [InspectorName("전투 범위 표시 모듈")]
    [SerializeField] internal BattleRangeVisualizer battleRangeVisualizer;
    [InspectorName("플레이어 범위 제어 모듈")]
    [SerializeField] internal BattlePlayerRangeController battlePlayerRangeController;
    [InspectorName("플레이어 이동 실행 모듈")]
    [SerializeField] internal BattlePlayerMover battlePlayerMover;
    [InspectorName("행동 확인 화면 모듈")]
    [SerializeField] private BattleActionConfirmView battleActionConfirmView;
    [InspectorName("플레이어 입력 감지 모듈")]
    [SerializeField] private BattlePlayerInputReader battlePlayerInputReader;
    [InspectorName("캐릭터 마우스 오버 강조 모듈")]
    [SerializeField] private BattleUnitHoverHighlighter battleUnitHoverHighlighter;
    [InspectorName("밀치기 결과 사전 예고 화면")]
    [SerializeField] internal BattlePushPreviewView battlePushPreviewView;
    [InspectorName("전투 데이터 저장소")]
    [SerializeField] internal BattleDataPool battleDataPool;
    [InspectorName("이동 플로우 모듈")]
    [SerializeField] internal BattleUnitMoveFlow moveFlow;
    [InspectorName("기본 공격 플로우 모듈")]
    [SerializeField] private BattleUnitAttackFlow attackFlow;
    [InspectorName("카드 플로우 모듈")]
    [SerializeField] private BattleUnitCardFlow cardFlow;

    [Header("이동 연출 시간")]
    [InspectorName("타일당 기본 이동 시간")]
    public float secondsPerTile = 1f;
    [InspectorName("이동 속도 배율")]
    public float moveSpeedMultiplier = 4f;

    [Header("이동 범위 색상")]
    // 이전엔 색상 9개가 이 컨트롤러 필드로 직접 박혀 있었는데, 다른 컨트롤러/Scene에서도 같은 팔레트를
    // 재사용할 수 있도록 BattleRangeColorPalette(ScriptableObject) 에셋 하나로 분리했다.
    [InspectorName("색상 팔레트")]
    public BattleRangeColorPalette colorPalette;
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

    internal BattlePlayerMapContext battlePlayerMapContext;
    internal readonly BattlePlayerTurnActionState turnActionState = new BattlePlayerTurnActionState();
    private bool rangeVisible;
    /// <summary>R 단축키로 범위 표시를 켠 상태인지 기억한다. 이동으로 범위가 잠깐 꺼져도 이동 후 자동으로 다시 켠다.</summary>
    internal bool rangeToggleActive;
    internal PlayerCombatData playerCombatData;

    /// <summary>전투 정지·재개 시 Player의 마우스와 키보드 입력 감지를 함께 켜거나 끕니다.</summary>
    public void SetBattleInputEnabled(bool enabled)
    {
        EnsureBattlePlayerInputReader();
        battlePlayerInputReader.enabled = enabled;
    }

    private bool IsBasicAttackActive => attackFlow != null && attackFlow.IsActive;

    /// <summary>이동 실행 코루틴 또는 기본 공격 실행이 진행 중인지. BattleUnitCardFlow의 우클릭 대상
    /// 선택 처리에서도 같은 판단이 필요해 internal로 열어뒀다.</summary>
    internal bool IsAnyActionMoving =>
        (moveFlow != null && moveFlow.IsMoving) ||
        (attackFlow != null && attackFlow.IsExecuting);

    private bool IsCardActionActive => cardFlow != null && cardFlow.IsActive;

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
        EnsureMoveFlow();
        EnsureAttackFlow();
        EnsureBattlePushPreviewView();
        EnsureCardFlow();
        EnsureBattlePlayerInputReader();
        EnsureBattleUnitHoverHighlighter();

        EnsureBattleActionConfirmView();

        SetConfirmButtonsInteractable(false);
        SetMoveButtonGroupVisible(false);

        if (colorPalette == null)
        {
            Debug.LogError("색상 팔레트(BattleRangeColorPalette)가 연결되지 않았습니다.", this);
        }
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
            battlePlayerInputReader.RightClickRequested -= HandleRightClick;
            battlePlayerInputReader.CancelRequested -= HandleCancelInput;
            battlePlayerInputReader.RangeToggleRequested -= HandleRangeToggleRequested;
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
        EnsureMoveFlow();
        playerCombatData = player != null ? player.GetComponent<PlayerCombatData>() : null;
        EnsureAttackFlow();
        EnsureCardFlow();
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
        EnsureMoveFlow();
        moveFlow.ShowMoveRange();
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
        if (cardFlow.IsAwaitingConfirmation)
        {
            cardFlow.Confirm();
            return;
        }

        if (attackFlow.IsAwaitingConfirmation)
        {
            attackFlow.Confirm();
            return;
        }

        EnsureMoveFlow();
        moveFlow.ConfirmSelectedMove();
    }

    /// <summary>새 Player 턴에 주사위, 이동 사용, 선택, 표시 상태를 전부 초기화한다.</summary>
    public void ResetTurnMoveState()
    {
        // R 토글은 사용자가 직접 끄거나(CancelMoveSelection의 완전 닫힘 분기) 명시적으로 끄기 전까지는
        // 턴이 바뀌어도 유지한다. 여기서는 상태만 초기화하고 필요하면 마지막에 다시 켠다.
        turnActionState.Reset();
        EnsureMoveFlow();
        moveFlow.ResetTurn();
        EnsureBattlePlayerRangeController();
        battlePlayerRangeController.ClearState();
        attackFlow.ResetTurn();
        cardFlow.ResetTurn();

        SetConfirmButtonsInteractable(false);
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
            if (moveFlow.IsAwaitingConfirmation)
            {
                Debug.Log($"플레이어 클릭으로 이동 확정: {moveFlow.PendingTarget?.name}", clickedPlayer);
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
                moveFlow.ShowMoveRange();
                ShowEnemyThreatRange(true);
            }
            else
                moveFlow.ShowMoveRange();
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
                moveFlow.SelectMoveTile(clickedTile);
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
        if (moveFlow.IsAwaitingConfirmation || IsBasicAttackActive)
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
        if (cardFlow.IsSelectingTarget)
        {
            cardFlow.HandleTargetRightClick(pointerPosition);
            return;
        }

        if (!turnActionState.DiceRolled || !rangeVisible ||
            IsAnyActionMoving || moveFlow.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)
        {
            return;
        }

        if (BattlePlayerInputReader.IsPointerOverInteractiveUI(pointerPosition))
        {
            return;
        }

        if (TryRaycastEnemy(pointerPosition, out EnemyTurnActor enemy))
        {
            attackFlow.TryBegin(enemy);
            return;
        }

        // 일반 이동 목적지는 좌클릭으로만 선택한다. 빈 바닥 우클릭은 현재 이동 선택을
        // 변경하거나 범위를 닫지 않는다.
    }

    /// <summary>Player 본체 또는 자식 Collider가 클릭됐는지 검사한다.</summary>
    private bool TryRaycastPlayer(Vector2 pointerPosition, out GameObject clickedPlayer)
    {
        RefreshCamera();
        EnsureBattleRaycaster();
        return battleRaycaster.TryGetPlayer(pointerPosition, out clickedPlayer);
    }

    /// <summary>마우스 아래 Collider의 부모에서 활성 EnemyTurnActor를 찾는다.</summary>
    internal bool TryRaycastEnemy(Vector2 pointerPosition, out EnemyTurnActor enemy)
    {
        RefreshCamera();
        EnsureBattleRaycaster();
        return battleRaycaster.TryGetEnemy(pointerPosition, out enemy);
    }

    /// <summary>마우스 아래 충돌체의 부모에서 MapInfo를 찾는다.</summary>
    internal bool TryRaycastMapTile(Vector2 pointerPosition, out MapInfo tile)
    {
        RefreshCamera();
        EnsureBattleRaycaster();
        return battleRaycaster.TryGetMapTile(pointerPosition, out tile);
    }

    /// <summary>
    /// R 단축키 전용 표시. Player 자신의 이동·공격 범위 대신
    /// 활성 Enemy들이 이번 턴에 실제로 위협할 수 있는 타일을 계산해 보여준다.
    /// </summary>
    internal void ShowEnemyThreatRange(bool preserveMoveRange = false)
    {
        RefreshMapTiles();
        EnsureBattlePlayerRangeController();
        if (!preserveMoveRange)
        {
            RestoreAllTileColors();
            battlePlayerRangeController.ClearState();
        }
        ResolveBattleDataPool();
        // BuildAndShow와 동일하게, R 위협 범위 표시도 occupiedEnemyTiles를 먼저 갱신해둬야 두 표시 모드가
        // 같은 소스로 "Enemy가 어느 타일에 서 있는가"를 판단한다(BattlePlayerRangeController 쪽 통일 작업과 짝).
        battlePlayerRangeController.RefreshOccupiedEnemyTiles(battleDataPool, FindClosestMapTile);

        IEnumerable<GameObject> enemies = battleDataPool != null && battleDataPool.Units != null
            ? battleDataPool.Units.Enemies
            : null;
        bool shown = battlePlayerRangeController.BuildAndShowEnemyThreatRange(
            BattleMapTraversalService.IsWalkable,
            enemies,
            FindClosestMapTile,
            colorPalette.EnemyThreatTileColor);
        SetRangeVisible(preserveMoveRange ? rangeVisible || shown : shown);
    }

    /// <summary>월드 좌표와 XZ 평면상 가장 가까운 MapInfo 타일을 찾는다.</summary>
    internal MapInfo FindClosestMapTile(Vector3 worldPosition)
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
    internal int GetPlayerAttackRange()
    {
        if (playerCombatData == null && player != null)
        {
            playerCombatData = player.GetComponent<PlayerCombatData>();
        }

        return playerCombatData != null ? playerCombatData.BasicAttackRangeTiles : 1;
    }

    /// <summary>
    /// BattleCardHandView가 손패 클릭을 전달하는 카드 행동의 공개 진입점이다.
    /// 이동·평타·다른 카드 행동과 충돌하지 않는지만 검사하고,
    /// 실제 카드 대상 선택 시작은 BattleUnitCardFlow.TryStartSelectedCardUse()에 위임한다.
    /// </summary>
    public bool BeginCardUseConfirmation(
        SelectedCardUseInfo cardUse,
        BattleCardDrawSystem cardDrawSystem)
    {
        if (cardUse == null || cardDrawSystem == null || player == null ||
            IsAnyActionMoving || moveFlow.IsAwaitingConfirmation || IsBasicAttackActive || IsCardActionActive)
        {
            return false;
        }

        EnsureCardFlow();
        return cardFlow.TryStartSelectedCardUse(cardUse, cardDrawSystem);
    }

    /// <summary>이동 또는 공격 확정 단계의 안내 문구를 전투 확인 텍스트에 표시한다.</summary>
    internal void SetActionConfirmText(string message)
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
            cardFlow.Cancel();
            return;
        }

        if (attackFlow.IsAwaitingConfirmation)
        {
            attackFlow.Cancel();
            return;
        }

        EnsureMoveFlow();
        moveFlow.CancelSelectedMove();
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
    internal void EnsureBattleRaycaster()
    {
        battleRaycaster = BattleComponentResolver.GetOrAdd(gameObject, battleRaycaster);
        battleRaycaster.AttachReferences(mainCamera, player, tileLayerMask);
    }

    /// <summary>범위 표시 전용 컴포넌트를 확보하고 현재 색상 혼합 설정을 전달한다.</summary>
    internal void EnsureBattleRangeVisualizer()
    {
        battleRangeVisualizer = BattleComponentResolver.GetOrAdd(gameObject, battleRangeVisualizer);
        battleRangeVisualizer.SetBlendStrengths(rangeColorBlend, selectedColorBlend, landedColorBlend);
    }

    /// <summary>Player 이동·공격 범위 생성과 표시를 담당하는 모듈을 확보한다.</summary>
    internal void EnsureBattlePlayerRangeController()
    {
        battlePlayerRangeController = BattleComponentResolver.GetOrAdd(gameObject, battlePlayerRangeController);
        EnsureBattleRangeVisualizer();
        battlePlayerRangeController.AttachVisualizer(battleRangeVisualizer);
    }

    /// <summary>Player 이동 연출 컴포넌트를 확보하고 현재 이동 속도 설정을 전달한다.</summary>
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

    /// <summary>기본 공격 플로우 전담 컴포넌트(BattleUnitAttackFlow)를 확보하고 소유자 참조를 연결한다.</summary>
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
        battlePlayerInputReader.RightClickRequested -= HandleRightClick;
        battlePlayerInputReader.CancelRequested -= HandleCancelInput;
        battlePlayerInputReader.RangeToggleRequested -= HandleRangeToggleRequested;
        battlePlayerInputReader.LeftClickRequested += HandleLeftClick;
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
        battleUnitHoverHighlighter.AttachReferences(mainCamera, player);
    }

    /// <summary>카드 Confirm 전에 밀치기 결과를 표시할 전용 View를 확보한다.</summary>
    internal void EnsureBattlePushPreviewView()
    {
        battlePushPreviewView = BattleComponentResolver.GetOrAdd(
            gameObject,
            battlePushPreviewView);
        battlePushPreviewView.ConfigurePreviewDependencies(mainCamera);
    }

    /// <summary>확정/취소 부모를 우선 토글하고, 참조가 없으면 개별 버튼을 토글한다.</summary>
    internal void SetMoveButtonGroupVisible(bool visible)
    {
        EnsureBattleActionConfirmView();
        battleActionConfirmView.SetVisible(visible);
    }

    /// <summary>
    /// 확정(confirmMoveButton)·취소(quitMoveButton) 버튼의 클릭 가능 여부를 null 체크와 함께 한 번에 설정한다.
    /// 이동/공격/카드/턴 초기화 등 여러 곳에서 반복되던 "두 버튼 interactable 토글" 패턴을 여기 하나로 모았다.
    /// </summary>
    internal void SetConfirmButtonsInteractable(bool interactable)
    {
        if (confirmMoveButton != null)
        {
            confirmMoveButton.interactable = interactable;
        }

        if (quitMoveButton != null)
        {
            quitMoveButton.interactable = interactable;
        }
    }

    /// <summary>
    /// 기본 공격·카드 사용 모두에서 "확인 대기" 단계로 들어갈 때 공통으로 하는 UI 처리
    /// (확정/취소 버튼 활성화 + 안내 문구 표시 + 버튼 그룹 표시)를 한 곳으로 모은 헬퍼.
    /// HandleBasicAttackConfirmationRequested/HandleCardConfirmationRequested가 완전히 동일한
    /// 본문을 갖고 있던 것을 여기로 합쳤다.
    /// </summary>
    internal void ShowActionConfirmationUI(string message)
    {
        SetConfirmButtonsInteractable(true);
        SetActionConfirmText(message);
        SetMoveButtonGroupVisible(true);
    }

    /// <summary>
    /// 확인 대기 UI(버튼 그룹 + 안내 문구)를 닫는 공통 처리. 기본 공격/카드 취소 핸들러가
    /// 각자 이 두 줄을 반복하던 것을 모았다 — 기본 공격 취소는 여기에 더해 ShowMoveRange()를 추가로 부른다.
    /// </summary>
    internal void HideActionConfirmationUI()
    {
        SetMoveButtonGroupVisible(false);
        SetActionConfirmText(string.Empty);
    }

    /// <summary>현재 생성된 MapInfo 목록을 다시 수집하고 원본 Material 색상을 보관한다.</summary>
    internal void RefreshMapTiles()
    {
        ResolveBattleDataPool();
        EnsureBattlePlayerMapContext();
        EnsureBattleRangeVisualizer();
        battlePlayerMapContext.Refresh(battleDataPool, battleRangeVisualizer);
    }

    /// <summary>씬 설치기가 등록한 전투 데이터 저장소를 참조한다.</summary>
    internal void ResolveBattleDataPool()
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

    /// <summary>플레이어 행동 제어기가 변경한 모든 타일 Renderer 색상을 원본으로 복구한다.</summary>
    internal void RestoreAllTileColors()
    {
        EnsureBattleRangeVisualizer();
        battleRangeVisualizer.RestoreAllTileColors();
    }

    /// <summary>AI 경로 Debug 표시와 충돌하지 않도록 범위 가시성 변경 이벤트를 보낸다.</summary>
    internal void SetRangeVisible(bool visible)
    {
        rangeVisible = visible;
        // BattleRangeVisibilityTracker.SetVisible이 이전 값과 같으면 무시하므로 여기서 따로 비교하지 않는다.
        BattleRangeVisibilityTracker.SetVisible(visible);
    }

}
