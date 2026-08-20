using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Randoom = UnityEngine.Random;

/// <summary>
/// Player/Enemy 턴 전환, 주사위 상태, 생성된 Player 배포를 담당하는 전투 진입점이다.
/// 이동 판단이나 AI 판단은 직접 수행하지 않고 각 전용 컴포넌트에 위임한다.
/// </summary>
public class BattleGameManager : MonoBehaviour
{
    public static BattleGameManager Instance { get; private set; }

    [Header("턴 화면 참조")]
    [InspectorName("턴 종료 전용 버튼")]
    [SerializeField] private Button turnEndButton;
    [InspectorName("독립 주사위 굴리기 버튼(화면 중앙 등)")]
    [SerializeField] private Button diceButton;
    [InspectorName("턴 버튼 제어 모듈")]
    [SerializeField] private BattleTurnButtonController turnButtonController;
    [InspectorName("턴 디버그 텍스트")]
    [SerializeField] private TMP_Text turnDebugText;
    [InspectorName("주사위 디버그 텍스트")]
    [SerializeField] private TMP_Text diceDebugText;
    [InspectorName("턴 디버그 표시 모듈")]
    [SerializeField] private BattleTurnDebugView turnDebugView;
    [InspectorName("플레이어 HP 디버그 텍스트")]
    [SerializeField] private TMP_Text playerHpDebugText;
    [InspectorName("플레이어 보호막 디버그 텍스트")]
    [SerializeField] private TMP_Text playerShieldDebugText;

    [Header("플레이어 런타임 참조")]
    [InspectorName("플레이어 바디")]
    [SerializeField] private GameObject playerBody;
    [InspectorName("플레이어 행동력 화면")]
    [SerializeField] private PlayerMPUI playerMPUI;
    [InspectorName("플레이어 행동 제어기")]
    [SerializeField] private BattlePlayerActionController playerActionController;
    [InspectorName("전투 카드 데이터베이스")]
    [SerializeField] private BattleCardDatabase battleCardDatabase;
    [InspectorName("원본 카드 데이터베이스")]
    [SerializeField] private CardDatabase originalCardDatabase;
    [InspectorName("전투 데이터 저장소")]
    [SerializeField] private BattleDataPool battleDataPool;
    [InspectorName("적 턴 순차 실행 모듈")]
    [SerializeField] private BattleEnemyTurnRunner enemyTurnRunner;
    [InspectorName("플레이어 HP 바 프리팹 (UI 배치용, 추후 연결)")]
    [SerializeField] private GameObject playerHpPrefab;
    [InspectorName("플레이어 초상화 HP·보호막 화면")]
    [SerializeField] private BattlePlayerPortraitStatusView playerPortraitStatusView;
    [Header("플레이어 체력 변화 연출")]
    [InspectorName("피해 감소 속도 (비율/초)")]
    [SerializeField, Min(0.01f)] private float portraitDamageDecreaseSpeed = 1.5f;
    [InspectorName("회복 증가 속도 (비율/초)")]
    [SerializeField, Min(0.01f)] private float portraitHealingIncreaseSpeed = 3f;
    [Header("Combat Log")]
    [InspectorName("Combat Log TMP Text")]
    [SerializeField] private TMP_Text combatLogText;
    [InspectorName("Maximum Visible Log Lines")]
    [SerializeField, Range(1, 20)] private int combatLogVisibleEntries = 6;
    [InspectorName("Mirror Combat Log To Console")]
    [SerializeField] private bool mirrorCombatLogToConsole;
    private BattleTurnAnnouncementView turnAnnouncementView;
    private BattleCardPanelToggle cardPanelToggle;

    [Header("Debug QA Boost")]
    [InspectorName("Debug QA boost enabled")]
    [SerializeField] private bool enableDebugQaBoost = true;
    [InspectorName("Debug Player maximum MP")]
    [SerializeField, Range(1, 10)] private int debugPlayerMaxMP = 10;
    [InspectorName("Debug maximum movement tiles")]
    [SerializeField, Range(1, 12)] private int debugMaxMoveRange = 6;

    [Header("턴 상태 (런타임 확인용)")]
    [InspectorName("현재 턴 번호")]
    [SerializeField] private int totalTurn = 1;
    [InspectorName("플레이어 턴 여부")]
    [SerializeField] private bool isPlayerTurn = true;
    [InspectorName("이번 턴 주사위 굴림 여부")]
    [SerializeField] private bool diceRolledThisTurn = false;
    [InspectorName("현재 주사위 값")]
    [SerializeField] private int currentDiceValue = 0;
    [InspectorName("전투 정지 여부")]
    [SerializeField] private bool battleStopped;

