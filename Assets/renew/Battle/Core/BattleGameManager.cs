using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 전투 씬의 큰 진행 순서를 연결하는 최상위 조정자다.
/// Player/Enemy 턴 전환, 전투 조작 차단 UI, 생성된 Player 배포를 담당한다.
/// 이동 경로 계산, 카드 효과 계산, Enemy AI 판단은 직접 수행하지 않고 각 전용 컴포넌트에 위임한다.
///
/// 주요 호출 흐름:
/// BattleUIFlowController가 Player 등록 및 최초 StartPlayerTurn을 요청한다.
/// BattleDiceSystem의 연출 완료 신호로 이동 범위와 카드 패널을 연 뒤, EndTurn이 Enemy 순차 행동을 시작한다.
/// BattleEnemyTurnRunner가 모든 Enemy 행동을 마치면 다시 StartPlayerTurn으로 돌아온다.
/// 상점·보상·캐릭터 정보 UI는 LockBattleInputForOverlay/UnlockBattleInputAfterOverlay로 전투 입력을 잠근다.
///
/// 여기서 "전투 조작 차단 UI"는 상점·보상창처럼 열린 동안 뒤쪽 전투 화면을 누를 수 없는 UI를 뜻한다.
/// </summary>
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

    [Header("턴 진행 모듈")] // 턴/오버레이/플레이어 체력만 관리 현재는 BattleGameManager가 담당하지만, 추후 별도 조정자로 분리할 수 있다.
    [InspectorName("턴 버튼 제어 모듈")]
    [SerializeField] private BattleTurnButtonController turnButtonController;
    [InspectorName("주사위 버튼 입력·표시 모듈")]
    [Tooltip("주사위 버튼의 누르기 연출과 버튼 표시·활성 상태를 직접 관리합니다.")]
    [SerializeField] private BattleDiceRollButton diceRollButton;
    [InspectorName("주사위 규칙 시스템")]
    [Tooltip("이번 턴의 굴림 여부, 주사위 결과와 연출 완료 신호를 관리합니다.")]
    [SerializeField] private BattleDiceSystem diceSystem;
    [InspectorName("카드 패널 표시 제어 모듈")]
    [Tooltip("주사위를 굴리면 손패를 열고 새 턴 또는 적 턴에는 숨길 카드 패널입니다. 턴 버튼 제어기를 경유하지 않고 직접 제어합니다.")]
    [SerializeField] private BattleCardPanelToggle cardPanelToggle;
    [InspectorName("오버레이 UI 제어 모듈")]
    [SerializeField] private BattleOverlayUiController overlayUi;
    [InspectorName("플레이어 체력 연결 모듈")]
    [SerializeField] private BattlePlayerHealthBinding playerHealthBinding;

    [Header("플레이어 런타임 참조")] // BattleGameManager는 Player 생성/등록과 턴 전환만 담당하며, Player의 이동·카드·공격은 각 전용 컴포넌트가 담당한다.
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
    [Header("턴 안내 직접 참조")]
    [SerializeField] private BattleTurnAnnouncementView turnAnnouncementView;
    [Tooltip("Player 턴 전환 때 화면을 어둡게 만드는 기존 LoadingUI입니다.")]
    [SerializeField] private LoadingUI turnTransitionFade;

    [Header("Debug QA Boost")] // 텍스트만 존재함 
    [InspectorName("Debug QA boost enabled")]
    [SerializeField] private bool enableDebugQaBoost = true;
    [InspectorName("Debug Player maximum MP")]
    [SerializeField, Range(1, 10)] private int debugPlayerMaxMP = 10;
    [InspectorName("Debug maximum movement tiles")]
    [SerializeField, Range(1, 12)] private int debugMaxMoveRange = 6;
    private BattleQaTeleportController qaTeleportController;

    [Header("턴 상태 (런타임 확인용)")] // 턴 관리용 
    [InspectorName("현재 턴 번호")]
    [FormerlySerializedAs("totalTurn")]
    [SerializeField] private int currentTurnNumber = 1;
    [InspectorName("플레이어 턴 여부")]
    [FormerlySerializedAs("isPlayerTurn")]
    [SerializeField] private bool isPlayerTurnActive = true;
    [InspectorName("전투 정지 여부")]
    [FormerlySerializedAs("battleStopped")]
    [SerializeField] private bool isBattleStopped;
    [Header("전투 진행 상태")]
    [InspectorName("현재 스테이지")]
    [SerializeField, Min(1)] private int currentStage = 1;


    /// <summary>Player 등록이 끝난 뒤 카메라·Enemy 감지기 등에 생성된 Player 인스턴스를 전달한다.</summary>
    public event System.Action<GameObject> PlayerRegistered;

    /// <summary>Player 턴 자원과 입력 상태 초기화가 끝난 뒤 카드 드로우 등 후속 시스템에 알린다.</summary>
    public event System.Action PlayerTurnStarted;

    /// <summary>턴·주사위·전투 조작 차단 UI 상태가 바뀌어 카드 사용 가능 여부가 달라졌음을 알린다.</summary>
    public event System.Action<bool> CardUseAvailabilityChanged;

    /// <summary>SpawnPlayer가 생성하고 RegisterPlayer가 등록한 실제 전투 Player 오브젝트다.</summary>
    public GameObject CurrentPlayer { get; private set; }
    /// <summary>현재 Player의 턴 자원(MP). 카드·이동·기본 공격 비용이 이 값을 공유한다.</summary>
    public BattleUnitMP CurrentPlayerMP { get; private set; }
    /// <summary>현재 Player의 공격력과 사거리 등 전투 계산에 필요한 읽기 전용 기준 데이터다.</summary>
    public PlayerCombatData CurrentPlayerCombatData { get; private set; }
    /// <summary>현재 등록된 Player가 직접 소유하는 장비 슬롯과 장비 스탯이다.</summary>
    public PlayerWeapon CurrentPlayerWeapon { get; private set; }
    /// <summary>현재 등록된 Player가 직접 소유하는 골드 지갑이다.</summary>
    public PlayerWallet CurrentPlayerWallet { get; private set; }
    /// <summary>현재 등록된 플레이어의 체력 컴포넌트를 참조한다.</summary>
    public BattleHealth CurrentPlayerHealth => playerHealthBinding?.CurrentHealth;
    /// <summary>전투 시작과 Player 턴 시작에 손패를 구성하는 기존 카드 드로우 시스템 참조다.</summary>
    public BattleCardDrawSystem CardDrawSystem => cardDrawSystem;
    /// <summary>맵의 보상 상자를 열고 닫는 시스템. Player 사망 시 열린 UI를 강제로 닫는다.</summary>
    public BattleChestRewardSystem ChestRewardSystem => chestRewardSystem;
    /// <summary>맵 상점 진입·판매·구매를 담당하는 시스템. Player 사망 시 열린 UI를 강제로 닫는다.</summary>
    public BattleCardShopSystem CardShopSystem => cardShopSystem;
    public bool IsBattleStopped => isBattleStopped;
    public bool IsDebugQaBoostEnabled => enableDebugQaBoost;
    public int CurrentTurn => currentTurnNumber;
    public int CurrentStage => currentStage;
    /// <summary>이번 Player 턴에 확정된 주사위 값. 아직 굴리지 않았거나 이동 후에는 0이다.</summary>
    public BattleDiceSystem DiceSystem => diceSystem;
    /// <summary>상점·보상창처럼 뒤쪽 전투 조작을 막는 UI가 하나 이상 열려 있는지 나타낸다.</summary>
    public bool IsBattleBlockingUiOpen => overlayUi != null && overlayUi.IsOverlayOpen;

    /// <summary>기존 호출부 호환용 이름. 새 코드에서는 <see cref="IsBattleBlockingUiOpen"/>을 사용한다.</summary>
    public bool IsModalInteractionOpen => IsBattleBlockingUiOpen;
    public bool CanUsePlayerCards =>
        !isBattleStopped && isPlayerTurnActive && diceSystem != null &&
        diceSystem.HasRolledThisTurn && !IsBattleBlockingUiOpen;


    /// <summary>
    /// 상점·보상·상태창·턴 배너처럼 열린 동안 뒤쪽 전투를 조작하면 안 되는 UI가 열릴 때 호출한다.
    /// Overlay는 전투 화면 위에 덮이는 UI라는 뜻이며, 이 프로젝트에서는 '전투 조작 차단 UI'로 이해하면 된다.
    /// 여러 UI가 겹쳐 열릴 수 있으므로 열린 개수를 기록하고, 첫 UI부터 Player 입력,
    /// 카메라 입력, HUD 버튼 클릭을 함께 잠근다. 호출자는 해당 UI가 닫힐 때 반드시
    /// <see cref="UnlockBattleInputAfterOverlay"/>를 한 번 대응 호출해야 한다.
    /// </summary>
    public void LockBattleInputForOverlay()
    {
        // 실제 입력 차단과 열린 UI 개수 관리는 Overlay 전용 컴포넌트에 맡긴다.
        overlayUi.RegisterOpenedOverlayAndLockInput();
        // UI가 열린 즉시 턴 종료·주사위·카드 버튼 상태도 같은 잠금 상태로 맞춘다.
        SyncTurnUI();
    }

    /// <summary>
    /// LockBattleInputForOverlay로 등록한 오버레이 하나가 닫혔음을 알린다.
    /// 열린 전투 조작 차단 UI 수가 0이 된 경우에만 현재 턴에 맞춰 Player와 카메라 입력을 복구한다.
    /// Mathf.Max로 0 미만을 막지만, 호출 불균형을 해결하는 코드는 아니므로 열기/닫기 쌍을 지켜야 한다.
    /// </summary>
    public void UnlockBattleInputAfterOverlay()
    {
        // Player 턴인지, 전투가 중지됐는지를 전달해 입력을 복구해도 되는지 Overlay가 판단하게 한다.
        overlayUi.RegisterClosedOverlayAndRestoreInput(isPlayerTurnActive, isBattleStopped);
        SyncTurnUI();
    }

    /// <summary>
    /// BattleCardShopSystem이 상점을 열 때 true, 닫을 때 false를 전달한다.
    /// 숨기기 직전 각 UI의 activeSelf를 저장하므로 원래 꺼져 있던 UI를 잘못 켜지 않는다.
    /// shopRoot는 상점과 같은 Canvas에 있는 형제 UI만 골라 숨기기 위한 기준점이다.
    /// 이 함수는 화면 표시만 담당하며 실제 입력 잠금은 Lock/UnlockBattleInputForOverlay가 담당한다.
    /// </summary>
    public void SetShopOpen(bool shopIsOpen)
    {
        overlayUi.SetShopOpen(shopIsOpen);
        SyncTurnUI();
    }


    /// <summary>
    /// 씬이 로드될 때 전투 전체에서 사용할 단일 인스턴스를 확정하고 연결 시스템을 준비한다.
    /// 실행 순서는 전투 로그 초기화 → 카드/보상/상점 시스템 연결 → HUD 보조 컴포넌트 연결
    /// → 턴 버튼 및 Enemy 턴 실행기 연결이다. 여기서 생성·보완하는 컴포넌트는 기존 Moon 씬의
    /// 명시 참조가 비어 있을 때 이전 씬 호환성을 유지하기 위한 경로이며, 신규 씬은 Inspector 참조가 우선이다.
    /// </summary>
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
        // 현재 메인 진행 코드는 아직 DataConfig에 스테이지를 저장하므로 전투 진입 경계에서 한 번만 가져온다.
        // 추후 Run/Stage 진행 관리자가 생기면 SetCurrentStage 호출로 교체하고 이 호환 줄을 삭제한다.
        currentStage = Mathf.Max(1, DataConfig.stage);
        // 이전 전투의 정지 상태와 TimeScale이 남아 새 전투가 멈춘 채 시작되는 것을 방지한다.
        isBattleStopped = false;
        Time.timeScale = 1f;
        // 정적 로그 저장소는 Scene을 다시 열어도 유지될 수 있으므로 새 전투 시작 시 비운다.
        BattleCombatLog.ClearAllEntries();

        // 죽음을 판정하는 초기화가 아니다. BattleHealth.Died 이벤트가 발생했을 때
        // 이 Manager의 HandlePlayerDied가 호출되도록 콜백만 연결한다.
        playerHealthBinding?.SetDeathHandler(HandlePlayerDied);

        // 누락된 Inspector 참조를 런타임 자동 생성으로 숨기지 않고 시작 즉시 Console에 표시한다.
        ValidateRequiredReferences();

        // 동적 맵의 상점·상자 UI를 빠르게 검증할 수 있도록 Editor 전용 QA 텔레포트 입력을 연결한다.
        if (enableDebugQaBoost)
        {
            qaTeleportController = BattleComponentResolver.GetOrAdd(gameObject, qaTeleportController);
            qaTeleportController.Attach(this);
        }

        // UI 모듈은 버튼 클릭을 해석하고, 실제 턴 규칙은 이 Manager의 공개 함수를 호출한다.
        turnButtonController?.BindEndTurnAction(EndTurn);
        if (diceSystem != null)
        {
            diceSystem.DicePresentationCompleted -= HandleDicePresentationCompleted;
            diceSystem.DicePresentationCompleted += HandleDicePresentationCompleted;
        }

        // Scene에 저장된 최초 턴 상태를 버튼·카드 사용 가능 상태에 즉시 반영한다.
        SyncTurnUI();
    }


    /// <summary>
    /// 턴 종료 버튼 또는 E 입력에서 호출한다. 전투 정지·전투 조작 차단 UI 표시·주사위 미사용 상태에서는 무시한다.
    /// 정상 종료 시 Player 턴 플래그와 주사위 표시값을 초기화하고 턴 번호를 증가시킨 뒤,
    /// 카드 패널을 닫고 TriggerEnemyTurn 코루틴으로 제어권을 넘긴다.
    /// </summary>
    public void EndTurn()
    {
        // 사망으로 전투가 멈췄거나 상점 등의 UI가 열려 있으면 뒤쪽 턴 입력을 받지 않는다.
        if (isBattleStopped || IsBattleBlockingUiOpen)
        {
            return;
        }

        if (!isPlayerTurnActive || diceSystem == null || !diceSystem.HasRolledThisTurn)
        {
            if (isPlayerTurnActive && (diceSystem == null || !diceSystem.HasRolledThisTurn))
            {
                Debug.Log("주사위를 굴린 뒤 턴을 종료할 수 있습니다.", this);
            }

            return;
        }

        // 여기서부터 Player 입력 조건을 먼저 끈 뒤 Enemy 턴 코루틴으로 제어권을 넘긴다.
        isPlayerTurnActive = false;
        diceSystem.ResetForNewTurn();
        currentTurnNumber++;
        HideCardPanelUntilDice();
        SyncTurnUI();

        StartCoroutine(RunEnemyTurnSequence());
    }

    /// <summary>Enemy 전체 행동 후 Player 턴 상태와 MP·이동 상태를 초기화한다.
    /// "PLAYER TURN" 배너/페이드를 매번 재생한다(기본값).</summary>
    public void StartPlayerTurn()
    {
        StartPlayerTurn(showAnnouncement: true);
    }

    /// <summary>showAnnouncement가 false면 페이드(LoadingUI)와 "PLAYER TURN" 배너·카메라 잠금을
    /// 생략하고 곧바로 플레이어 턴을 시작한다. 두 경우에 쓴다.
    /// (1) Battle Scene 진입 시: SpawnPlayer.waitUnitMApGen()이 이미 자체 페이드 인/아웃을
    ///     한 번 재생하므로(맵 생성 완료 → HUD 표시), 곧바로 이어지는 최초 StartPlayerTurn까지
    ///     또 한 번 페이드를 재생하면 입장 중 페이드가 총 2번 발생한다.
    /// (2) Enemy 턴에서 실제로 행동(이동/공격)한 Enemy가 하나도 없었을 때: 연출 없이 즉시
    ///     다음 플레이어 턴으로 넘어간다.</summary>
    public void StartPlayerTurn(bool showAnnouncement)
    {
        // Player 사망 등으로 전투가 끝났다면 턴 자원과 입력을 다시 열지 않는다.
        if (isBattleStopped)
        {
            return;
        }

        // 상태이상 계산에 앞서 Player 턴 진입 상태를 세운다. 기절이면 아래에서 즉시 다시 해제된다.
        isPlayerTurnActive = true;

        // 기절 여부는 턴 시작 효과가 처리되기 전 값을 기준으로 이번 턴 건너뛰기를 결정한다.
        bool playerTurnSkipped = CurrentPlayer != null &&
            CurrentPlayer.GetComponent<BattleStatusEffects>()?.Has(BattleStatusType.Stun) == true;

        // 독·화상처럼 Player 턴 시작 시 발동하거나 남은 턴 수가 감소하는 모든 유닛의 상태효과를 한 번 진행한다.
        BattleStatusEffects.ProcessAllPlayerTurnStart();

        // 기절한 Player는 MP 회복, 주사위 입력, 카드 드로우 없이 바로 Enemy 턴으로 넘긴다.
        if (playerTurnSkipped)
        {
            isPlayerTurnActive = false;
            diceSystem?.ResetForNewTurn();
            currentTurnNumber++;
            // 실제 버그 수정(2026-08-22, 사용자 확인): 기절로 Player 턴을 건너뛰어도 바로 이어지는
            // Enemy 턴은 그대로 진행되므로, 여기서도 다음 Enemy 턴 MP를 새로 굴려둬야 한다.
            // 이 호출이 없으면 EnemyTurnActor.RollTurnMP()가 이번 라운드에 한 번도 호출되지 않아
            // Enemy가 그 이전 Player 턴에서 굴렸던 오래된 MP 값을 그대로 들고 행동하게 된다.
            PrepareEnemiesForNextTurn();
            // 버튼과 카드 사용 가능 상태를 먼저 잠근 뒤 Enemy 순차 행동을 시작한다.
            SyncTurnUI();
            StartCoroutine(RunEnemyTurnSequence());
            return;
        }

        // 보호막은 한 Player 턴만 유지되는 규칙이므로 새 Player 턴 시작 시 제거한다.
        CurrentPlayerHealth?.ClearShield();
        // 새 턴에는 아직 주사위를 굴리지 않았으므로 주사위 값과 사용 여부를 초기화한다.
        diceSystem?.ResetForNewTurn();
        // 이동·기본 공격·카드가 함께 쓰는 Player MP를 최대치까지 회복한다.
        CurrentPlayerMP?.RestoreFull();
        // Player가 미리 위협 정보를 확인할 수 있도록 다음 Enemy 턴의 MP를 지금 결정한다.
        PrepareEnemiesForNextTurn();
        // 이전 턴에서 남은 선택 타일, 이동 경로, 이동 완료 상태와 범위 표시를 지운다.
        ResetPlayerMoveState();
        // 카드는 주사위를 굴린 뒤에만 보이게 새 턴 시작 시 패널을 닫는다.
        HideCardPanelUntilDice();
        // 주사위 버튼, 턴 종료 버튼, 카드 사용 가능 상태를 새 턴 값으로 갱신한다.
        SyncTurnUI();

        // Manager의 턴 초기화가 전부 끝난 뒤 DrawSystem 등 구독자가 손패를 구성하게 한다.
        PlayerTurnStarted?.Invoke();
        // 화면 전투 로그에는 내부 초기화가 완료된 턴만 기록한다.
        BattleCombatLog.AddEntry($"TURN {currentTurnNumber}  PLAYER TURN");
        // 첫 전투 진입은 기존 입장 흐름이 페이드 한 번을 이미 담당한다.
        // 호출 경로가 추가되더라도 TURN 1에서 두 번째 페이드가 발생하지 않게 방어한다.
        bool willPlayTurnTransition = showAnnouncement && currentTurnNumber > 1;
        if (willPlayTurnTransition)
        {
            StartCoroutine(PlayPlayerTurnAnnouncementLocked(currentTurnNumber));
            return;
        }

        // 적이 행동하지 않은 턴은 페이드 코루틴을 생략한다. 기존에는 입력 복구도 그 코루틴의
        // 마지막에서만 실행되어 턴 버튼은 활성화됐지만 플레이어 이동·선택과 카메라는 계속
        // 잠긴 상태가 됐다. 무연출 복귀 경로에서는 여기서 즉시 조작권을 돌려준다.
        if (!IsBattleBlockingUiOpen)
        {
            playerActionController?.SetBattleInputEnabled(true);
            BattleMapCameraInput.SetEnabledOnMainCamera(true);

            BattleCameraRig cameraRig = Camera.main != null
                ? Camera.main.GetComponent<BattleCameraRig>()
                : null;
            cameraRig?.FocusPlayerImmediately();

            // 적이 행동하지 않은 경우에도 새 플레이어 턴이 시작됐다는 정보는 보여준다.
            // 페이드와 입력 잠금은 생략하므로 문구가 표시되는 동안에도 즉시 주사위/조작이 가능하다.
            if (currentTurnNumber > 1)
            {
                turnAnnouncementView?.StartPlayerTurnAnnouncement(currentTurnNumber, 1f);
            }
        }
    }

    /// <summary>"PLAYER TURN" 배너가 떠 있는 동안(1초) 유저가 지도를 조작하지 못하도록 잠근다.
    /// 배너에는 배경을 가리는 UI가 없어서(투명 텍스트만) 이전에는 표시 중에도 이동·공격 입력이
    /// 그대로 먹혔다.</summary>
    private IEnumerator PlayPlayerTurnAnnouncementLocked(int turn)
    {
        LockBattleInputForOverlay();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);

        if (turnTransitionFade != null)
        {
            // 게임 시작 페이드와 같은 느낌으로 턴 전환 페이드도 0.15초로 빠르게 재생한다
            // (턴 진행 로직은 그대로 두고 페이드 속도만 변경).
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
        yield return turnAnnouncementView.ShowStageAnnouncement(currentStage, 2f);
        BattleMapCameraInput.SetEnabledOnMainCamera(true);
        UnlockBattleInputAfterOverlay();
    }

    /// <summary>플레이어 턴 동안 다음 적 턴의 MP와 위협 범위를 확인할 수 있도록 모든 생존 적 MP를 준비한다.</summary>
    private void PrepareEnemiesForNextTurn()
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


    /// <summary>
    /// 생성된 Player를 전투 시스템의 공식 Player로 등록한다.
    /// BattlePlayerRegistrationService가 MP·전투 데이터·체력과 입력 제어기를 연결하면 이 클래스가
    /// 결과 참조를 보관하고, 체력 UI/사망 이벤트를 연결한 뒤 PlayerRegistered 이벤트로 카메라와
    /// Enemy 감지 시스템에 같은 인스턴스를 배포한다. 등록 실패 시 이전 Player 참조를 모두 비운다.
    /// </summary>
    public void RegisterPlayer(GameObject player)
    {
        if (CurrentPlayerWallet != null)
            CurrentPlayerWallet.GoldChanged -= HandlePlayerGoldChanged;

        // Binder가 Player의 MP·전투 데이터·체력을 찾고 MP UI, 덱, 행동 제어기에 연결한다.
        // Manager는 연결 방법을 알지 않고 성공 여부와 완성된 참조만 돌려받는다.
        if (playerRuntimeBinder == null || !playerRuntimeBinder.TryBind(
                player,
                CardDrawSystem,
                playerActionController,
                this,
                out BattleUnitMP playerMP,
                out PlayerCombatData combatData,
                out BattleHealth playerHealth))
        {
            // 일부 참조만 남아 이전 Player와 새 Player가 섞이지 않도록 등록 결과를 전부 비운다.
            CurrentPlayer = null;
            CurrentPlayerMP = null;
            CurrentPlayerCombatData = null;
            CurrentPlayerWeapon = null;
            CurrentPlayerWallet = null;
            playerHealthBinding?.Bind(null);
            return;
        }

        // 이후 턴 진행과 외부 조회에서 사용할 공식 Player 런타임 참조를 한 번에 교체한다.
        CurrentPlayer = player;
        CurrentPlayerMP = playerMP;
        CurrentPlayerCombatData = combatData;
        CurrentPlayerWeapon = BattleComponentResolver.GetOrAdd(
            player,
            player.GetComponent<PlayerWeapon>());
        CurrentPlayerWallet = BattleComponentResolver.GetOrAdd(
            player,
            player.GetComponent<PlayerWallet>());
        // 메인 씬의 기존 재화를 잃지 않도록 Player 등록 시 한 번만 레거시 값을 새 지갑으로 이전한다.
        CurrentPlayerWallet?.InitializeGold(DataConfig.playerMoney);
        if (CurrentPlayerWallet != null)
            CurrentPlayerWallet.GoldChanged += HandlePlayerGoldChanged;

        BattleEquipVisualBinder equipmentVisualBinder = BattleComponentResolver.GetOrAdd(
            player,
            player.GetComponent<BattleEquipVisualBinder>());
        CurrentPlayerCombatData.Bind(CurrentPlayerWeapon);
        equipmentVisualBinder?.Bind(CurrentPlayerWeapon);

        // SpawnPlayer가 Player Body의 CharactorStatus에 저장한 선택 인덱스를 UI 표현 컴포넌트에 전달한다.
        // 캐릭터 이름이나 Prefab 이름을 검색하지 않으며 Player 등록 시 한 번만 버튼 이미지를 결정한다.
        CharactorStatus playerCharacterStatus = player.GetComponentInParent<CharactorStatus>(true);
        if (playerCharacterStatus != null)
        {
            turnButtonController?.ApplyTurnEndImageForCharacter(playerCharacterStatus.TribeIndex);
        }
        else
        {
            Debug.LogError("캐릭터별 턴 종료 이미지를 결정할 CharactorStatus가 없습니다.", player);
        }

        // QA 모드에서만 밸런스와 무관한 최대 MP·이동 범위를 덮어써 기능 검증 시간을 줄인다.
        if (enableDebugQaBoost)
        {
            CurrentPlayerMP.ConfigureMaxMP(debugPlayerMaxMP);
            playerActionController?.ConfigureDebugMoveRange(debugMaxMoveRange);
        }
        // 새 Player의 체력 변경·사망 이벤트를 초상화 UI와 Manager 사망 처리에 연결한다.
        playerHealthBinding?.Bind(playerHealth);

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
        if (isBattleStopped)
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
        diceRollButton?.DisableRollInput();

        StopAllCoroutines();
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (diceSystem != null)
            diceSystem.DicePresentationCompleted -= HandleDicePresentationCompleted;
        if (CurrentPlayerWallet != null)
            CurrentPlayerWallet.GoldChanged -= HandlePlayerGoldChanged;
        overlayUi?.ResetOverlayInputState();
        playerHealthBinding?.ClearBinding();
        Instance = null;
    }

    /// <summary>현재 전투 스테이지를 변경하고 아직 남아 있는 구 진행 코드에도 같은 값을 동기화한다.</summary>
    public void SetCurrentStage(int stage)
    {
        currentStage = Mathf.Max(1, stage);
        DataConfig.stage = currentStage;
    }

    /// <summary>PlayerWallet을 재화 원본으로 유지하면서 구 저장 구조에는 결과만 동기화한다.</summary>
    private static void HandlePlayerGoldChanged(int currentGold)
    {
        DataConfig.playerMoney = Mathf.Max(0, currentGold);
    }


    /// <summary>
    /// 기존 주사위 버튼 연결을 유지하는 전달 함수다. 실제 굴림과 상태 저장은 BattleDiceSystem이 담당한다.
    /// </summary>
    public void RollDice()
    {
        if (diceSystem == null)
        {
            Debug.LogError("주사위 규칙 시스템 참조가 없어 굴릴 수 없습니다.", this);
            return;
        }

        bool canRollNow = !isBattleStopped && !IsBattleBlockingUiOpen && isPlayerTurnActive;
        if (!diceSystem.TryRollDice(canRollNow))
            Debug.LogWarning(
                $"주사위 입력 무시: 플레이어 턴={isPlayerTurnActive}, " +
                $"이미 굴림={diceSystem.HasRolledThisTurn}", this);
        SyncTurnUI();
    }

    /// <summary>
    /// Dice System이 연출 완료를 알리면 현재 Player 턴에서 이동 범위와 카드 패널을 연다.
    /// </summary>
    private void HandleDicePresentationCompleted(int presentedDiceValue)
    {
        if (!isPlayerTurnActive || diceSystem == null ||
            presentedDiceValue <= 0 || diceSystem.CurrentDiceValue != presentedDiceValue)
            return;

        playerActionController?.SetMoveRange(presentedDiceValue);
        cardPanelToggle?.Show();
    }

    /// <summary>
    /// 이동이 확정된 뒤 이번 주사위의 숫자 보관값만 0으로 지운다.
    /// Dice System의 굴림 완료 상태는 유지하므로 같은 턴에 주사위를 다시 굴릴 수 있게 만드는 함수가 아니다.
    /// BattlePlayerActionController가 이동 완료 또는 이동 취소 상태 정리 과정에서 호출한다.
    /// </summary>
    public void ResetDiceOnMove()
    {
        diceSystem?.ClearDisplayedValueAfterMove();
    }

    /// <summary>
    /// EndTurn이 시작하는 Enemy 턴 전체 흐름이다. Enemy 턴 배너 동안 전투 입력을 잠그고,
    /// BattleEnemyTurnRunner에 등록된 Enemy를 순서대로 실행하도록 위임한다. 모든 행동이 끝나면
    /// StartPlayerTurn으로 돌아간다. 실제 행동한 Enemy가 없으면 다음 Player 턴의 페이드를 생략한다.
    /// </summary>
    private IEnumerator RunEnemyTurnSequence()
    {
        // 1. Enemy 턴 안내가 끝날 때까지 Player·카메라 입력을 잠근다.
        LockBattleInputForOverlay();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);
        int enemyRound = Mathf.Max(1, currentTurnNumber - 1);
        BattleCombatLog.AddEntry($"TURN {enemyRound}  ENEMY TURN");
        yield return turnAnnouncementView.ShowEnemyTurnAnnouncementAndWait(enemyRound, 1f);
        UnlockBattleInputAfterOverlay();

        // 2. Runner는 UnitRegistry에 등록된 Enemy의 생성 순서를 기준으로 한 명씩 실행한다.
        //    각 Enemy의 이동·공격 코루틴이 끝나기 전에는 다음 Enemy로 넘어가지 않는다.
        if (enemyTurnRunner == null)
        {
            Debug.LogError("Enemy 턴 실행기가 연결되지 않아 Player 턴으로 복귀합니다.", this);
            StartPlayerTurn(showAnnouncement: false);
            yield break;
        }
        yield return enemyTurnRunner.RunAll(battleDataPool);

        // 3. 모든 Enemy 행동이 끝난 뒤 Player 턴을 새로 초기화한다. 실제 행동한 Enemy가
        //    없었다면 불필요한 전환 페이드만 생략하고 게임 규칙 초기화는 동일하게 수행한다.
        StartPlayerTurn(showAnnouncement: enemyTurnRunner.AnyEnemyActedLastRun);
    }

    /// <summary>현재 턴과 주사위 상태에 맞춰 주사위 및 턴 종료 버튼 사용 여부를 갱신한다.</summary>
    private void SyncTurnUI()
    {
        turnButtonController?.ApplyTurnEndButtonState(
            isPlayerTurnActive,
            isBattleStopped,
            IsBattleBlockingUiOpen);

        diceRollButton?.ApplyRollButtonState(
            isPlayerTurnActive,
            diceSystem != null && diceSystem.HasRolledThisTurn,
            isBattleStopped,
            IsBattleBlockingUiOpen);

        CardUseAvailabilityChanged?.Invoke(CanUsePlayerCards);
    }

    /// <summary>플레이어 턴 시작 시 이동 입력기의 선택, 이동, 범위 표시 상태를 초기화한다.</summary>
    private void ResetPlayerMoveState()
    {
        if (playerActionController != null)
        {
            playerActionController.ResetPlayerTurnActions();
        }
    }

    /// <summary>새 Player 턴과 Enemy 턴에는 카드 패널을 숨겨 주사위 이후에만 표시한다.</summary>
    private void HideCardPanelUntilDice()
    {
        cardPanelToggle?.Hide();
    }

    /// <summary>
    /// 필수 Inspector 참조가 빠졌는지 Scene 시작 시 한 번 검사해 Console에 구체적인 누락 항목을 표시한다.
    /// 자동 검색이나 AddComponent로 누락을 숨기지 않으므로 Moon Scene뿐 아니라 이 Manager를 재사용하는
    /// 다른 전투 Scene에서도 같은 검증을 받을 수 있다. 오류를 출력해도 실행을 강제 중단하지는 않는다.
    /// </summary>
    private void ValidateRequiredReferences()
    {
        if (turnButtonController == null) Debug.LogError("턴 버튼 제어기 참조가 없습니다.", this);
        if (diceRollButton == null) Debug.LogError("주사위 버튼 입력·표시 모듈 참조가 없습니다.", this);
        if (diceSystem == null) Debug.LogError("주사위 규칙 시스템 참조가 없습니다.", this);
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
