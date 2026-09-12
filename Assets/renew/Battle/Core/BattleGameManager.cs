using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>전투 시작과 종료, Player/Enemy 턴 전환을 연결합니다.</summary>
[DisallowMultipleComponent]
public class BattleGameManager : MonoBehaviour
{

    public static BattleGameManager Instance { get; private set; }

    [Header("코드 리뷰 확인")]
    [Tooltip("이 체크는 게임 동작에 영향을 주지 않습니다. BattleGameManager가 턴 전환, 주사위 상태, 전투 조작 차단 UI, Player 등록을 연결한다는 설명을 리뷰어가 확인했을 때 체크합니다.")]
    [InspectorName("코드 역할 확인 완료")]
    [SerializeField] private bool CODE_EXPLAIN;
    /// <summary>Inspector에서 이 클래스의 역할 설명을 확인했는지 표시하는 리뷰 메타데이터다.</summary>
    public bool IsCodeExplanationReviewed => CODE_EXPLAIN;

    [Header("턴 진행 모듈")]
    [InspectorName("턴 버튼 제어 모듈")]
    [SerializeField] private BattleTurnButtonController turnButtonController;
    [InspectorName("카드 패널 표시 제어 모듈")]
    [Tooltip("주사위를 굴리면 손패를 열고 새 턴 또는 적 턴에는 숨길 카드 패널입니다. 턴 버튼 제어기를 경유하지 않고 직접 제어합니다.")]
    [SerializeField] private BattleCardPanelToggle cardPanelToggle;
    [InspectorName("오버레이 UI 제어 모듈")]
    [SerializeField] private BattleOverlayUiController overlayUi;
    [InspectorName("플레이어 체력 연결 모듈")]
    [SerializeField] private BattlePlayerHealthBinding playerHealthBinding;

    [Header("플레이어 런타임 참조")]
    [InspectorName("플레이어 행동 제어기")]
    [SerializeField] private BattlePlayerActionController playerActionController;
    [InspectorName("플레이어 런타임 연결 모듈")]
    [SerializeField] private BattlePlayerRegistrationBinder playerRuntimeBinder;
    [InspectorName("카드 드로우 시스템")]
    [SerializeField] private BattleCardDrawSystem cardDrawSystem;
    [InspectorName("상자 보상 시스템")]
    [SerializeField] private BattleChestRewardSystem chestRewardSystem;
    [InspectorName("카드 상점 시스템")]
    [SerializeField] private BattleCardShopSystem cardShopSystem;
    [InspectorName("전투 데이터 저장소")]
    [SerializeField] private BattleDataPool battleDataPool;
    [InspectorName("적 턴 순차 실행 모듈")]
    [Tooltip("등록된 Enemy를 순서대로 한 명씩 실행하고 각 행동이 끝날 때까지 기다립니다. Player/Enemy 턴 전환 자체는 BattleGameManager가 담당합니다.")]
    [SerializeField] private BattleEnemyTurnRunner enemyTurnRunner;
    [InspectorName("카드 효과 주사위 시스템")]
    [Tooltip("같은 전투 Manager 오브젝트에 있는 BattleDiceSystem입니다. 카드 사용마다 효과 굴림을 실행합니다.")]
    [SerializeField] private BattleDiceSystem diceSystem;
    [Header("턴 안내 직접 참조")]
    [SerializeField] private BattleTurnAnnouncementView turnAnnouncementView;
    [Tooltip("Player 턴 전환 때 화면을 어둡게 만드는 기존 LoadingUI입니다.")]
    [SerializeField] private LoadingUI turnTransitionFade;

    [Header("전투 진행 상태")]
    [SerializeField] private BattleTurnState turnState;
    [SerializeField] private BattleStageState stageState;

