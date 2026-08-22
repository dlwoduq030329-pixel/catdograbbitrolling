using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 Canvas에서 전투 Canvas로 넘어가는 흐름만 담당한다.
/// 맵 생성과 카메라 이동 완료 상태를 확인해 화면을 전환한다.
/// </summary>
public class BattleUIFlowController : MonoBehaviour
{
    [Header("캔버스 참조")]
    [InspectorName("플레이어 선택 캔버스")]
    [SerializeField] private GameObject playerSelectCanvas;
    [InspectorName("전투 캔버스")]
    [SerializeField] private GameObject battleCanvas;

    [Header("전환 시스템 참조")]
    [InspectorName("맵 생성기")]
    [SerializeField] private MapGenerator mapGenerator;
    [InspectorName("맵 생성기")]
    [SerializeField] private NewMapGenerator newmapGenerator;

    [InspectorName("전투 게임 관리자")]
    [SerializeField] private BattleGameManager battleGameManager;
    [InspectorName("플레이어 생성기")]
    [SerializeField] private SpawnPlayer playerSpawner;
    [InspectorName("적 생성기")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("카메라 전환 완료 판정")]
    [InspectorName("전환 카메라")]
    [SerializeField] private Camera transitionCamera;
    [InspectorName("카메라 정지 판정 거리")]
    [SerializeField, Min(0.0001f)] private float cameraStopThreshold = 0.01f;
    [InspectorName("정지 확인 프레임 수")]
    [SerializeField, Min(1)] private int cameraStableFrameCount = 5;
    [InspectorName("카메라 대기 제한 시간")]
    [SerializeField, Min(0.1f)] private float cameraWaitTimeout = 10f;

    private Coroutine transitionRoutine;
    private CanvasGroup battleCanvasInputGroup;
    private bool battleCanvasWasInteractable;
    private bool battleCanvasWasBlockingRaycasts;
    private GameObject startupInputBlocker;

    /// <summary>전환 카메라를 보완하고 시작 화면을 캐릭터 선택 Canvas로 맞춘다.</summary>
    private void Awake()
    {
        if (transitionCamera == null)
        {
            transitionCamera = Camera.main;
        }

        EnsureInitialPlayerSelectionUI();
        SetCanvasState(showBattle: false);
    }

    /// <summary>캐릭터 선택 코드 수정 없이 맵 생성 상태를 관찰하는 전투 전환 절차를 시작한다.</summary>
    private void Start()
    {
        // 버튼 OnClick 연결 여부와 무관하게 캐릭터 선택 뒤 시작되는 맵 생성을 감시한다.
        BeginBattleTransition();
    }

    /// <summary>
    /// 캐릭터 확정 버튼에서도 호출할 수 있다.
    /// StatusUI.summonPlayer()와 함께 사용하면 맵 및 카메라 전환 완료 뒤 전투 UI가 열린다.
    /// </summary>
    /// <summary>캐릭터 선택 완료 후 카메라 이동을 기다리고 Player Select UI에서 Battle UI로 전환한다.</summary>
    public void BeginBattleTransition()
    {
        if (transitionRoutine != null)
        {
            Debug.Log("리턴됨");
            return;
            
        }

        transitionRoutine = StartCoroutine(WaitForMapAndShowBattle());
        
    }

    /// <summary>맵 생성, Player 등록, 카메라 이동을 순서대로 기다린다.</summary>
    private IEnumerator WaitForMapAndShowBattle()
    {
        // 같은 버튼의 StatusUI.summonPlayer()가 MapGenerator 상태를 먼저 초기화하도록 한 Frame 양보한다.
        yield return null;
        Debug.Log("진입 성공");
        if (mapGenerator == null||newmapGenerator == null)
        {
            Debug.LogError("전투 UI 전환 실패: 맵 생성기 참조가 없습니다.", this);
            transitionRoutine = null;
            yield break;
        }

        while (!mapGenerator.IsGenerateEnd()&&!newmapGenerator.IsGenerateEnd())
        {
            Debug.Log("한무대기");

            yield return null;
        }
        Debug.Log("맵 생성 확인 완료");

        // SpawnPlayer가 같은 프레임에 HUDCanvas를 활성화하더라도 EventSystem 입력보다 먼저
        // 최상위 차단막을 올려 연타 입력이 인벤토리/캐릭터 정보창을 열지 못하게 한다.
        SetStartupInputBlocked(true);
        Debug.Log("맵 생성 확인 완료");

        if (battleGameManager == null)
        {
            Debug.LogError("전투 UI 전환 실패: 전투 게임 관리자 참조가 없습니다.", this);
            transitionRoutine = null;
            yield break;
        }

        if (playerSpawner == null || playerSpawner.SpawnedPlayer == null)
        {
            Debug.LogError("전투 UI 전환 실패: SpawnPlayer의 생성 결과가 없습니다.", this);
            transitionRoutine = null;
            yield break;
        }

        battleGameManager.RegisterPlayer(playerSpawner.SpawnedPlayer);

        enemySpawner?.SpawnEnemiesOnGeneratedMap(battleGameManager.CurrentPlayer.transform);

        yield return WaitForCameraMovementToFinish();

        SetCanvasState(showBattle: true);
        SetBattleCanvasInputLocked(true);
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

        SetBattleCanvasInputLocked(false);
        SetStartupInputBlocked(false);

        transitionRoutine = null;
    }

