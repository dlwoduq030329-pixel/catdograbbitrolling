using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 단계가 끝난 뒤 맵·Player·Enemy·카메라가 준비되는 순서를 기다리고 전투를 시작하는 Scene 로더다.
/// UI의 세부 표현이나 전투 규칙은 관리하지 않으며, 선택 Canvas에서 Battle Canvas로 넘어가는 시작 순서만 연결한다.
/// </summary>
public class BattleUIFlowController : MonoBehaviour
{
    [Header("캔버스 참조")]
    [InspectorName("플레이어 선택 캔버스")]
    [FormerlySerializedAs("playerSelectCanvas")]
    [SerializeField, Tooltip("전투 시작 전 캐릭터를 선택하는 UI 전체입니다. 전투 준비가 끝나면 숨깁니다.")]
    private GameObject characterSelectionCanvas;
    [InspectorName("전투 캔버스")]
    [FormerlySerializedAs("battleCanvas")]
    [SerializeField, Tooltip("맵·유닛·카메라 준비와 스테이지 안내가 끝난 뒤 표시할 전투 HUD 전체입니다.")]
    private GameObject battleHudCanvas;

    [Header("전환 시스템 참조")]
    [InspectorName("맵 생성기")]
    [FormerlySerializedAs("mapGenerator")]
    [SerializeField, Tooltip("기존 맵 생성 완료 상태를 제공하는 생성기입니다. 두 생성기가 준비될 때까지 전투 진입을 기다립니다.")]
    private MapGenerator legacyMapGenerator;
    [InspectorName("Renew 맵 생성기")]
    [FormerlySerializedAs("newmapGenerator")]
    [SerializeField, Tooltip("Renew 전투 맵 생성 완료 상태를 제공하는 생성기입니다.")]
    private NewMapGenerator renewMapGenerator;

    [InspectorName("전투 게임 관리자")]
    [SerializeField, Tooltip("생성된 Player 등록, 스테이지 안내와 첫 Player 턴 시작을 요청할 전투 진행 관리자입니다.")]
    private BattleGameManager battleGameManager;
    [InspectorName("플레이어 생성기")]
    [FormerlySerializedAs("playerSpawner")]
    [SerializeField, Tooltip("맵 생성 뒤 실제로 생성된 Player 인스턴스를 제공하는 컴포넌트입니다.")]
    private SpawnPlayer spawnedPlayerProvider;
    [InspectorName("적 생성기")]
    [SerializeField, Tooltip("등록된 Player 위치를 기준으로 전투 Enemy 생성을 시작할 컴포넌트입니다.")]
    private EnemySpawner enemySpawner;

    [Header("카메라 전환 완료 판정")]
    [InspectorName("전환 카메라")]
    [FormerlySerializedAs("transitionCamera")]
    [SerializeField, Tooltip("Player 생성 후 추적 이동이 멈출 때까지 기다리고 전투 입력을 켤 카메라입니다.")]
    private Camera battleCamera;
    [InspectorName("카메라 정지 판정 거리")]
    [FormerlySerializedAs("cameraStopThreshold")]
    [SerializeField, Min(0.0001f), Tooltip("프레임 사이 카메라 이동 거리가 이 값 이하면 정지한 프레임으로 계산합니다.")]
    private float cameraStoppedDistanceThreshold = 0.01f;
    [InspectorName("정지 확인 프레임 수")]
    [FormerlySerializedAs("cameraStableFrameCount")]
    [SerializeField, Min(1), Tooltip("카메라 이동 완료로 인정하기 위해 연속으로 정지해야 하는 프레임 수입니다.")]
    private int requiredCameraStoppedFrames = 5;
    [InspectorName("카메라 대기 제한 시간")]
    [FormerlySerializedAs("cameraWaitTimeout")]
    [SerializeField, Min(0.1f), Tooltip("카메라가 멈추지 않아도 전투 진입을 계속할 최대 대기 시간입니다.")]
    private float maximumCameraWaitSeconds = 10f;

    private Coroutine battleStartupRoutine;
    private GameObject battleStartupClickBlocker;

    /// <summary>Main Camera fallback을 보완하고 최초 화면을 캐릭터 선택 단계로 맞춘다.</summary>
    private void Awake()
    {
        if (battleCamera == null)
        {
            battleCamera = Camera.main;
        }

        ShowInitialCharacterSelectionPanel();
        ShowCharacterSelectionOrBattleCanvas(showBattle: false);
    }

    /// <summary>Scene 시작 후 캐릭터 선택 코드와 별개로 맵 생성 완료를 기다리는 전투 시작 절차를 등록한다.</summary>
    private void Start()
    {
        // 버튼 OnClick 연결 여부와 무관하게 캐릭터 선택 뒤 시작되는 맵 생성을 감시한다.
        StartBattleSceneLoading();
    }