    private int modalInteractionCount;
    private CanvasGroup hudCanvasGroup;
    private bool hudCanvasWasInteractable = true;
    private bool hudCanvasWasBlockingRaycasts = true;
    private bool shopHudHidden;
    private bool turnButtonWasActive;
    private bool mpUiWasActive;
    private bool turnDebugWasActive;
    private bool diceDebugWasActive;
    private bool hpDebugWasActive;
    private bool shieldDebugWasActive;
    private bool cardPanelWasActive;
    private GameObject cachedCardPanel;
    private readonly List<GameObject> shopHiddenCanvasObjects = new List<GameObject>();
    private readonly List<bool> shopHiddenCanvasStates = new List<bool>();

    public GameObject CurrentPlayer { get; private set; }
    public CharacterMP CurrentPlayerMP { get; private set; }
    public PlayerCombatData CurrentPlayerCombatData { get; private set; }
    /// <summary>디버그 HP 표시와 향후 피해 확인용으로 등록된 플레이어의 체력 컴포넌트를 참조한다.</summary>
    public BattleHealth CurrentPlayerHealth { get; private set; }
    private BattleStatusEffects currentPlayerStatus;
    public BattleCardDrawSystem CardDrawSystem { get; private set; }
    public BattleChestRewardSystem ChestRewardSystem { get; private set; }
    public BattleCardShopSystem CardShopSystem { get; private set; }
    public bool IsBattleStopped => battleStopped;
    public bool IsDebugQaBoostEnabled => enableDebugQaBoost;
    public int CurrentTurn => totalTurn;
    public bool IsModalInteractionOpen => modalInteractionCount > 0;
    public bool CanUsePlayerCards => isPlayerTurn && diceRolledThisTurn && !IsModalInteractionOpen;

    public void BeginModalInteraction()
    {
        modalInteractionCount++;
        ResolvePlayerActionController();
        playerActionController?.SetBattleInputEnabled(false);
        BattleMapCameraInput.SetEnabledOnMainCamera(false);
        if (modalInteractionCount == 1)
        {
            SetHudCanvasLocked(true);
        }
        SyncTurnUI();
    }

    public void EndModalInteraction()
    {
        modalInteractionCount = Mathf.Max(0, modalInteractionCount - 1);
        if (modalInteractionCount == 0 && !battleStopped)
        {
            ResolvePlayerActionController();
            playerActionController?.SetBattleInputEnabled(isPlayerTurn);
            BattleMapCameraInput.SetEnabledOnMainCamera(isPlayerTurn);
            SetHudCanvasLocked(false);
        }
        SyncTurnUI();
    }

    /// <summary>모달(턴/스테이지 배너, 캐릭터 정보 등)이 떠 있는 동안 HUDCanvas 전체의 버튼
    /// 입력을 막는다. IsModalInteractionOpen은 BattlePlayerInputReader처럼 Update()를 직접
    /// 폴링하는 입력만 막고 Unity UI Button.onClick(EventSystem 레이캐스트)은 막지 못했기
    /// 때문에, 게임 시작 연출 중에도 HUD 버튼이 그대로 눌리는 문제가 있었다. HUDCanvas에
    /// CanvasGroup을 붙여 interactable/blocksRaycasts를 잠그는 방식으로 두 경로를 모두 막는다.
    /// 캐릭터 정보 패널처럼 HUDCanvas 안에 중첩된 모달 자신은 CharacterListUIStatusController가
    /// 별도 CanvasGroup(ignoreParentGroups = true)로 예외 처리해 계속 조작 가능하다.</summary>
    private void SetHudCanvasLocked(bool locked)
    {
        if (hudCanvasGroup == null)
        {
            foreach (Canvas sceneCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (sceneCanvas == null || !sceneCanvas.gameObject.name.StartsWith("HUDCanvas")) continue;
                hudCanvasGroup = sceneCanvas.GetComponent<CanvasGroup>();
                if (hudCanvasGroup == null) hudCanvasGroup = sceneCanvas.gameObject.AddComponent<CanvasGroup>();
                break;
            }
            if (hudCanvasGroup == null) return;
        }

        if (locked)
        {
            hudCanvasWasInteractable = hudCanvasGroup.interactable;
            hudCanvasWasBlockingRaycasts = hudCanvasGroup.blocksRaycasts;
            hudCanvasGroup.interactable = false;
            hudCanvasGroup.blocksRaycasts = false;
            return;
        }

        hudCanvasGroup.interactable = hudCanvasWasInteractable;
        hudCanvasGroup.blocksRaycasts = hudCanvasWasBlockingRaycasts;
    }