    /// <summary>전투 첫 연출 동안 모든 Canvas보다 앞에서 포인터 입력을 소비한다.</summary>
    private void SetStartupInputBlocked(bool blocked)
    {
        if (startupInputBlocker == null)
        {
            startupInputBlocker = new GameObject(
                "Battle Startup Input Blocker",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(Image));

            Canvas blockerCanvas = startupInputBlocker.GetComponent<Canvas>();
            blockerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            blockerCanvas.sortingOrder = 32760;

            RectTransform blockerRect = startupInputBlocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;

            Image blockerImage = startupInputBlocker.GetComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;
        }

        startupInputBlocker.SetActive(blocked);
        if (blocked && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>전투 진입 연출 동안 Battle Canvas의 모든 UI 상호작용을 일괄 차단한다.</summary>
    private void SetBattleCanvasInputLocked(bool locked)
    {
        if (battleCanvas == null) return;

        if (battleCanvasInputGroup == null)
        {
            battleCanvasInputGroup = battleCanvas.GetComponent<CanvasGroup>();
            if (battleCanvasInputGroup == null)
            {
                battleCanvasInputGroup = battleCanvas.AddComponent<CanvasGroup>();
            }
        }

        if (locked)
        {
            battleCanvasWasInteractable = battleCanvasInputGroup.interactable;
            battleCanvasWasBlockingRaycasts = battleCanvasInputGroup.blocksRaycasts;
            battleCanvasInputGroup.interactable = false;
            battleCanvasInputGroup.blocksRaycasts = true;
            BattleMapCameraInput.SetEnabledOnMainCamera(false);
            return;
        }

        battleCanvasInputGroup.interactable = battleCanvasWasInteractable;
        battleCanvasInputGroup.blocksRaycasts = battleCanvasWasBlockingRaycasts;
        BattleMapCameraInput.SetEnabledOnMainCamera(true);
    }

    /// <summary>원본 PlayerChoose를 수정하지 않고 초기 캐릭터 선택 State 화면을 활성화한다.</summary>
    private void EnsureInitialPlayerSelectionUI()
    {
        if (playerSelectCanvas == null)
        {
            return;
        }

        Transform[] children = playerSelectCanvas.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == "State")
            {
                child.gameObject.SetActive(true);
                return;
            }
        }
    }

    /// <summary>Camera 위치가 지정 Frame 동안 충분히 정지할 때까지 기다린다.</summary>
    private IEnumerator WaitForCameraMovementToFinish()
    {
        if (transitionCamera == null)
        {
            Debug.LogWarning("전환 카메라 참조가 없어 기다리지 않고 전투 UI를 표시합니다.", this);
            yield break;
        }

        // SpawnPlayer의 CameraChase.InitTarget 호출과 첫 LateUpdate 이동이 반영될 때까지 기다린다.
        yield return new WaitForEndOfFrame();

        Vector3 previousPosition = transitionCamera.transform.position;
        int stableFrames = 0;
        float elapsed = 0f;

        while (stableFrames < cameraStableFrameCount && elapsed < cameraWaitTimeout)
        {
            yield return new WaitForEndOfFrame();

            float movedDistance = Vector3.Distance(previousPosition, transitionCamera.transform.position);
            stableFrames = movedDistance <= cameraStopThreshold ? stableFrames + 1 : 0;
            previousPosition = transitionCamera.transform.position;
            elapsed += Time.unscaledDeltaTime;
        }

        if (elapsed >= cameraWaitTimeout)
        {
            Debug.LogWarning("카메라 전환 대기 시간이 초과되어 전투 UI를 표시합니다.", this);
        }
    }

    /// <summary>두 Canvas를 상호 배타적으로 전환하고 전투 카메라 입력 상태를 맞춘다.</summary>
    private void SetCanvasState(bool showBattle)
    {
        if (playerSelectCanvas != null)
        {
            if (!showBattle && playerSelectCanvas.transform.localScale.sqrMagnitude < 0.0001f)
            {
                playerSelectCanvas.transform.localScale = Vector3.one;
            }

            playerSelectCanvas.SetActive(!showBattle);
        }

        if (battleCanvas != null)
        {
            if (showBattle && battleCanvas.transform.localScale.sqrMagnitude < 0.0001f)
            {
                battleCanvas.transform.localScale = Vector3.one;
            }

            battleCanvas.SetActive(showBattle);
        }

        ConfigureBattleCameraInput(showBattle);
    }

    /// <summary>전환 Camera에 입력 컴포넌트가 없으면 런타임에 추가하고 활성 상태를 전달한다.</summary>
    private void ConfigureBattleCameraInput(bool enabled)
    {
        if (transitionCamera == null)
        {
            return;
        }

        BattleMapCameraInput cameraInput = transitionCamera.GetComponent<BattleMapCameraInput>();
        if (cameraInput == null && enabled)
        {
            cameraInput = transitionCamera.gameObject.AddComponent<BattleMapCameraInput>();
        }

        if (cameraInput != null)
        {
            cameraInput.SetInputEnabled(enabled);
        }
    }
}