    [Header("QA Debug")]
    [FormerlySerializedAs("enableDebugQaBoost")]
    [InspectorName("QA 자원 보정 사용")]
    [SerializeField] private bool enableQaStats = true;
    [InspectorName("QA 텔레포트 사용")]
    [SerializeField] private bool enableQaTeleport = true;
    [InspectorName("QA 최대 MP")]
    [FormerlySerializedAs("debugPlayerMaxMP")]
    [SerializeField, Range(1, 10)] private int qaMaxMP = 10;
    [InspectorName("QA 최대 AP")]
    [FormerlySerializedAs("debugPlayerMaxAP")]
    [FormerlySerializedAs("debugMaxAP")]
    [FormerlySerializedAs("debugMaxMoveRange")]
    [SerializeField, Range(1, 12)] private int qaMaxAP = 6;
    private BattleQaTeleportController qaTeleportController;

    [Header("전투 상태 (런타임 확인용)")]
    [InspectorName("전투 정지 여부")]
    [FormerlySerializedAs("battleStopped")]
    [SerializeField] private bool isBattleStopped;

    /// <summary>Player 등록이 끝난 뒤 카메라·Enemy 감지기 등에 생성된 Player 인스턴스를 전달한다.</summary>
    public event System.Action<GameObject> PlayerRegistered;

    /// <summary>Player 턴 자원과 입력 상태 초기화가 끝난 뒤 카드 드로우 등 후속 시스템에 알린다.</summary>
    public event System.Action PlayerTurnStarted;

    /// <summary>턴·주사위·전투 조작 차단 UI 상태가 바뀌어 카드 사용 가능 여부가 달라졌음을 알린다.</summary>
    public event System.Action<bool> CardUseAvailabilityChanged;

    /// <summary>SpawnPlayer가 생성하고 RegisterPlayer가 등록한 실제 전투 Player 오브젝트다.</summary>
    public GameObject CurrentPlayer => playerRuntimeBinder?.Player;
    /// <summary>카드와 기본 공격에 사용하는 현재 Player의 MP입니다.</summary>
    public BattleUnitMP CurrentPlayerMP => playerRuntimeBinder?.MP;
    /// <summary>이동에 사용하는 현재 Player의 AP입니다.</summary>
    public BattleUnitAP CurrentPlayerAP => playerRuntimeBinder?.AP;
    /// <summary>현재 Player의 공격력과 사거리 등 전투 계산에 필요한 읽기 전용 기준 데이터다.</summary>
    public PlayerCombatData CurrentPlayerCombatData => playerRuntimeBinder?.CombatData;
    /// <summary>현재 등록된 Player가 직접 소유하는 장비 슬롯과 장비 스탯이다.</summary>
    public PlayerWeapon CurrentPlayerWeapon => playerRuntimeBinder?.Weapon;
    /// <summary>현재 등록된 Player가 직접 소유하는 골드 지갑이다.</summary>
    public PlayerWallet CurrentPlayerWallet => playerRuntimeBinder?.Wallet;
    /// <summary>현재 등록된 플레이어의 체력 컴포넌트를 참조한다.</summary>
    public BattleHealth CurrentPlayerHealth => playerHealthBinding?.CurrentHealth;
    /// <summary>전투 시작과 Player 턴 시작에 손패를 구성하는 기존 카드 드로우 시스템 참조다.</summary>
    public BattleCardDrawSystem CardDrawSystem => cardDrawSystem;
    /// <summary>맵의 보상 상자를 열고 닫는 시스템. Player 사망 시 열린 UI를 강제로 닫는다.</summary>
    public BattleChestRewardSystem ChestRewardSystem => chestRewardSystem;
    /// <summary>맵 상점 진입·판매·구매를 담당하는 시스템. Player 사망 시 열린 UI를 강제로 닫는다.</summary>
    public BattleCardShopSystem CardShopSystem => cardShopSystem;
    public BattleDiceSystem DiceSystem => diceSystem;
    public bool IsBattleStopped => isBattleStopped;
    public bool IsQaStatsEnabled => enableQaStats;
    public int CurrentTurn => turnState != null ? turnState.Turn : 1;
    public int CurrentStage => stageState != null ? stageState.Stage : 1;
    /// <summary>상점·보상창처럼 뒤쪽 전투 조작을 막는 UI가 하나 이상 열려 있는지 나타낸다.</summary>
    public bool IsBattleBlockingUiOpen => overlayUi != null && overlayUi.IsOverlayOpen;

