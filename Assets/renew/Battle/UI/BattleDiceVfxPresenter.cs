using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 확정된 주사위 값에 맞춰 선택형 굴림 VFX와 결과 UI를 순서대로 보여준다.
/// VFX 프리팹이 없어도 결과 표시와 전투 진행은 유지되며, 나중에 프리팹 참조만 넣으면 자동 재생된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDiceVfxPresenter : MonoBehaviour
{
    [Header("굴림 VFX (추후 연결)")]
    [Tooltip("주사위를 굴릴 때 생성할 VFX입니다. 비어 있으면 VFX만 생략합니다.")]
    [SerializeField] private GameObject diceRollVfxPrefab;
    [Tooltip("월드 VFX 생성 위치입니다. 비어 있으면 현재 Player 위치를 사용합니다.")]
    [SerializeField] private Transform worldVfxSpawnPoint;
    [Tooltip("월드 VFX를 Player 또는 지정 위치에서 얼마나 옮길지 정합니다.")]
    [SerializeField] private Vector3 worldVfxPositionOffset = new Vector3(0f, 1.5f, 0f);

    [Header("결과 표시")]
    [Tooltip("1~6 Sprite가 들어 있는 기존 Dice 결과 UI 프리팹입니다.")]
    [SerializeField] private GameObject diceResultPrefab;
    [SerializeField, Min(0f)] private float rollVfxDurationSeconds = 1f;
    [SerializeField, Min(0f)] private float resultDisplaySeconds = 0.8f;

    private BattleGameManager battleGameManager;
    private BattleDiceSystem diceSystem;
    private GameObject presentationCanvasObject;
    private GameObject spawnedRollVfx;
    private GameObject spawnedResultView;
    private diceResultUI diceResultView;
    private Coroutine presentationRoutine;
    private bool holdsInputLock;
    private int presentingDiceValue;

    private void Start() => BindToBattleGameManager();

    private void OnDisable()
    {
        if (diceSystem != null)
            diceSystem.DiceRollResolved -= HandleDiceRollResult;

        if (presentationRoutine != null)
        {
            StopCoroutine(presentationRoutine);
            presentationRoutine = null;
        }

        DestroySpawnedRollVfx();
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

    /// <summary>굴림 VFX를 먼저 재생하고, VFX 종료 후 확정 숫자 UI를 표시한다.</summary>
    private IEnumerator PlayDicePresentation(int diceValue)
    {
        presentingDiceValue = diceValue;
        AcquireInputLock();
        SpawnOptionalRollVfx();

        if (spawnedRollVfx != null)
            yield return new WaitForSecondsRealtime(rollVfxDurationSeconds);

        DestroySpawnedRollVfx();
        ShowDiceResult(diceValue);
        yield return new WaitForSecondsRealtime(resultDisplaySeconds);
        FinishPresentation(diceValue);
    }

    /// <summary>UI VFX는 Overlay Canvas에, 일반 VFX는 지정 위치 또는 Player 위치에 생성한다.</summary>
    private void SpawnOptionalRollVfx()
    {
        DestroySpawnedRollVfx();
        if (diceRollVfxPrefab == null) return;

        if (diceRollVfxPrefab.GetComponent<RectTransform>() != null)
        {
            EnsurePresentationCanvasCreated();
            spawnedRollVfx = Instantiate(diceRollVfxPrefab, presentationCanvasObject.transform, false);
            return;
        }

        Vector3 basePosition = worldVfxSpawnPoint != null
            ? worldVfxSpawnPoint.position
            : battleGameManager != null && battleGameManager.CurrentPlayer != null
                ? battleGameManager.CurrentPlayer.transform.position
                : transform.position;
        spawnedRollVfx = Instantiate(
            diceRollVfxPrefab,
            basePosition + worldVfxPositionOffset,
            diceRollVfxPrefab.transform.rotation);
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

    private void DestroySpawnedRollVfx()
    {
        if (spawnedRollVfx == null) return;
        Destroy(spawnedRollVfx);
        spawnedRollVfx = null;
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
