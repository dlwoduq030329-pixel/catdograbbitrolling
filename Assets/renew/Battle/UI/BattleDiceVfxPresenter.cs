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
    [Tooltip("Dice의 부모로 배치된 투명 연출 기준면입니다. 전투 오브젝트와 충돌하지 않고 Camera 기준 위치만 제공합니다.")]
    [SerializeField] private Transform dicePresentationFloor;
    [Tooltip("카메라 고정 표시를 사용하지 않을 때의 3D 주사위 생성 위치입니다. 비어 있으면 현재 Player 위치를 사용합니다.")]
    [SerializeField] private Transform animatedDiceSpawnPoint;
    [Tooltip("지정 위치 또는 Player 위치에서 3D 주사위를 얼마나 옮길지 정합니다.")]
    [SerializeField] private Vector3 animatedDicePositionOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Animator가 주사위를 아래로 이동시켜도 기준 바닥보다 내려가지 않게 유지할 최소 높이입니다.")]
    [SerializeField, Min(0f)] private float minimumHeightAboveDiceFloor = 0.15f;

    [Header("카메라 고정 표시")]
    [Tooltip("주사위를 전투 타일과 분리해 화면 안에 표시할 전투 카메라입니다. 연결되어 있으면 Player 위치보다 우선합니다.")]
    [SerializeField] private Camera dicePresentationCamera;
    [Tooltip("화면 안에서 주사위 연출의 기준 위치입니다. (0.5, 0.5)는 화면 정중앙입니다.")]
    [SerializeField] private Vector2 diceViewportPosition = new Vector2(0.5f, 0.55f);
    [Tooltip("카메라 앞에서 주사위를 표시할 거리입니다.")]
    [SerializeField, Min(0.1f)] private float diceDistanceFromCamera = 8f;
    [Tooltip("Animator가 움직여도 주사위가 들어가면 안 되는 화면 가장자리 비율입니다.")]
    [SerializeField, Range(0f, 0.45f)] private float viewportEdgePadding = 0.1f;

    [Header("결과 표시")]
    [Tooltip("1~6 Sprite가 들어 있는 기존 Dice 결과 UI 프리팹입니다.")]
    [SerializeField] private GameObject diceResultPrefab;
    [SerializeField, Min(0f)] private float rollVfxDurationSeconds = 1f;
    [SerializeField, Min(0f)] private float resultDisplaySeconds = 0.8f;

    private BattleGameManager battleGameManager;
    private BattleDiceSystem diceSystem;
    private GameObject presentationCanvasObject;
    private GameObject spawnedAnimatedDice;
    private GameObject spawnedResultView;
    private diceResultUI diceResultView;
    private Coroutine presentationRoutine;
    private bool holdsInputLock;
    private int presentingDiceValue;
    private float diceFloorWorldY;
    private bool usesCameraFixedPresentation;

    private void Start() => BindToBattleGameManager();

    /// <summary>
    /// 주사위 AnimationClip은 Transform 위치를 직접 기록하므로 일반 Collider 바닥만으로는 통과를 막을 수 없다.
    /// Camera 고정 모드에서는 Viewport 좌표를 제한해 화면 밖 이동과 지형 영향을 막고,
    /// Camera가 연결되지 않은 이전 월드 배치에서는 최저 Y만 제한한다.
    /// </summary>
    private void LateUpdate()
    {
        if (sceneDiceObject == null || !sceneDiceObject.activeSelf) return;

        if (usesCameraFixedPresentation && dicePresentationCamera != null)
        {
            Vector3 viewportPosition = dicePresentationCamera.WorldToViewportPoint(
                sceneDiceObject.transform.position);
            viewportPosition.x = Mathf.Clamp(
                viewportPosition.x, viewportEdgePadding, 1f - viewportEdgePadding);
            viewportPosition.y = Mathf.Clamp(
                viewportPosition.y, viewportEdgePadding, 1f - viewportEdgePadding);
            // Animation의 전후 이동도 카메라 뒤나 지나치게 먼 곳으로 벗어나지 않게 제한한다.
            viewportPosition.z = Mathf.Clamp(
                viewportPosition.z,
                diceDistanceFromCamera * 0.5f,
                diceDistanceFromCamera * 1.5f);
            sceneDiceObject.transform.position =
                dicePresentationCamera.ViewportToWorldPoint(viewportPosition);
            return;
        }

        Vector3 animatedPosition = sceneDiceObject.transform.position;
        if (animatedPosition.y >= diceFloorWorldY) return;

        animatedPosition.y = diceFloorWorldY;
        sceneDiceObject.transform.position = animatedPosition;
    }

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
        CompleteInterruptedPresentation();
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

    /// <summary>결과값에 대응하는 3D 주사위 애니메이션을 먼저 재생하고 숫자 UI를 표시한다.</summary>
    private IEnumerator PlayDicePresentation(int diceValue)
    {
        presentingDiceValue = diceValue;
        AcquireInputLock();
        ShowAndPlaySceneDice(diceValue);

        if (sceneDiceObject != null)
            yield return new WaitForSecondsRealtime(rollVfxDurationSeconds);

        HideSceneDice();
        ShowDiceResult(diceValue);
        yield return new WaitForSecondsRealtime(resultDisplaySeconds);
        FinishPresentation(diceValue);
    }

    /// <summary>
    /// Scene에 배치된 주사위를 켜고 확정값과 같은 `Dice_Red_Result_1~6` State를 처음부터 재생한다.
    /// 이 함수는 주사위를 생성하거나 Animator를 런타임에 추가하지 않는다.
    /// </summary>
    private void ShowAndPlaySceneDice(int diceValue)
    {
        HideSceneDice();
        if (sceneDiceObject == null || sceneDiceAnimator == null) return;

        usesCameraFixedPresentation = dicePresentationCamera != null;
        Vector3 basePosition = usesCameraFixedPresentation
            ? dicePresentationCamera.ViewportToWorldPoint(new Vector3(
                diceViewportPosition.x,
                diceViewportPosition.y,
                diceDistanceFromCamera))
            : animatedDiceSpawnPoint != null
                ? animatedDiceSpawnPoint.position
                : battleGameManager != null && battleGameManager.CurrentPlayer != null
                    ? battleGameManager.CurrentPlayer.transform.position
                    : transform.position;
        // SpawnPoint 또는 Player가 서 있는 높이를 이번 연출의 바닥으로 저장한다.
        // minimumHeightAboveDiceFloor는 모델 Pivot 차이를 Inspector에서 보정하기 위한 값이다.
        diceFloorWorldY = basePosition.y + minimumHeightAboveDiceFloor;
        // Camera 고정 모드에서는 Viewport 기준 위치를 그대로 사용한다. 월드 배치 보정값을 더하면
        // Camera 회전에 따라 화면 위치가 달라지므로 PositionOffset은 이전 월드 배치에서만 적용한다.
        Vector3 spawnPosition = usesCameraFixedPresentation
            ? basePosition
            : basePosition + animatedDicePositionOffset;
        if (dicePresentationFloor != null)
        {
            // 투명 기준면만 Camera/Spawn 위치로 옮기고 Dice Animation은 그 아래 Local 좌표에서 재생한다.
            // 기준면에는 Collider와 Renderer가 없으므로 전투 타일·캐릭터·Raycast와 상호작용하지 않는다.
            dicePresentationFloor.position = spawnPosition;
            dicePresentationFloor.gameObject.SetActive(true);
            sceneDiceObject.transform.localPosition = Vector3.zero;
            sceneDiceObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            sceneDiceObject.transform.position = spawnPosition;
        }
        sceneDiceObject.SetActive(true);
        sceneDiceAnimator.Rebind();
        sceneDiceAnimator.Update(0f);

        string resultStateName = DiceResultAnimationStatePrefix + diceValue;
        int resultStateHash = Animator.StringToHash(resultStateName);
        if (!sceneDiceAnimator.HasState(0, resultStateHash))
        {
            Debug.LogError($"[Dice Animation] Animator State를 찾지 못했습니다: {resultStateName}", this);
            HideSceneDice();
            return;
        }

        sceneDiceAnimator.Play(resultStateHash, 0, 0f);
        sceneDiceAnimator.Update(0f);
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
        if (dicePresentationFloor != null) dicePresentationFloor.gameObject.SetActive(false);
        usesCameraFixedPresentation = false;
    }

    private void SetResultVisible(bool visible)
    {
        if (spawnedResultView != null) spawnedResultView.SetActive(visible);
    }

    private void AcquireInputLock()
    {
        if (holdsInputLock || battleGameManager == null) return;
        battleGameManager.LockBattleInputForOverlay();
        holdsInputLock = true;
    }

    private void ReleaseInputLock()
    {
        if (!holdsInputLock) return;
        holdsInputLock = false;
        battleGameManager?.UnlockBattleInputAfterOverlay();
    }
}