    /// <summary>
    /// 캐릭터 확정 후 맵 생성, 유닛 등록, 카메라 정지, 스테이지 안내 순서로 전투 Scene 로딩을 시작한다.
    /// Start와 캐릭터 확정 버튼 양쪽에서 호출되어도 Coroutine 하나만 실행하도록 중복 요청을 무시한다.
    /// </summary>
    public void StartBattleSceneLoading()
    {
        if (battleStartupRoutine != null)
        {
            Debug.Log("리턴됨");
            return;
            
        }

        battleStartupRoutine = StartCoroutine(LoadBattleAfterMapGeneration());
        
    }

    /// <summary>
    /// 맵 생성 완료 → 입력 차단 → Player 등록 → Enemy 생성 → 카메라 정지 → 스테이지 안내 → 첫 턴 시작을 순서대로 실행한다.
    /// 이 순서를 한 Coroutine에 유지하여 준비되지 않은 Player나 맵을 후속 시스템이 먼저 참조하지 않게 한다.
    /// </summary>
    private IEnumerator LoadBattleAfterMapGeneration()
    {
        // 같은 버튼의 StatusUI.summonPlayer()가 MapGenerator 상태를 먼저 초기화하도록 한 Frame 양보한다.
        yield return null;
        Debug.Log("진입 성공");
        // Scene마다 Legacy 또는 Renew 생성기 하나만 사용할 수 있다. 둘 다 없을 때만 설정 오류다.
        if (legacyMapGenerator == null && renewMapGenerator == null)
        {
            Debug.LogError("전투 UI 전환 실패: 맵 생성기 참조가 없습니다.", this);
            battleStartupRoutine = null;
            yield break;
        }

        // 연결된 생성기만 검사한다. 두 생성기가 함께 있는 전환기 Scene에서는 둘 다 완료될 때까지 기다린다.
        while ((legacyMapGenerator != null && !legacyMapGenerator.IsGenerateEnd()) ||
               (renewMapGenerator != null && !renewMapGenerator.IsGenerateEnd()))
        {
            Debug.Log("한무대기");

            yield return null;
        }
        Debug.Log("맵 생성 확인 완료");

        // SpawnPlayer가 같은 프레임에 HUDCanvas를 활성화하더라도 EventSystem 입력보다 먼저
        // 최상위 차단막을 올려 연타 입력이 인벤토리/캐릭터 정보창을 열지 못하게 한다.
        SetBattleStartupClickBlockerActive(true);
        Debug.Log("맵 생성 확인 완료");

        if (battleGameManager == null)
        {
            Debug.LogError("전투 UI 전환 실패: 전투 게임 관리자 참조가 없습니다.", this);
            battleStartupRoutine = null;
            yield break;
        }

        if (spawnedPlayerProvider == null || spawnedPlayerProvider.SpawnedPlayer == null)
        {
            Debug.LogError("전투 UI 전환 실패: SpawnPlayer의 생성 결과가 없습니다.", this);
            battleStartupRoutine = null;
            yield break;
        }

        battleGameManager.RegisterPlayer(spawnedPlayerProvider.SpawnedPlayer);

        enemySpawner?.SpawnEnemiesOnGeneratedMap(battleGameManager.CurrentPlayer.transform);

        yield return WaitUntilBattleCameraStops();

        ShowCharacterSelectionOrBattleCanvas(showBattle: true);
        // 전투 진입 중 Player·카메라·HUD 입력 상태는 BattleOverlayUiController 한 곳에서만 관리한다.
        // 여기서 같은 HUD CanvasGroup을 별도로 잠그면 이미 잠긴 false/false를 원래 상태로 잘못 저장해,
        // 진입 연출이 끝난 뒤 Overlay가 복구한 HUD를 다시 비활성화하는 이중 관리 버그가 발생한다.
        battleGameManager.LockBattleInputForOverlay();
        yield return battleGameManager.PlayStageIntro();
        // SpawnPlayer.waitUnitMApGen()이 맵 생성 완료 시점에 LoadingUI로 이미 페이드 아웃/인을
        // 한 번 재생한다(화면 검게 → HUD 활성화 → 다시 밝게). 여기서 곧바로 이어지는 최초
        // StartPlayerTurn까지 기본값(showAnnouncement: true)으로 호출하면 PlayPlayerTurnAnnouncementLocked가
        // 페이드를 한 번 더 재생해 Battle Scene 진입 중 페이드가 총 2번(입장 1번 + Turn 1 1번)
        // 발생한다. 진입 시점의 첫 턴만 배너·페이드를 생략해 정확히 한 번만 페이드가 일어나게 한다.
        battleGameManager.StartPlayerTurn(showAnnouncement: false);

        // showAnnouncement: false라 StartPlayerTurn은 더 이상 모달 잠금을 새로 걸지 않으므로,
        // 여기서 진입 전용 잠금(LockBattleInputForOverlay)만 정상적으로 해제하면 곧바로 열린다.
        battleGameManager.UnlockBattleInputAfterOverlay();
        while (battleGameManager.IsModalInteractionOpen)
        {
            yield return null;
        }

        SetBattleStartupClickBlockerActive(false);

        battleStartupRoutine = null;
    }