    /// <summary>기존 호출부 호환용 이름. 새 코드에서는 <see cref="IsBattleBlockingUiOpen"/>을 사용한다.</summary>
    public bool IsModalInteractionOpen => IsBattleBlockingUiOpen;
    public bool CanUsePlayerCards =>
        !IsBattleStopped && turnState != null && turnState.IsPlayerTurn && !IsBattleBlockingUiOpen &&
        (diceSystem == null || !diceSystem.IsRolling);


    /// <summary>상점이나 보상창이 열리면 뒤쪽 전투 입력을 잠급니다.</summary>
    public void LockBattleInputForOverlay()
    {
        overlayUi.RegisterOpenedOverlayAndLockInput();
        UpdateTurnUI();
    }

    /// <summary>오버레이가 모두 닫히면 현재 턴에 맞춰 전투 입력을 복구합니다.</summary>
    public void UnlockBattleInputAfterOverlay()
    {
        overlayUi.RegisterClosedOverlayAndRestoreInput(turnState.IsPlayerTurn, IsBattleStopped);
        UpdateTurnUI();
    }

    /// <summary>
    /// 카드 효과 굴림을 기다리는 동안 월드 조작·카메라·턴 종료를 잠근다.
    /// RollButton은 HUD 입력을 받아야 하므로 Overlay의 CanvasGroup 잠금은 사용하지 않는다.
    /// </summary>
    public void LockBattleInputForCardRoll()
    {
        overlayUi?.LockForCardRoll();
    }

    /// <summary>카드 효과 적용이 끝난 뒤 다른 Overlay가 없을 때 현재 턴 입력을 복구한다.</summary>
    public void UnlockBattleInputAfterCardRoll()
    {
        overlayUi?.UnlockAfterCardRoll(turnState.IsPlayerTurn, IsBattleStopped);
        UpdateTurnUI();
    }

    /// <summary>상점 상태에 맞춰 뒤쪽 전투 UI를 숨기거나 복구합니다.</summary>
    public void SetShopOpen(bool shopIsOpen)
    {
        overlayUi.SetShopOpen(shopIsOpen);
        UpdateTurnUI();
    }