    /// <summary>상점 모달 동안 전투 조작 HUD를 숨기고 종료 시 이전 활성 상태로 복원한다.</summary>
    public void SetShopHudVisible(bool visible, GameObject shopRoot = null)
    {
        if (!visible)
        {
            if (shopHudHidden) return;
            shopHudHidden = true;
            turnButtonWasActive = turnEndButton != null && turnEndButton.gameObject.activeSelf;
            mpUiWasActive = playerMPUI != null && playerMPUI.gameObject.activeSelf;
            turnDebugWasActive = turnDebugText != null && turnDebugText.gameObject.activeSelf;
            diceDebugWasActive = diceDebugText != null && diceDebugText.gameObject.activeSelf;
            hpDebugWasActive = playerHpDebugText != null && playerHpDebugText.gameObject.activeSelf;
            shieldDebugWasActive = playerShieldDebugText != null && playerShieldDebugText.gameObject.activeSelf;
            BattleCardPanelToggle cardPanelToggle = FindFirstObjectByType<BattleCardPanelToggle>(FindObjectsInactive.Include);
            cachedCardPanel = cardPanelToggle != null ? cardPanelToggle.gameObject : null;
            cardPanelWasActive = cachedCardPanel != null && cachedCardPanel.activeSelf;

            if (turnEndButton != null) turnEndButton.gameObject.SetActive(false);
            if (playerMPUI != null) playerMPUI.gameObject.SetActive(false);
            if (turnDebugText != null) turnDebugText.gameObject.SetActive(false);
            if (diceDebugText != null) diceDebugText.gameObject.SetActive(false);
            if (playerHpDebugText != null) playerHpDebugText.gameObject.SetActive(false);
            if (playerShieldDebugText != null) playerShieldDebugText.gameObject.SetActive(false);
            if (cachedCardPanel != null) cachedCardPanel.SetActive(false);
            HideShopBackgroundCanvases(shopRoot);
            return;
        }

        if (!shopHudHidden) return;
        shopHudHidden = false;
        if (turnEndButton != null) turnEndButton.gameObject.SetActive(turnButtonWasActive);
        if (playerMPUI != null) playerMPUI.gameObject.SetActive(mpUiWasActive);
        if (turnDebugText != null) turnDebugText.gameObject.SetActive(turnDebugWasActive);
        if (diceDebugText != null) diceDebugText.gameObject.SetActive(diceDebugWasActive);
        if (playerHpDebugText != null) playerHpDebugText.gameObject.SetActive(hpDebugWasActive);
        if (playerShieldDebugText != null) playerShieldDebugText.gameObject.SetActive(shieldDebugWasActive);
        if (cachedCardPanel != null) cachedCardPanel.SetActive(cardPanelWasActive);
        cachedCardPanel = null;
        RestoreShopBackgroundCanvases();
        SyncTurnUI();
    }

    /// <summary>별도 HUDCanvas와 상점 이외의 Battle Canvas 직계 UI 가지를 숨긴다.</summary>
    private void HideShopBackgroundCanvases(GameObject shopRoot)
    {
        shopHiddenCanvasObjects.Clear();
        shopHiddenCanvasStates.Clear();

        foreach (Canvas sceneCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sceneCanvas == null || !sceneCanvas.gameObject.name.StartsWith("HUDCanvas")) continue;
            RememberAndHide(sceneCanvas.gameObject);
        }

        Canvas battleCanvas = null;
        if (shopRoot != null)
        {
            Canvas[] parents = shopRoot.GetComponentsInParent<Canvas>(true);
            foreach (Canvas parent in parents)
            {
                if (parent != null && parent.gameObject.name.Trim() == "Canvas - Battle")
                {
                    battleCanvas = parent;
                    break;
                }
            }
        }
        if (battleCanvas == null) return;

        Transform shopBranch = shopRoot.transform;
        while (shopBranch.parent != null && shopBranch.parent != battleCanvas.transform)
            shopBranch = shopBranch.parent;