    /// <summary>전투 첫 연출 동안 투명한 최상위 Raycast 이미지를 켜서 연타가 뒤쪽 HUD에 전달되지 않게 한다.</summary>
    private void SetBattleStartupClickBlockerActive(bool shouldBlockClicks)
    {
        if (battleStartupClickBlocker == null)
        {
            battleStartupClickBlocker = new GameObject(
                "Battle Startup Input Blocker",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(Image));

            Canvas blockerCanvas = battleStartupClickBlocker.GetComponent<Canvas>();
            blockerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            blockerCanvas.sortingOrder = 32760;

            RectTransform blockerRect = battleStartupClickBlocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;

            Image blockerImage = battleStartupClickBlocker.GetComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;
        }

        battleStartupClickBlocker.SetActive(shouldBlockClicks);
        if (shouldBlockClicks && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>이전 Player 선택 코드를 수정하지 않고 선택 Canvas 아래의 초기 State 화면을 활성화한다.</summary>
    private void ShowInitialCharacterSelectionPanel()
    {
        if (characterSelectionCanvas == null)
        {
            return;
        }

        Transform[] children = characterSelectionCanvas.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == "State")
            {
                child.gameObject.SetActive(true);
                return;
            }
        }
    }

    /// <summary>Battle Camera 위치가 지정 프레임 동안 충분히 정지하거나 최대 대기 시간을 넘길 때까지 기다린다.</summary>
    private IEnumerator WaitUntilBattleCameraStops()
    {
        if (battleCamera == null)
        {
            Debug.LogWarning("전환 카메라 참조가 없어 기다리지 않고 전투 UI를 표시합니다.", this);
            yield break;
        }

        // SpawnPlayer의 CameraChase.InitTarget 호출과 첫 LateUpdate 이동이 반영될 때까지 기다린다.
        yield return new WaitForEndOfFrame();

        Vector3 previousPosition = battleCamera.transform.position;
        int stableFrames = 0;
        float elapsed = 0f;

        while (stableFrames < requiredCameraStoppedFrames && elapsed < maximumCameraWaitSeconds)
        {
            yield return new WaitForEndOfFrame();

            float movedDistance = Vector3.Distance(previousPosition, battleCamera.transform.position);
            stableFrames = movedDistance <= cameraStoppedDistanceThreshold ? stableFrames + 1 : 0;
            previousPosition = battleCamera.transform.position;
            elapsed += Time.unscaledDeltaTime;
        }

        if (elapsed >= maximumCameraWaitSeconds)
        {
            Debug.LogWarning("카메라 전환 대기 시간이 초과되어 전투 UI를 표시합니다.", this);
        }
    }

    /// <summary>캐릭터 선택 Canvas와 Battle HUD를 상호 배타적으로 표시하고 카메라 입력 상태도 같은 단계로 맞춘다.</summary>
    private void ShowCharacterSelectionOrBattleCanvas(bool showBattle)
    {
        if (characterSelectionCanvas != null)
        {
            if (!showBattle && characterSelectionCanvas.transform.localScale.sqrMagnitude < 0.0001f)
            {
                characterSelectionCanvas.transform.localScale = Vector3.one;
            }

            characterSelectionCanvas.SetActive(!showBattle);
        }

        if (battleHudCanvas != null)
        {
            if (showBattle && battleHudCanvas.transform.localScale.sqrMagnitude < 0.0001f)
            {
                battleHudCanvas.transform.localScale = Vector3.one;
            }

            battleHudCanvas.SetActive(showBattle);
        }

        SetBattleCameraInputEnabled(showBattle);
    }

    /// <summary>
    /// Battle Camera 입력을 현재 화면 단계에 맞춰 켜거나 끈다.
    /// 현재는 이전 Scene 호환을 위해 컴포넌트가 없을 때 자동 추가하며, 직접 참조 전환 후 이 생성 분기는 삭제한다.
    /// </summary>
    private void SetBattleCameraInputEnabled(bool shouldEnableInput)
    {
        if (battleCamera == null)
        {
            return;
        }

        BattleMapCameraInput cameraInput = battleCamera.GetComponent<BattleMapCameraInput>();
        if (cameraInput == null && shouldEnableInput)
        {
            cameraInput = battleCamera.gameObject.AddComponent<BattleMapCameraInput>();
        }

        if (cameraInput != null)
        {
            cameraInput.SetInputEnabled(shouldEnableInput);
        }
    }
}