    /// <summary>전투 상태와 고정 컴포넌트를 초기화합니다.</summary>
    private void Awake()
    {
        // Scene에 Manager가 중복 배치되면 서로 다른 턴 상태가 동시에 진행되므로 뒤에 생성된 쪽을 제거한다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 다른 전투 컴포넌트가 공용 Manager를 찾을 수 있도록 이 Scene의 공식 인스턴스로 등록한다.
        Instance = this;
        // 현재 메인 Scene에서는 전투 Manager와 주사위 시스템이 같은 고정 오브젝트에 배치된다.
        // Scene 전체 검색 없이 같은 오브젝트의 구성만 한 번 연결한다.
        if (diceSystem == null)
            diceSystem = GetComponent<BattleDiceSystem>();
        // BattleTurnState/BattleStageState는 전투의 핵심 상태라 없으면 런타임에 새로 만들지 않고
        // 그대로 초기화를 멈춘다. Scene의 BattleGameManager 오브젝트에 미리 붙어 있어야 한다.
        turnState = GetComponent<BattleTurnState>();
        if (turnState == null)
        {
            Debug.LogError($"[{nameof(BattleGameManager)}] {gameObject.name}에 BattleTurnState가 없습니다. Scene에 미리 추가해야 합니다.", this);
            return;
        }

        stageState = GetComponent<BattleStageState>();
        if (stageState == null)
        {
            Debug.LogError($"[{nameof(BattleGameManager)}] {gameObject.name}에 BattleStageState가 없습니다. Scene에 미리 추가해야 합니다.", this);
            return;
        }

        overlayUi?.SetupCardRollUi(cardPanelToggle, turnButtonController);
        stageState.LoadSavedStage();
        turnState.ResetTurn();
        isBattleStopped = false;
        Time.timeScale = 1f;
        // 정적 로그 저장소는 Scene을 다시 열어도 유지될 수 있으므로 새 전투 시작 시 비운다.
        BattleCombatLog.ClearAllEntries();

        // 죽음을 판정하는 초기화가 아니다. BattleHealth.Died 이벤트가 발생했을 때
        // 이 Manager의 HandlePlayerDied가 호출되도록 콜백만 연결한다.
        playerHealthBinding?.SetDeathHandler(HandlePlayerDied);

        // 누락된 Inspector 참조를 런타임 자동 생성으로 숨기지 않고 시작 즉시 Console에 표시한다.
        CheckRequiredReferences();

        // 동적 맵의 상점·상자 UI를 빠르게 검증할 수 있도록 Editor 전용 QA 텔레포트 입력을 연결한다.
        if (enableQaTeleport)
        {
            qaTeleportController = GetComponent<BattleQaTeleportController>();
            if (qaTeleportController == null)
            {
                Debug.LogError($"[{nameof(BattleGameManager)}] enableQaTeleport가 켜져 있는데 {gameObject.name}에 BattleQaTeleportController가 없습니다. Scene에 추가해야 합니다.", this);
            }
            else
            {
                qaTeleportController.Attach(this);
            }
        }

        // UI 모듈은 버튼 클릭을 해석하고, 실제 턴 규칙은 이 Manager의 공개 함수를 호출한다.
        turnButtonController?.SetEndTurnAction(EndTurn);

        // Scene에 저장된 최초 턴 상태를 버튼·카드 사용 가능 상태에 즉시 반영한다.
        UpdateTurnUI();
    }


    /// <summary>Player 턴을 끝내고 Enemy 턴을 시작합니다.</summary>
    public void EndTurn()
    {
        // 사망으로 전투가 멈췄거나 상점 등의 UI가 열려 있으면 뒤쪽 턴 입력을 받지 않는다.
        if (IsBattleStopped || IsBattleBlockingUiOpen)
        {
            return;
        }

        if (!turnState.TryStartEnemyTurn())
            return;

        cardPanelToggle?.Hide();
        UpdateTurnUI();

        StartCoroutine(RunEnemyTurnSequence());
    }

    /// <summary>Player 턴을 시작하고 전환 연출을 재생합니다.</summary>
    public void StartPlayerTurn()
    {
        StartPlayerTurn(showAnnouncement: true);
    }