        foreach (Transform child in battleCanvas.transform)
        {
            if (child == null || child == shopBranch) continue;
            RememberAndHide(child.gameObject);
        }
    }

    private void RememberAndHide(GameObject target)
    {
        if (target == null || shopHiddenCanvasObjects.Contains(target)) return;
        shopHiddenCanvasObjects.Add(target);
        shopHiddenCanvasStates.Add(target.activeSelf);
        target.SetActive(false);
    }

    private void RestoreShopBackgroundCanvases()
    {
        for (int i = 0; i < shopHiddenCanvasObjects.Count; i++)
        {
            GameObject target = shopHiddenCanvasObjects[i];
            if (target != null) target.SetActive(shopHiddenCanvasStates[i]);
        }
        shopHiddenCanvasObjects.Clear();
        shopHiddenCanvasStates.Clear();
    }
    /// <summary>Player 등록이 끝난 뒤 외부 시스템에 생성된 인스턴스를 전달하는 이벤트.</summary>
    public event System.Action<GameObject> PlayerRegistered;
    /// <summary>플레이어 턴 초기화가 완료된 뒤 카드 드로우 등 후속 시스템에 알린다.</summary>
    public event System.Action PlayerTurnStarted;
    /// <summary>턴 또는 주사위 상태가 바뀌어 카드 사용 가능 여부가 변경됐을 때 알린다.</summary>
    public event System.Action<bool> CardUseAvailabilityChanged;
    /// <summary>주사위 입력이 있을 때마다 알린다. true=실제로 굴림, false=조건이 안 맞아 무시됨.
    /// 추후 주사위 연출(VFX)을 이 이벤트에 걸면 된다.</summary>
    public event System.Action<bool> DiceRolled;

    /// <summary>싱글턴과 버튼 이벤트를 구성하고 카드 드로우 시스템을 자동으로 보완한다.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        battleStopped = false;
        Time.timeScale = 1f;
        BattleCombatLog.Clear();
        BattleComponentResolver.GetOrAdd<BattleCombatLogView>(gameObject, null).Configure(
            combatLogText,
            combatLogVisibleEntries,
            mirrorCombatLogToConsole);

        CardDrawSystem = BattleCardDrawSystemFactory.CreateOrConfigure(
            gameObject,
            battleCardDatabase,
            originalCardDatabase);
        ChestRewardSystem = BattleComponentResolver.GetOrAdd<BattleChestRewardSystem>(gameObject, null);
        ChestRewardSystem.Configure(battleCardDatabase, originalCardDatabase);
        CardShopSystem = BattleComponentResolver.GetOrAdd<BattleCardShopSystem>(gameObject, null);
        CardShopSystem.Configure(battleCardDatabase, originalCardDatabase);
        BattleComponentResolver.GetOrAdd<BattleHudStatusBridge>(gameObject, null);
        BattleComponentResolver.GetOrAdd<BattleEnemyInspectView>(gameObject, null);
        turnAnnouncementView = BattleComponentResolver.GetOrAdd<BattleTurnAnnouncementView>(gameObject, turnAnnouncementView);
        EnsureCharacterStatusController();

        EnsureTurnButtonController();
        EnsureEnemyTurnRunner();
        EnsureTurnDebugView();

        // 턴 종료 버튼은 이제 턴 종료 전용이다. 주사위 기능은 완전히 분리했다.
        turnButtonController.Bind(turnEndButton, EndTurn);

        // 독립 주사위 버튼: 더 이상 숨기지 않는다.
        // (통합 행동 버튼과 별개로 화면 중앙 등에 배치해 매 턴 시작을 강조하는 용도)
        // BattleDiceRollButton(꾹 눌러 게이지가 오가는 방식)이 붙어 있으면 그 컴포넌트가
        // PointerDown/Up으로 직접 RollDice를 호출하므로, 여기서는 onClick을 이중으로 걸지 않는다.
        if (diceButton != null && diceButton != turnEndButton)
        {
            BattlePointerSelectionClearer.Ensure(diceButton.gameObject);
            bool hasHoldGauge = diceButton.GetComponent<BattleDiceRollButton>() != null;
            if (!hasHoldGauge)
            {
                diceButton.onClick.RemoveListener(RollDice);
                diceButton.onClick.AddListener(RollDice);
            }
        }

        RefreshDebugView();
        SyncTurnUI();
    }

    /// <summary>주사위를 굴린 Player 턴만 종료하고 Enemy 순차 행동을 시작한다.</summary>
    public void EndTurn()
    {
        if (battleStopped || IsModalInteractionOpen)
        {
            return;
        }

        if (!isPlayerTurn || !diceRolledThisTurn)
        {
            if (isPlayerTurn && !diceRolledThisTurn)
            {
                Debug.Log("주사위를 굴린 뒤 턴을 종료할 수 있습니다.", this);
            }

            return;
        }

        isPlayerTurn = false;
        diceRolledThisTurn = false;
        currentDiceValue = 0;
        totalTurn++;
        HideCardPanelUntilDice();
        RefreshDebugView();
        SyncTurnUI();

        StartCoroutine(TriggerEnemyTurn());
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
        if (battleStopped)
        {
            return;
        }

        EnsureCharacterStatusController();
        isPlayerTurn = true;
        bool playerTurnSkipped = CurrentPlayer != null &&
            CurrentPlayer.GetComponent<BattleStatusEffects>()?.Has(BattleStatusType.Stun) == true;
        BattleStatusEffects.ProcessAllPlayerTurnStart();
        if (playerTurnSkipped)
        {
            isPlayerTurn = false;
            diceRolledThisTurn = false;
            currentDiceValue = 0;
            totalTurn++;
            RefreshDebugView();
            SyncTurnUI();
            StartCoroutine(TriggerEnemyTurn());
            return;
        }
        CurrentPlayerHealth?.ClearShield();
        diceRolledThisTurn = false;
        currentDiceValue = 0;
        CurrentPlayerMP?.RestoreFull();
        PrepareEnemiesForNextTurn();
        ResetPlayerMoveState();
        HideCardPanelUntilDice();
        RefreshDebugView();
        SyncTurnUI();
        PlayerTurnStarted?.Invoke();
        BattleCombatLog.Add($"TURN {totalTurn}  PLAYER TURN");
        // 첫 전투 진입은 기존 입장 흐름이 페이드 한 번을 이미 담당한다.
        // 호출 경로가 추가되더라도 TURN 1에서 두 번째 페이드가 발생하지 않게 방어한다.
        bool willPlayTurnTransition = showAnnouncement && totalTurn > 1;
        if (willPlayTurnTransition)
        {
            StartCoroutine(PlayPlayerTurnAnnouncementLocked(totalTurn));
            return;
        }

        // 적이 행동하지 않은 턴은 페이드 코루틴을 생략한다. 기존에는 입력 복구도 그 코루틴의
        // 마지막에서만 실행되어 턴 버튼은 활성화됐지만 플레이어 이동·선택과 카메라는 계속
        // 잠긴 상태가 됐다. 무연출 복귀 경로에서는 여기서 즉시 조작권을 돌려준다.
        if (!IsModalInteractionOpen)
        {
            ResolvePlayerActionController();
            playerActionController?.SetBattleInputEnabled(true);
            BattleMapCameraInput.SetEnabledOnMainCamera(true);

            BattleCameraRig cameraRig = Camera.main != null
                ? Camera.main.GetComponent<BattleCameraRig>()
                : null;
            cameraRig?.FocusPlayerImmediately();

            // 적이 행동하지 않은 경우에도 새 플레이어 턴이 시작됐다는 정보는 보여준다.
            // 페이드와 입력 잠금은 생략하므로 문구가 표시되는 동안에도 즉시 주사위/조작이 가능하다.
            if (totalTurn > 1)
            {
                turnAnnouncementView = BattleComponentResolver.GetOrAdd<BattleTurnAnnouncementView>(
                    gameObject,
                    turnAnnouncementView);
                turnAnnouncementView.ShowPlayerTurn(totalTurn, 1f);
            }
        }
    }

    /// <summary>"PLAYER TURN" 배너가 떠 있는 동안(1초) 유저가 지도를 조작하지 못하도록 잠근다.
    /// 배너에는 배경을 가리는 UI가 없어서(투명 텍스트만) 이전에는 표시 중에도 이동·공격 입력이
    /// 그대로 먹혔다.</summary>
    private IEnumerator PlayPlayerTurnAnnouncementLocked(int turn)
    {
        turnAnnouncementView = BattleComponentResolver.GetOrAdd<BattleTurnAnnouncementView>(gameObject, turnAnnouncementView);
        BeginModalInteraction();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);

        LoadingUI loadingUI = FindFirstObjectByType<LoadingUI>(FindObjectsInactive.Include);
        if (loadingUI != null)
        {
            // 게임 시작 페이드와 같은 느낌으로 턴 전환 페이드도 0.15초로 빠르게 재생한다
            // (턴 진행 로직은 그대로 두고 페이드 속도만 변경).
            yield return loadingUI.FadeToBlackRoutine(0.15f);
        }

        BattleCameraRig cameraRig = Camera.main != null
            ? Camera.main.GetComponent<BattleCameraRig>()
            : null;
        cameraRig?.FocusPlayerImmediately();

        loadingUI?.FadeIn(0.15f);
        yield return turnAnnouncementView.ShowPlayerTurnRoutine(turn, 1f);

        while (loadingUI != null && loadingUI.IsFading)
        {
            yield return null;
        }

        BattleMapCameraInput.SetEnabledOnMainCamera(true);
        EndModalInteraction();
    }

    /// <summary>현재 층 번호를 2초 동안 표시하고 연출 종료까지 기다린다. 표시 중에는 배경 입력과
    /// 카메라 드래그/줌/키보드 이동을 모두 잠근다.</summary>
    public IEnumerator PlayStageIntro()
    {
        turnAnnouncementView = BattleComponentResolver.GetOrAdd<BattleTurnAnnouncementView>(gameObject, turnAnnouncementView);
        BeginModalInteraction();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);
        yield return turnAnnouncementView.ShowStage(Mathf.Max(1, DataConfig.stage), 2f);
        BattleMapCameraInput.SetEnabledOnMainCamera(true);
        EndModalInteraction();
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

        foreach (EnemyTurnActor actor in FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None))
            actor?.PrepareNextTurnMP();
    }

    /// <summary>Player Body의 마지막 자식으로 생성된 선택 캐릭터를 찾아 등록한다.</summary>
    public bool RegisterSpawnedPlayerFromBody()
    {
        if (!BattlePlayerRegistrationService.TryFindSpawnedPlayer(
                playerBody,
                this,
                out GameObject spawnedPlayer))
        {
            return false;
        }

        RegisterPlayer(spawnedPlayer);
        return true;
    }

    /// <summary>
    /// 생성된 Player를 이동, Enemy 감지, MP UI에 배포한다. CharacterMP가 없으면 런타임에 보완한다.
    /// </summary>
    public void RegisterPlayer(GameObject player)
    {
        if (playerMPUI == null)
        {
            playerMPUI = FindFirstObjectByType<PlayerMPUI>(FindObjectsInactive.Include);
        }

        ResolvePlayerActionController();
        if (!BattlePlayerRegistrationService.TryRegisterRuntime(
                player,
                playerMPUI,
                CardDrawSystem,
                playerActionController,
                this,
                out CharacterMP playerMP,
                out PlayerCombatData combatData,
                out BattleHealth playerHealth))
        {
            CurrentPlayer = null;
            CurrentPlayerMP = null;
            CurrentPlayerCombatData = null;
            BindPlayerHealthDebugText(null);
            return;
        }

        CurrentPlayer = player;
        CurrentPlayerMP = playerMP;
        CurrentPlayerCombatData = combatData;
        if (enableDebugQaBoost)
        {
            CurrentPlayerMP.ConfigureFixedMaxMP(debugPlayerMaxMP);
            playerActionController?.ConfigureDebugMoveRange(debugMaxMoveRange);
        }
        BindPlayerHealthDebugText(playerHealth);

        PlayerRegistered?.Invoke(CurrentPlayer);
        Debug.Log($"전투 플레이어 등록 완료: {CurrentPlayer.name}", CurrentPlayer);
    }

    /// <summary>플레이어 HP 변경을 디버그 텍스트에 표시한다. 텍스트 참조가 없으면 값만 보관한다.</summary>
    private void BindPlayerHealthDebugText(BattleHealth health)
    {
        if (CurrentPlayerHealth != null)
        {
            CurrentPlayerHealth.HealthChanged -= RefreshPlayerHpDebugText;
            CurrentPlayerHealth.ShieldChanged -= RefreshPlayerShieldDebugText;
            CurrentPlayerHealth.Died -= RefreshPlayerHpDebugText;
            CurrentPlayerHealth.Died -= HandlePlayerDied;
        }
        if (currentPlayerStatus != null)
            currentPlayerStatus.Changed -= RefreshPlayerStatusEffectsText;

        CurrentPlayerHealth = health;
        currentPlayerStatus = health != null ? BattleStatusEffects.GetOrAdd(health.gameObject) : null;
        if (currentPlayerStatus != null)
        {
            currentPlayerStatus.Changed -= RefreshPlayerStatusEffectsText;
            currentPlayerStatus.Changed += RefreshPlayerStatusEffectsText;
        }

        playerPortraitStatusView = BattleComponentResolver.GetOrAdd(
            gameObject,
            playerPortraitStatusView);
        playerPortraitStatusView.ConfigureAnimation(
            portraitDamageDecreaseSpeed,
            portraitHealingIncreaseSpeed);
        playerPortraitStatusView.Bind(CurrentPlayerHealth);

        if (CurrentPlayerHealth != null)
        {
            CurrentPlayerHealth.HealthChanged += RefreshPlayerHpDebugText;
            CurrentPlayerHealth.ShieldChanged += RefreshPlayerShieldDebugText;
            CurrentPlayerHealth.Died += RefreshPlayerHpDebugText;
            CurrentPlayerHealth.Died += HandlePlayerDied;
        }

        RefreshPlayerStatusDebugText(CurrentPlayerHealth);
    }

    /// <summary>디버그용 플레이어 HP 텍스트를 "현재/최대" 형태로 갱신한다.</summary>
    private void RefreshPlayerHpDebugText(BattleHealth health)
    {
        RefreshPlayerStatusDebugText(health);
    }

    /// <summary>디버그용 플레이어 보호막 텍스트를 현재 수치로 갱신합니다.</summary>
    private void RefreshPlayerShieldDebugText(BattleHealth health)
    {
        RefreshPlayerStatusDebugText(health);
    }

    private void RefreshPlayerStatusEffectsText(BattleStatusEffects status)
    {
        RefreshPlayerStatusDebugText(CurrentPlayerHealth);
    }

    /// <summary>보호막이 있으면 Barrier만, 없으면 HP만 즉시 표시한다.</summary>
    private void RefreshPlayerStatusDebugText(BattleHealth health)
    {
        bool hasBarrier = health != null && health.CurrentShield > 0f;
        string statusLabel = currentPlayerStatus != null
            ? currentPlayerStatus.BuildCompactLabel()
            : string.Empty;
        string statusSuffix = string.IsNullOrEmpty(statusLabel) ? string.Empty : $"\n{statusLabel}";
        if (playerHpDebugText != null)
        {
            playerHpDebugText.enabled = !hasBarrier;
            playerHpDebugText.text = health != null
                ? $"HP : {Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.CeilToInt(health.MaxHealth)}{statusSuffix}"
                : "HP : - / -";
        }

        if (playerShieldDebugText != null)
        {
            playerShieldDebugText.enabled = hasBarrier;
            playerShieldDebugText.text = health != null
                ? $"Barrier : {Mathf.CeilToInt(health.CurrentShield)}{statusSuffix}"
                : "Barrier : -";
        }
    }

    /// <summary>
    /// 플레이어 체력이 0이 되면 사망 연출·보상 등 후속 규칙을 아직 정하지 않았으므로
    /// 우선 게임을 그 자리에서 멈춘다(Time.timeScale = 0). 사용자 지정 임시 처리다.
    /// </summary>
    private void HandlePlayerDied(BattleHealth health)
    {
        if (battleStopped)
        {
            return;
        }

        battleStopped = true;
        ChestRewardSystem?.ForceClose();
        CardShopSystem?.ForceClose();
        modalInteractionCount = 0;
        Debug.Log("플레이어 체력이 0이 되어 게임을 정지합니다.", this);

        ResolvePlayerActionController();
        playerActionController?.SetBattleInputEnabled(false);

        if (turnEndButton != null)
        {
            turnEndButton.interactable = false;
        }

        if (diceButton != null)
        {
            diceButton.interactable = false;
        }

        StopAllCoroutines();
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        modalInteractionCount = 0;
        Instance = null;
    }

    /// <summary>플레이어 턴에 한 번 주사위를 굴리고 결과를 이동 가능 거리로 전달한다.</summary>
    public void RollDice()
    {
        if (battleStopped || IsModalInteractionOpen)
        {
            return;
        }

        if (!isPlayerTurn || diceRolledThisTurn)
        {
            Debug.LogWarning($"주사위 입력 무시: 플레이어 턴={isPlayerTurn}, 이미 굴림={diceRolledThisTurn}", this);
            DiceRolled?.Invoke(false);
            return;
        }

        diceRolledThisTurn = true;
        currentDiceValue = Random.Range(1, 7);
        SyncTurnUI();
        EnsureTurnDebugView();
        turnDebugView.ShowDice(currentDiceValue);

        ResolvePlayerActionController();
        if (playerActionController != null)
        {
            playerActionController.SetMoveRange(currentDiceValue);
        }

        ResolveCardPanelToggle();
        cardPanelToggle?.Show();

        // 주사위를 실제로 굴렸을 때만 true로 알린다. 추후 주사위 굴리는 연출(VFX)을 여기 걸면 된다.
        DiceRolled?.Invoke(true);
    }

    /// <summary>이동 완료 후 Debug 주사위 표시만 0으로 되돌린다.</summary>
    public void ResetDiceOnMove()
    {
        currentDiceValue = 0;
        EnsureTurnDebugView();
        turnDebugView.ShowDice(currentDiceValue);
    }

    /// <summary>현재 활성 Enemy를 순서대로 기다리며 실행한 후 Player 턴으로 복귀한다.
    /// 실제로 행동(이동/공격)한 Enemy가 하나도 없었으면 다음 Player 턴은 페이드·배너 없이
    /// 곧바로 시작한다.</summary>
    private IEnumerator TriggerEnemyTurn()
    {
        ResolveBattleDataPool();
        EnsureEnemyTurnRunner();
        BeginModalInteraction();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);
        int enemyRound = Mathf.Max(1, totalTurn - 1);
        BattleCombatLog.Add($"TURN {enemyRound}  ENEMY TURN");
        yield return turnAnnouncementView.ShowEnemyTurnRoutine(enemyRound, 1f);
        EndModalInteraction();
        yield return enemyTurnRunner.RunAll(battleDataPool);
        StartPlayerTurn(showAnnouncement: enemyTurnRunner.AnyEnemyActedLastRun);
    }

    /// <summary>HUD의 캐릭터 정보 화면에 상태 갱신과 전투 입력 잠금 기능을 연결한다.</summary>
    private void EnsureCharacterStatusController()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        //MapGenerator 하위의 오브젝트만 가져오는걸로 Find너무 빡임.

        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || candidate.name != "CharacterListUI")
            {
                continue;
            }

            BattleComponentResolver.GetOrAdd<CharacterListUIStatusController>(
                candidate.gameObject,
                candidate.GetComponent<CharacterListUIStatusController>());
            return;
        }
    }

    /// <summary>씬 설치기가 구성한 전투 데이터 저장소 참조를 보완한다.</summary>
    private void ResolveBattleDataPool()
    {
        if (battleDataPool == null)
        {
            battleDataPool = FindFirstObjectByType<BattleDataPool>(FindObjectsInactive.Include);
        }
    }

    /// <summary>현재 턴 번호와 주사위 값을 전용 View에 전달한다.</summary>
    private void RefreshDebugView()
    {
        EnsureTurnDebugView();
        turnDebugView.ShowTurn(totalTurn);
        turnDebugView.ShowDice(currentDiceValue);
    }

    /// <summary>현재 턴과 주사위 상태에 맞춰 주사위 및 턴 종료 버튼 사용 여부를 갱신한다.</summary>
    private void SyncTurnUI()
    {
        EnsureTurnButtonController();
        turnButtonController.ApplyTurnState(isPlayerTurn);
        if (turnEndButton != null && IsModalInteractionOpen)
            turnEndButton.interactable = false;

        // 독립 주사위 버튼은 플레이어 턴이면서 아직 굴리지 않았을 때만 보이고 눌린다.
        if (diceButton != null && diceButton != turnEndButton)
        {
            bool canRollNow = isPlayerTurn && !diceRolledThisTurn && !battleStopped;
            diceButton.gameObject.SetActive(canRollNow);
            diceButton.interactable = canRollNow && !IsModalInteractionOpen;
        }else
        {
            Debug.Log("없음");
        }

        CardUseAvailabilityChanged?.Invoke(CanUsePlayerCards);
    }

    /// <summary>턴 버튼 이벤트와 상태 표시를 담당하는 전용 컴포넌트를 확보한다.</summary>
    private void EnsureTurnButtonController()
    {
        turnButtonController = BattleComponentResolver.GetOrAdd(gameObject, turnButtonController);
    }

    /// <summary>Enemy 목록 조회와 순차 행동 실행을 담당하는 전용 컴포넌트를 확보한다.</summary>
    private void EnsureEnemyTurnRunner()
    {
        enemyTurnRunner = BattleComponentResolver.GetOrAdd(gameObject, enemyTurnRunner);
    }

    /// <summary>턴과 주사위 디버그 텍스트 표시를 담당하는 전용 View를 확보한다.</summary>
    private void EnsureTurnDebugView()
    {
        turnDebugView = BattleComponentResolver.GetOrAdd(gameObject, turnDebugView);

        turnDebugView.Configure(turnDebugText, diceDebugText);
    }

    /// <summary>플레이어 턴 시작 시 이동 입력기의 선택, 이동, 범위 표시 상태를 초기화한다.</summary>
    private void ResetPlayerMoveState()
    {
        ResolvePlayerActionController();
        if (playerActionController != null)
        {
            playerActionController.ResetTurnMoveState();
        }
    }

    /// <summary>Moon 씬의 명시 참조를 우선 사용하고 이전 씬에서는 최초 한 번만 제어기를 찾는다.</summary>
    private void ResolvePlayerActionController()
    {
        if (playerActionController == null)
        {
            playerActionController = FindFirstObjectByType<BattlePlayerActionController>(FindObjectsInactive.Include);
        }
    }

    /// <summary>비활성 전투 UI까지 포함해 카드 패널 제어기를 찾아 재사용한다.</summary>
    private void ResolveCardPanelToggle()
    {
        if (cardPanelToggle == null)
            cardPanelToggle = FindFirstObjectByType<BattleCardPanelToggle>(FindObjectsInactive.Include);
    }

    /// <summary>새 Player 턴과 Enemy 턴에는 카드 패널을 숨겨 주사위 이후에만 표시한다.</summary>
    private void HideCardPanelUntilDice()
    {
        ResolveCardPanelToggle();
        cardPanelToggle?.Hide();
    }

}
