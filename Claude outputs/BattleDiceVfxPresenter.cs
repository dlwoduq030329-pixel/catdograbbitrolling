using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 확정된 주사위 값에 맞춰 3D 주사위 Animator State와 결과 UI를 순서대로 보여준다.
/// 애니메이션 참조가 없어도 결과 표시와 전투 진행은 유지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDiceVfxPresenter : MonoBehaviour
{
    private const string DiceResultAnimationStatePrefix = "Dice_Red_Result_";

    [Header("3D 주사위 애니메이션")]
    [Tooltip("Scene에 미리 배치한 Dice 오브젝트입니다. Presenter는 생성·삭제하지 않고 활성 상태만 바꿉니다.")]
    [SerializeField] private GameObject sceneDiceObject;
    [Tooltip("Scene Dice에 연결된 Animator입니다. Dice_Red_Result_1~6 State를 직접 재생합니다.")]
    [SerializeField] private Animator sceneDiceAnimator;
    [Tooltip("Battle 하이라키에 배치된 주사위 연출 무대입니다. 굴릴 때 Camera 화면 기준 위치로 이동합니다.")]
    [SerializeField] private Transform dicePresentationStage;
    [Tooltip("주사위 무대를 화면 기준 위치로 옮길 전투 Camera입니다. Scene에서 직접 연결합니다.")]
    [SerializeField] private Camera battleCamera;
    [Tooltip("화면 안에서 주사위 무대가 보일 위치입니다. (0.5, 0.5)는 화면 정중앙입니다.")]
    [SerializeField] private Vector2 stageViewportPosition = new Vector2(0.5f, 0.45f);
    [Tooltip("Camera로부터 주사위 무대까지의 거리입니다.")]
    [SerializeField, Min(0.31f)] private float stageDistanceFromCamera = 8f;
    [Tooltip("원본 Dice Clip이 사용하는 약 28x28 좌표 공간을 Camera 화면 안에 축소하는 배율입니다.")]
    [SerializeField, Min(0.01f)] private float legacyAnimationSpaceScale = 0.18f;
    [Tooltip("원본 Animation 이동 범위의 중앙 좌표입니다. Clip 좌표를 화면 중앙에 맞출 때 사용합니다.")]
    [SerializeField] private Vector3 legacyAnimationSpaceCenter = new Vector3(13.87f, 0.74f, 1f);

    [Header("결과 표시")]
    [Tooltip("1~6 Sprite가 들어 있는 기존 Dice 결과 UI 프리팹입니다.")]
    [SerializeField] private GameObject diceResultPrefab;
    [Tooltip("Animator State 길이를 읽지 못했을 때만 사용하는 예비 굴림 시간입니다.")]
    [SerializeField, Min(0f)] private float fallbackRollDurationSeconds = 2.3f;
    [Tooltip("주사위가 착지한 뒤 최종 눈금을 읽을 수 있도록 그대로 유지하는 시간입니다.")]
    [SerializeField, Min(0f)] private float landedResultHoldSeconds = 1.2f;
    [SerializeField, Min(0f)] private float resultDisplaySeconds = 0.8f;

    private BattleGameManager battleGameManager;
    private BattleDiceSystem diceSystem;
    private GameObject presentationCanvasObject;
    private GameObject spawnedResultView;
    private diceResultUI diceResultView;
    private Coroutine presentationRoutine;
    private bool holdsInputLock;
    private int presentingDiceValue;
    private BattleCameraRig lockedCameraRig;

    private void Start() => BindToBattleGameManager();

    /// <summary>
    /// OnDisable에서 구독을 해제하므로, 이 오브젝트가 다시 켜질 때(예: 오버레이 UI가 이 오브젝트를
    /// 껐다 켜는 경우) 다시 구독하지 않으면 이후 주사위 결과 연출이 영구히 멈춘다.
    /// BindToBattleGameManager는 이미 같은 대상에 중복 구독하지 않도록 방어돼 있다.
    /// </summary>
    private void OnEnable() => BindToBattleGameManager();

    private void OnDisable()
    {
        if (diceSystem != null)
            diceSystem.DiceRollResolved -= HandleDiceRollResult;

        if (presentationRoutine != null)
        {
            StopCoroutine(presentationRoutine);
            presentationRoutine = null;
        }

        HideSceneDice();
        SetResultVisible(false);
        // diceSystem 참조가 살아있는 동안 진행 중이던 연출을 먼저 마무리한다(턴 흐름이 멈추지 않도록).
        CompleteInterruptedPresentation();

        // 구독 대상 캐시는 완료 처리 이후에 비운다. 비우지 않으면 BindToBattleGameManager가
        // "이미 같은 대상에 연결돼 있다"고 착각해 OnEnable에서 재구독을 건너뛴다.
        battleGameManager = null;
        diceSystem = null;
    }

    /// <summary>현재 씬의 BattleGameManager가 발행하는 주사위 결과 이벤트를 구독한다.</summary>
    private void BindToBattleGameManager()
    {
        BattleGameManager manager = BattleGameManager.Instance;
        BattleDiceSystem resolvedDiceSystem = manager != null ? manager.DiceSystem : null;
        if (manager == null || resolvedDiceSystem == null ||
            (ReferenceEquals(manager, battleGameManager) && ReferenceEquals(resolvedDiceSystem, diceSystem)))
            return;

        if (diceSystem != null)
            diceSystem.DiceRollResolved -= HandleDiceRollResult;

        battleGameManager = manager;
        diceSystem = resolvedDiceSystem;
        diceSystem.DiceRollResolved -= HandleDiceRollResult;
        diceSystem.DiceRollResolved += HandleDiceRollResult;
    }

    /// <summary>Dice System이 전달한 성공한 굴림과 확정 숫자만 연출한다.</summary>
    private void HandleDiceRollResult(bool rollSucceeded, int diceValue)
    {
        if (!rollSucceeded || diceSystem == null) return;

        if (diceValue < 1 || diceValue > 6)
        {
            Debug.LogError($"[Dice VFX] 표시할 수 없는 주사위 값입니다: {diceValue}", this);
            diceSystem.CompletePresentation(diceValue);
            return;
        }

        if (presentationRoutine != null) StopCoroutine(presentationRoutine);
        presentationRoutine = StartCoroutine(PlayDicePresentation(diceValue));
    }

    /// <summary>
    /// 결과값에 대응하는 3D 주사위 애니메이션을 먼저 재생하고 숫자 UI를 표시한다.
    /// try/finally로 감싼 이유: Animator State(예: Dice_Red_Result_1~6)가 지워지거나 이름이 바뀌는
    /// 등 예상 못한 예외가 코루틴 중간에 나더라도 FinishPresentation은 반드시 한 번 실행돼야 한다.
    /// 그렇지 않으면 AcquireInputLock으로 잠근 입력(LockBattleInputForOverlay)이 영원히 풀리지 않아,
    /// 주사위뿐 아니라 카드 사용까지(CanUsePlayerCards가 !IsBattleBlockingUiOpen을 요구) 막혀버린다.
    /// (단, Unity의 StopCoroutine은 finally를 실행하지 않으므로 OnDisable 쪽 정리는 별도로 유지한다.)
    /// </summary>
    private IEnumerator PlayDicePresentation(int diceValue)
    {
        presentingDiceValue = diceValue;
        AcquireInputLock();
        try
        {
            PrepareSceneDiceStage();

            if (sceneDiceObject != null && sceneDiceAnimator != null)
            {
                // Legacy animSet과 같은 순서다. Rebind 직후 한 프레임을 넘긴 다음 시작 Transform을 복원하고,
                // 다시 한 프레임을 넘긴 뒤 State를 재생해야 Animator 초기화와 Transform Animation이 충돌하지 않는다.
                sceneDiceAnimator.Rebind();
                sceneDiceAnimator.Update(0f);
                yield return null;

                sceneDiceObject.transform.localPosition = Vector3.zero;
                sceneDiceObject.transform.localRotation = Quaternion.Euler(37.109f, 0f, 0f);
                yield return null;

                float animationDurationSeconds = PlayDiceAnimationState(diceValue);
                // 굴림이 끝날 때까지 기다린 뒤, 착지한 최종 면을 별도 시간 동안 그대로 보여준다.
                yield return new WaitForSecondsRealtime(animationDurationSeconds);
                yield return new WaitForSecondsRealtime(landedResultHoldSeconds);
            }

            HideSceneDice();
            ShowDiceResult(diceValue);
            yield return new WaitForSecondsRealtime(resultDisplaySeconds);
        }
        finally
        {
            FinishPresentation(diceValue);
        }
    }

    /// <summary>
    /// Scene에 배치된 주사위를 켜고 확정값과 같은 `Dice_Red_Result_1~6` State를 처음부터 재생한다.
    /// 이 함수는 주사위를 생성하거나 Animator를 런타임에 추가하지 않는다.
    /// </summary>
    private void PrepareSceneDiceStage()
    {
        HideSceneDice();
        if (sceneDiceObject == null || sceneDiceAnimator == null)
            return;

        if (dicePresentationStage != null)
        {
            PositionDiceStageInFrontOfCamera();
            dicePresentationStage.gameObject.SetActive(true);
        }
        sceneDiceObject.SetActive(true);
    }

    /// <summary>
    /// Legacy animSet처럼 확정값에 대응하는 짧은 State 이름을 직접 재생하고 실제 재생시간을 반환한다.
    /// State가 삭제·이름 변경 등으로 없어졌거나 Animator에 Controller 자체가 비어 있어도 예외를 던지지
    /// 않고 fallbackRollDurationSeconds로 대체한다(PlayDicePresentation의 try/finally와는 별개의
    /// 보호 장치 — 여기서 막으면 애초에 예외가 발생하지 않아 더 안전하다).
    /// </summary>
    private float PlayDiceAnimationState(int diceValue)
    {
        try
        {
            string resultStateName = DiceResultAnimationStatePrefix + diceValue;
            sceneDiceAnimator.Play(resultStateName, 0, 0f);
            sceneDiceAnimator.Update(0f);

            // State의 원본 Clip 길이를 State 속도로 나눠 실제 재생시간을 계산한다.
            // 현재 Clip은 약 6.63초, State 속도는 3배이므로 실제 굴림은 약 2.21초다.
            AnimatorStateInfo playingState = sceneDiceAnimator.GetCurrentAnimatorStateInfo(0);
            float playbackSpeed = Mathf.Abs(playingState.speed * playingState.speedMultiplier);
            return playingState.length > 0f && playbackSpeed > 0f
                ? playingState.length / playbackSpeed
                : fallbackRollDurationSeconds;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"[Dice VFX] 주사위 State 재생 실패({DiceResultAnimationStatePrefix}{diceValue}) — " +
                $"예비 시간({fallbackRollDurationSeconds}s)으로 대체합니다: {exception.Message}",
                this);
            return fallbackRollDurationSeconds;
        }
    }

    /// <summary>
    /// 원본 Clip의 약 28x28 이동 좌표 전체가 현재 전투 Camera 안에 들어오도록 무대를 축소·중앙 정렬한다.
    /// Legacy는 Camera Timeline이 고정된 Dice 좌표를 찾아갔지만, Renew는 같은 효과를 현재 Camera 기준으로 계산한다.
    /// </summary>
    private void PositionDiceStageInFrontOfCamera()
    {
        if (dicePresentationStage == null || battleCamera == null)
            return;

        float safeDistance = Mathf.Max(
            battleCamera.nearClipPlane + 0.01f,
            stageDistanceFromCamera);

        Vector3 desiredAnimationCenter = battleCamera.ViewportToWorldPoint(
            new Vector3(stageViewportPosition.x, stageViewportPosition.y, safeDistance));

        // 무대 회전은 절대 건드리지 않는다. 이전에는 여기서 매 굴림마다
        // dicePresentationStage.rotation = battleCamera.transform.rotation로 카메라 각도를
        // 그대로 베껴왔는데, 그 결과 카메라가 틸트돼 있을 때마다 주사위 텀블링 애니메이션 전체가
        // 같이 기울어져 보이는 문제가 있었다. 무대는 Inspector에 저장된 고정 회전값을 그대로
        // 쓰고, 여기서는 카메라 화면 기준 위치(이동)만 맞춘다.
        dicePresentationStage.localScale = Vector3.one * legacyAnimationSpaceScale;

        // Animator가 Dice.localPosition을 원본 절대 좌표로 덮어쓰므로, 그 좌표의 중앙만큼 Stage를 반대로 이동한다.
        // 결과적으로 어떤 눈금 Clip이 재생되어도 전체 이동 범위의 중앙이 지정한 Viewport 위치와 일치한다.
        Vector3 worldOffsetToLegacyCenter =
            dicePresentationStage.TransformVector(legacyAnimationSpaceCenter);
        dicePresentationStage.position = desiredAnimationCenter - worldOffsetToLegacyCenter;
    }

    /// <summary>기존 Dice 결과 프리팹을 한 번 생성해 확정된 1~6 Sprite를 표시한다.</summary>
    private void ShowDiceResult(int diceValue)
    {
        if (spawnedResultView == null && diceResultPrefab != null)
        {
            EnsurePresentationCanvasCreated();
            spawnedResultView = Instantiate(diceResultPrefab, presentationCanvasObject.transform, false);
            diceResultView = spawnedResultView.GetComponent<diceResultUI>();
        }

        if (spawnedResultView == null)
        {
            Debug.Log($"[Dice] 주사위 {diceValue}이(가) 나왔습니다.", this);
            return;
        }

        spawnedResultView.SetActive(true);
        diceResultView?.Init(diceValue);
    }

    private void EnsurePresentationCanvasCreated()
    {
        if (presentationCanvasObject != null) return;

        presentationCanvasObject = new GameObject(
            "Battle Dice Presentation Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = presentationCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = presentationCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
    }

    private void FinishPresentation(int diceValue)
    {
        SetResultVisible(false);
        diceSystem?.CompletePresentation(diceValue);
        ReleaseInputLock();
        presentingDiceValue = 0;
        presentationRoutine = null;
    }

    private void CompleteInterruptedPresentation()
    {
        if (presentingDiceValue > 0)
            diceSystem?.CompletePresentation(presentingDiceValue);

        presentingDiceValue = 0;
        ReleaseInputLock();
    }

    /// <summary>Scene 주사위를 삭제하지 않고 꺼서 다음 굴림 때 같은 오브젝트를 재사용한다.</summary>
    private void HideSceneDice()
    {
        if (sceneDiceObject != null) sceneDiceObject.SetActive(false);
        if (dicePresentationStage != null) dicePresentationStage.gameObject.SetActive(false);
    }

    private void SetResultVisible(bool visible)
    {
        if (spawnedResultView != null) spawnedResultView.SetActive(visible);
    }

    /// <summary>
    /// LockBattleInputForOverlay는 BattleMapCameraInput(마우스 드래그·줌 입력)만 잠근다.
    /// 하지만 BattleCameraRig.LateUpdate()는 입력 잠금과 무관하게 매 프레임 Player를 계속
    /// 따라가므로, Rig까지 꺼두지 않으면 롤 애니메이션이 재생되는 동안 카메라가 계속 움직이고
    /// PositionDiceStageInFrontOfCamera()가 굴림 시작 시점에 딱 한 번만 계산해둔 주사위 무대
    /// 위치는 그대로 남아 화면 기준에서 어긋나 보인다("주사위가 혼자 이상한 데 떠 있음").
    /// Rig를 통째로 꺼두면 카메라가 그 자리에 완전히 고정되므로 별도 매 프레임 재계산 없이도
    /// 무대 위치가 항상 화면 기준과 일치한다.
    /// </summary>
    private void AcquireInputLock()
    {
        if (holdsInputLock || battleGameManager == null) return;
        battleGameManager.LockBattleInputForOverlay();
        holdsInputLock = true;

        if (battleCamera != null)
        {
            lockedCameraRig = battleCamera.GetComponent<BattleCameraRig>();
            if (lockedCameraRig != null)
            {
                lockedCameraRig.enabled = false;
            }
        }
    }

    private void ReleaseInputLock()
    {
        if (!holdsInputLock) return;
        holdsInputLock = false;
        battleGameManager?.UnlockBattleInputAfterOverlay();

        if (lockedCameraRig != null)
        {
            lockedCameraRig.enabled = true;
            lockedCameraRig = null;
        }
    }
}