    /// <summary>Player 턴 자원과 입력을 초기화합니다. false이면 전환 연출을 생략합니다.</summary>
    public void StartPlayerTurn(bool showAnnouncement)
    {
        // Player 사망 등으로 전투가 끝났다면 턴 자원과 입력을 다시 열지 않는다.
        if (isBattleStopped)
            return;

        turnState.StartPlayerTurn();

        // 기절 여부는 턴 시작 효과가 처리되기 전 값을 기준으로 이번 턴 건너뛰기를 결정한다.
        bool playerTurnSkipped = CurrentPlayer != null &&
            CurrentPlayer.GetComponent<BattleStatusEffects>()?.Has(BattleStatusType.Stun) == true;

        // 독·화상처럼 Player 턴 시작 시 발동하거나 남은 턴 수가 감소하는 모든 유닛의 상태효과를 한 번 진행한다.
        BattleStatusEffects.ProcessAllPlayerTurnStart();

        // 기절한 Player는 MP 회복, 주사위 입력, 카드 드로우 없이 바로 Enemy 턴으로 넘긴다.
        if (playerTurnSkipped)
        {
            turnState.SkipPlayerTurn();
            // 기절로 턴을 건너뛰어도 다음 Enemy 턴 MP는 새로 준비합니다.
            PrepareEnemyTurnMP();
            // 버튼과 카드 사용 가능 상태를 먼저 잠근 뒤 Enemy 순차 행동을 시작한다.
            UpdateTurnUI();
            StartCoroutine(RunEnemyTurnSequence());
            return;
        }

        // 보호막은 한 Player 턴만 유지되는 규칙이므로 새 Player 턴 시작 시 제거한다.
        CurrentPlayerHealth?.ClearShield();
        // 기본 공격·카드가 함께 쓰는 Player MP를 최대치까지 회복한다.
        CurrentPlayerMP?.RestoreFull();
        // 장비 변경으로 DEX가 달라질 수 있으므로 매 턴 최대 AP를 다시 계산합니다.
        UpdatePlayerAP();
        // Player가 미리 위협 정보를 확인할 수 있도록 다음 Enemy 턴의 MP를 지금 결정한다.
        PrepareEnemyTurnMP();
        // 이전 턴에서 남은 선택 타일, 이동 경로, 이동 완료 상태와 범위 표시를 지운다.
        ResetPlayerMovement();
        // Player 턴 시작과 동시에 이동과 카드 입력을 엽니다.
        OpenPlayerTurnControls();
        // 턴 종료 버튼, 카드 사용 가능 상태를 새 턴 값으로 갱신한다.
        UpdateTurnUI();

        // Manager의 턴 초기화가 전부 끝난 뒤 DrawSystem 등 구독자가 손패를 구성하게 한다.
        PlayerTurnStarted?.Invoke();
        // 화면 전투 로그에는 내부 초기화가 완료된 턴만 기록한다.
        BattleCombatLog.AddEntry($"TURN {CurrentTurn}  PLAYER TURN");
        // 첫 전투 진입은 기존 입장 흐름이 페이드 한 번을 이미 담당한다.
        // 호출 경로가 추가되더라도 TURN 1에서 두 번째 페이드가 발생하지 않게 방어한다.
        bool willPlayTurnTransition = showAnnouncement && CurrentTurn > 1;
        if (willPlayTurnTransition)
        {
            StartCoroutine(PlayPlayerTurnAnnouncementLocked(CurrentTurn));
            return;
        }

        // 전환 연출을 생략한 경우 입력을 여기서 바로 복구합니다.
        if (!IsBattleBlockingUiOpen)
        {
            playerActionController?.SetBattleInputEnabled(true);
            BattleMapCameraInput.SetEnabledOnMainCamera(true);

            BattleCameraRig cameraRig = Camera.main != null
                ? Camera.main.GetComponent<BattleCameraRig>()
                : null;
            cameraRig?.FocusPlayerImmediately();

            if (CurrentTurn > 1)
            {
                turnAnnouncementView?.StartPlayerTurnAnnouncement(CurrentTurn, 1f);
            }
        }
    }

    /// <summary>Player 턴 전환 연출 동안 전투 입력을 잠급니다.</summary>
    private IEnumerator PlayPlayerTurnAnnouncementLocked(int turn)
    {
        LockBattleInputForOverlay();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);

        if (turnTransitionFade != null)
        {
            yield return turnTransitionFade.FadeToBlackRoutine(0.15f);
        }

        BattleCameraRig cameraRig = Camera.main != null
            ? Camera.main.GetComponent<BattleCameraRig>()
            : null;
        cameraRig?.FocusPlayerImmediately();

        turnTransitionFade?.FadeIn(0.15f);
        yield return turnAnnouncementView.ShowPlayerTurnAnnouncementAndWait(turn, 1f);

        while (turnTransitionFade != null && turnTransitionFade.IsFading)
        {
            yield return null;
        }

        BattleMapCameraInput.SetEnabledOnMainCamera(true);
        UnlockBattleInputAfterOverlay();
    }

    /// <summary>현재 층 번호를 2초 동안 표시하고 연출 종료까지 기다린다. 표시 중에는 배경 입력과
    /// 카메라 드래그/줌/키보드 이동을 모두 잠근다.</summary>
    public IEnumerator PlayStageIntro()
    {
        LockBattleInputForOverlay();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);
        try { yield return turnAnnouncementView.ShowStageAnnouncement(CurrentStage, 2f); }
        finally
        {
            BattleMapCameraInput.SetEnabledOnMainCamera(true);
            UnlockBattleInputAfterOverlay();
        }
    }

    /// <summary>플레이어 턴 동안 다음 적 턴의 MP와 위협 범위를 확인할 수 있도록 모든 생존 적 MP를 준비한다.</summary>
    private void PrepareEnemyTurnMP()
    {
        if (battleDataPool != null && battleDataPool.Units != null)
        {
            battleDataPool.Units.RemoveMissingEnemies();
            foreach (GameObject enemy in battleDataPool.Units.Enemies)
            {
                if (enemy == null || !enemy.activeInHierarchy) continue;
                EnemyTurnActor actor = enemy.GetComponent<EnemyTurnActor>();
                if (actor == null) actor = enemy.GetComponentInChildren<EnemyTurnActor>();
                actor?.PrepareNextTurnMP();
            }
            return;
        }

        Debug.LogError("Enemy 준비 실패: BattleDataPool 또는 UnitRegistry 참조가 없습니다.", this);
    }


    /// <summary>생성된 Player를 전투 시스템에 등록하고 외부 시스템에 알립니다.</summary>
    public void RegisterPlayer(GameObject player)
    {
        if (playerRuntimeBinder == null || !playerRuntimeBinder.Register(
                player,
                CardDrawSystem,
                playerActionController,
                this))
        {
            playerHealthBinding?.Bind(null);
            return;
        }

        if (enableQaStats)
            playerRuntimeBinder.SetDebugResources(qaMaxMP, qaMaxAP);

        playerHealthBinding?.Bind(playerRuntimeBinder.Health);

        // 카메라와 Enemy 감지기처럼 Player 생성 시점을 기다리던 외부 시스템에 같은 인스턴스를 배포한다.
        PlayerRegistered?.Invoke(CurrentPlayer);
        Debug.Log($"전투 플레이어 등록 완료: {CurrentPlayer.name}", CurrentPlayer);
    }

    /// <summary>
    /// 플레이어 체력이 0이 되면 사망 연출·보상 등 후속 규칙을 아직 정하지 않았으므로
    /// 우선 게임을 그 자리에서 멈춘다(Time.timeScale = 0). 사용자 지정 임시 처리다.
    /// </summary>
    private void HandlePlayerDied(BattleHealth health)
    {
        if (IsBattleStopped)
        {
            return;
        }

        isBattleStopped = true;
        ChestRewardSystem?.ForceClose();
        CardShopSystem?.ForceClose();
        overlayUi.ResetOverlayInputState();
        Debug.Log("플레이어 체력이 0이 되어 게임을 정지합니다.", this);

        playerActionController?.SetBattleInputEnabled(false);

        turnButtonController?.DisableTurnEndInput();

        StopAllCoroutines();
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        playerRuntimeBinder?.Clear();
        overlayUi?.ResetOverlayInputState();
        playerHealthBinding?.ClearBinding();
        Instance = null;
    }

    /// <summary>현재 전투 스테이지를 변경하고 아직 남아 있는 구 진행 코드에도 같은 값을 동기화한다.</summary>
    public void SetCurrentStage(int stage)
    {
        stageState.SetStage(stage);
    }


    /// <summary>Player 이동 입력과 카드 패널을 엽니다.</summary>
    private void OpenPlayerTurnControls()
    {
        playerActionController?.ActivateMovement();
        cardPanelToggle?.Show();
    }

    /// <summary>현재 DEX 또는 QA 설정으로 최대 AP를 계산하고 완전히 회복합니다.</summary>
    private void UpdatePlayerAP()
    {
        if (CurrentPlayer == null || CurrentPlayerAP == null)
            return;

        int maxAP = enableQaStats
            ? qaMaxAP
            : PlayerStatCalculator.GetMaxAP(CurrentPlayer, CurrentPlayerAP.BaseAP);
        CurrentPlayerAP.ConfigureMaxAP(maxAP);
    }

    /// <summary>Enemy 턴 안내와 모든 Enemy 행동을 실행한 뒤 Player 턴으로 돌아갑니다.</summary>
    private IEnumerator RunEnemyTurnSequence()
    {
        // Enemy 턴이 시작되기 전에 Player 이동 미리보기를 지웁니다.
        playerActionController?.moveFlow?.MoveThreatPreview?.ClearSelectedDestination();

        // Enemy 턴 안내가 끝날 때까지 Player와 카메라 입력을 잠급니다.
        LockBattleInputForOverlay();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);
        int enemyRound = Mathf.Max(1, CurrentTurn - 1);
        BattleCombatLog.AddEntry($"TURN {enemyRound}  ENEMY TURN");
        yield return turnAnnouncementView.ShowEnemyTurnAnnouncementAndWait(enemyRound, 1f);
        UnlockBattleInputAfterOverlay();

        // 등록된 Enemy를 한 명씩 실행합니다.
        if (enemyTurnRunner == null)
        {
            Debug.LogError("Enemy 턴 실행기가 연결되지 않아 Player 턴으로 복귀합니다.", this);
            StartPlayerTurn(showAnnouncement: false);
            yield break;
        }
        yield return enemyTurnRunner.RunAll(battleDataPool);

        // 행동한 Enemy가 없으면 다음 Player 턴의 전환 연출을 생략합니다.
        StartPlayerTurn(showAnnouncement: enemyTurnRunner.AnyEnemyActedLastRun);
    }

    /// <summary>현재 턴과 주사위 상태에 맞춰 주사위 및 턴 종료 버튼 사용 여부를 갱신한다.</summary>
    private void UpdateTurnUI()
    {
        turnButtonController?.ApplyTurnEndButtonState(
            turnState.IsPlayerTurn,
            IsBattleStopped,
            IsBattleBlockingUiOpen);

        CardUseAvailabilityChanged?.Invoke(CanUsePlayerCards);
    }

    /// <summary>플레이어 턴 시작 시 이동 입력기의 선택, 이동, 범위 표시 상태를 초기화한다.</summary>
    private void ResetPlayerMovement()
    {
        if (playerActionController != null)
        {
            playerActionController.ResetPlayerTurnActions();
        }
    }

    /// <summary>필수 Inspector 참조가 빠졌으면 Console에 표시합니다.</summary>
    private void CheckRequiredReferences()
    {
        if (turnButtonController == null) Debug.LogError("턴 버튼 제어기 참조가 없습니다.", this);
        if (cardPanelToggle == null) Debug.LogError("카드 패널 표시 제어기 참조가 없습니다.", this);
        if (playerRuntimeBinder == null) Debug.LogError("Player 런타임 연결 모듈 참조가 없습니다.", this);
        if (cardDrawSystem == null) Debug.LogError("카드 드로우 시스템 참조가 없습니다.", this);
        if (chestRewardSystem == null) Debug.LogError("상자 보상 시스템 참조가 없습니다.", this);
        if (cardShopSystem == null) Debug.LogError("카드 상점 시스템 참조가 없습니다.", this);
        if (playerActionController == null) Debug.LogError("Player 행동 제어기 참조가 없습니다.", this);
        if (battleDataPool == null) Debug.LogError("전투 데이터 저장소 참조가 없습니다.", this);
        if (enemyTurnRunner == null) Debug.LogError("Enemy 턴 실행기 참조가 없습니다.", this);
        if (overlayUi == null) Debug.LogError("오버레이 UI 제어 모듈 참조가 없습니다.", this);
        if (playerHealthBinding == null) Debug.LogError("Player 체력 연결 모듈 참조가 없습니다.", this);
        if (turnAnnouncementView == null) Debug.LogError("턴 안내 View 참조가 없습니다.", this);
        if (turnTransitionFade == null) Debug.LogError("턴 전환 LoadingUI 참조가 없습니다.", this);
    }


}
