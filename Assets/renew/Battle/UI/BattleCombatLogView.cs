using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

/// <summary>
/// BattleCombatLog에 저장된 최근 전투 기록을 Inspector에서 연결한 스크롤 Text에 표시한다.
/// 로그 데이터 생성은 담당하지 않으며, 변경 이벤트를 받아 화면 갱신과 자동 스크롤만 수행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCombatLogView : MonoBehaviour
{
    [Header("전투 로그 UI")]
    [Tooltip("여러 전투 로그를 줄바꿈으로 합쳐 표시할 TextMeshPro 텍스트입니다.")]
    [FormerlySerializedAs("logText")]
    [SerializeField] private TMP_Text combatLogText;
    [Tooltip("새 로그가 추가되면 최신 기록이 있는 아래쪽으로 자동 이동할 ScrollRect입니다.")]
    [FormerlySerializedAs("logScrollRect")]
    [SerializeField] private ScrollRect combatLogScrollRect;
    [Tooltip("화면에 한 번에 합쳐 표시할 최근 로그의 최대 개수입니다. 저장소의 원본 로그는 삭제하지 않습니다.")]
    [FormerlySerializedAs("maximumVisibleEntries")]
    [SerializeField, Range(1, 500)] private int maxDisplayedEntryCount = 100;
    [Tooltip("활성화하면 새로 추가된 마지막 전투 로그를 Unity Console에도 출력합니다.")]
    [FormerlySerializedAs("mirrorToConsole")]
    [SerializeField] private bool logNewestEntryToConsole;

    [Header("로그 패널 확대·축소")]
    [Tooltip("높이를 변경해 로그 창을 접고 펼칠 Scroll Log 루트 RectTransform입니다.")]
    [SerializeField] private RectTransform resizableLogPanel;
    [Tooltip("로그 창의 확대·축소를 전환하는 화살표 Button입니다.")]
    [SerializeField] private Button expandCollapseButton;
    [Tooltip("패널 상태에 맞춰 Z축으로 회전할 화살표 이미지의 RectTransform입니다. Button 자체를 연결해도 됩니다.")]
    [SerializeField] private RectTransform expandCollapseArrow;
    [Tooltip("로그 창을 접었을 때 유지할 높이입니다. 최근 로그 몇 줄만 보일 정도로 조절합니다.")]
    [SerializeField, Min(1f)] private float collapsedPanelHeight = 140f;
    [Tooltip("로그 창을 펼쳤을 때 사용할 높이입니다.")]
    [SerializeField, Min(1f)] private float expandedPanelHeight = 500f;
    [Tooltip("현재 높이에서 목표 높이까지 부드럽게 전환하는 시간입니다. 0이면 즉시 변경합니다.")]
    [SerializeField, Min(0f)] private float panelResizeDuration = 0.2f;
    [Tooltip("활성화 시 로그 창을 펼쳐진 상태로 시작합니다. 끄면 전투 화면을 덜 가리는 축소 상태로 시작합니다.")]
    [SerializeField] private bool startExpanded;

    /// <summary>텍스트 높이 계산이 끝난 다음 최신 로그로 이동하기 위해 실행 중인 Coroutine.</summary>
    private Coroutine scrollToLatestAfterLayoutRoutine;
    /// <summary>로그 패널 높이를 현재값에서 목표값까지 변경하고 있는 Coroutine.</summary>
    private Coroutine panelHeightAnimationRoutine;
    /// <summary>true면 전체 로그를 읽는 확대 상태, false면 최근 몇 줄만 보이는 축소 상태다.</summary>
    private bool logPanelIsExpanded;

    /// <summary>
    /// 전투 로그 화면이 활성화될 때 필요한 두 입력 경로를 연결한다.
    /// BattleCombatLog의 변경 이벤트는 로그 문자열 갱신을, Button Click은 패널 확대·축소를 시작한다.
    /// 마지막으로 Inspector의 시작 상태를 패널 높이에 즉시 적용하고, 비활성화 중 쌓인 로그까지 다시 표시한다.
    /// </summary>
    private void OnEnable()
    {
        // 이전 활성화에서 구독이 남았더라도 동일 Handler가 두 번 호출되지 않게 제거 후 다시 연결한다.
        BattleCombatLog.LogEntriesChanged -= HandleCombatLogEntriesChanged;
        BattleCombatLog.LogEntriesChanged += HandleCombatLogEntriesChanged;
        if (expandCollapseButton != null)
        {
            // OnEnable이 반복돼도 한 번의 Click에 Toggle이 중복 호출되지 않도록 먼저 제거한 뒤 등록한다.
            expandCollapseButton.onClick.RemoveListener(ToggleLogPanelExpandedState);
            expandCollapseButton.onClick.AddListener(ToggleLogPanelExpandedState);
        }

        // 무조건 축소하는 값이 아니다. Inspector에서 startExpanded를 켰으면 처음부터 확대 상태로 시작한다.
        logPanelIsExpanded = startExpanded;
        SetLogPanelSizeWithoutAnimation(logPanelIsExpanded);
        RefreshDisplayedCombatLogs();
    }

    /// <summary>
    /// 화면이 비활성화되면 OnEnable에서 연결한 이벤트와 Button Listener를 반대 순서로 해제한다.
    /// 비활성 Object에서 UI Coroutine이 계속 RectTransform이나 ScrollRect를 변경하지 않도록
    /// 최신 로그 이동과 패널 높이 전환 Coroutine도 모두 중지한다.
    /// </summary>
    private void OnDisable()
    {
        BattleCombatLog.LogEntriesChanged -= HandleCombatLogEntriesChanged;
        if (expandCollapseButton != null)
        {
            expandCollapseButton.onClick.RemoveListener(ToggleLogPanelExpandedState);
        }

        if (scrollToLatestAfterLayoutRoutine != null)
        {
            StopCoroutine(scrollToLatestAfterLayoutRoutine);
            scrollToLatestAfterLayoutRoutine = null;
        }

        if (panelHeightAnimationRoutine != null)
        {
            StopCoroutine(panelHeightAnimationRoutine);
            panelHeightAnimationRoutine = null;
        }
    }

    /// <summary>
    /// BattleCombatLog.AddEntry 또는 ClearAllEntries가 변경 이벤트를 발생시키면 호출되는 Handler다.
    /// 이 함수가 로그를 직접 만들지는 않는다. 저장소의 최신 상태를 읽어 TMP 표시를 갱신하고,
    /// 선택적 Console 출력이 켜져 있을 때만 마지막 한 줄을 디버그용으로 복사한다.
    /// </summary>
    private void HandleCombatLogEntriesChanged()
    {
        if (logNewestEntryToConsole && BattleCombatLog.LogEntries.Count > 0)
        {
            Debug.Log(
                $"[Battle Log] {BattleCombatLog.LogEntries[BattleCombatLog.LogEntries.Count - 1]}",
                this);
        }

        RefreshDisplayedCombatLogs();
    }

    /// <summary>
    /// BattleCombatLog 저장소의 전체 기록 중 최근 maxDisplayedEntryCount개만 화면용 문자열로 다시 만든다.
    /// 저장소에서 오래된 로그를 삭제하는 함수가 아니며, 화면에 합칠 범위만 잘라 TMP_Text 하나에 넣는다.
    /// 기록 순서는 오래된→최신을 유지하므로 LowerLeft로 배치된 UI에서는 최신 기록이 바닥에 놓인다.
    /// 텍스트 변경 후 실제 Content 높이는 Layout 계산 다음 Frame에 확정되므로 필요한 경우에만
    /// MoveScrollToLatestAfterLayout Coroutine을 시작한다.
    /// </summary>
    private void RefreshDisplayedCombatLogs()
    {
        if (combatLogText == null)
        {
            return;
        }

        // 축소 상태이거나 이미 최신 로그를 읽고 있을 때만 새 로그를 따라간다.
        // 확대 상태에서 사용자가 과거 로그를 읽는 중이면 현재 스크롤 위치를 강제로 빼앗지 않는다.
        bool shouldScrollToLatestAfterRebuild =
            !logPanelIsExpanded || IsCurrentlyViewingLatestLog();

        IReadOnlyList<string> allLogEntries = BattleCombatLog.LogEntries;
        int firstDisplayedEntryIndex = Mathf.Max(
            0,
            allLogEntries.Count - maxDisplayedEntryCount);
        StringBuilder combinedDisplayedLog = new StringBuilder();
        for (int entryIndex = firstDisplayedEntryIndex;
             entryIndex < allLogEntries.Count;
             entryIndex++)
        {
            if (combinedDisplayedLog.Length > 0)
            {
                combinedDisplayedLog.AppendLine();
            }

            combinedDisplayedLog.Append(allLogEntries[entryIndex]);
        }

        combatLogText.text = combinedDisplayedLog.ToString();
        if (combatLogScrollRect != null && isActiveAndEnabled && shouldScrollToLatestAfterRebuild)
        {
            if (scrollToLatestAfterLayoutRoutine != null)
            {
                StopCoroutine(scrollToLatestAfterLayoutRoutine);
            }

            scrollToLatestAfterLayoutRoutine = StartCoroutine(
                MoveScrollToLatestAfterLayout());
        }
    }

    /// <summary>
    /// TMP_Text → VerticalLayoutGroup → ContentSizeFitter가 새 문자열 높이를 계산하도록 한 Frame 기다린다.
    /// 그 뒤 ScrollRect의 0 위치(맨 아래=최신 로그)로 이동하고 기존 Drag 관성을 정지한다.
    /// 이 대기 없이 같은 Frame에 위치를 0으로 설정하면 이전 Content 높이를 기준으로 계산돼
    /// 마지막 줄이 화면 밖에 남을 수 있다.
    /// </summary>
    private IEnumerator MoveScrollToLatestAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (combatLogScrollRect != null)
        {
            combatLogScrollRect.verticalNormalizedPosition = 0f;
            combatLogScrollRect.StopMovement();
        }

        scrollToLatestAfterLayoutRoutine = null;
    }

    /// <summary>
    /// 화살표 Button을 누를 때 현재 상태의 반대로 로그 패널을 전환한다.
    /// 확대·축소 상태를 먼저 갱신한 뒤 현재 높이에서 새 목표 높이까지 보간하므로,
    /// 전환 도중 다시 눌러도 패널이 갑자기 시작 높이로 튀지 않는다.
    /// </summary>
    public void ToggleLogPanelExpandedState()
    {
        logPanelIsExpanded = !logPanelIsExpanded;
        if (panelHeightAnimationRoutine != null)
        {
            StopCoroutine(panelHeightAnimationRoutine);
        }

        panelHeightAnimationRoutine = StartCoroutine(
            AnimateLogPanelHeight(logPanelIsExpanded));
    }

    /// <summary>
    /// 현재 패널 높이에서 확대 또는 축소 높이까지 unscaled time으로 부드럽게 변경한다.
    /// 전투가 일시정지되어 Time.timeScale이 0이어도 UI 조작은 계속 반응해야 하므로 unscaledDeltaTime을 사용한다.
    /// 확대가 끝나면 새로 확보된 공간에서 가장 최근 로그가 보이도록 한 번 아래로 이동한다.
    /// </summary>
    private IEnumerator AnimateLogPanelHeight(bool expandPanel)
    {
        if (resizableLogPanel == null)
        {
            SetArrowDirectionForPanelState(expandPanel);
            panelHeightAnimationRoutine = null;
            yield break;
        }

        float startingHeight = resizableLogPanel.rect.height;
        float targetHeight = expandPanel ? expandedPanelHeight : collapsedPanelHeight;
        float elapsedSeconds = 0f;

        while (elapsedSeconds < panelResizeDuration)
        {
            elapsedSeconds += Time.unscaledDeltaTime;
            float progress = panelResizeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsedSeconds / panelResizeDuration);
            resizableLogPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Lerp(startingHeight, targetHeight, progress));
            yield return null;
        }

        resizableLogPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        SetArrowDirectionForPanelState(expandPanel);
        panelHeightAnimationRoutine = null;

        if (expandPanel && combatLogScrollRect != null && isActiveAndEnabled)
        {
            if (scrollToLatestAfterLayoutRoutine != null)
            {
                StopCoroutine(scrollToLatestAfterLayoutRoutine);
            }

            scrollToLatestAfterLayoutRoutine = StartCoroutine(MoveScrollToLatestAfterLayout());
        }
    }

    /// <summary>
    /// OnEnable 최초 배치에서는 확대·축소 전환 연출을 재생하지 않고 시작 높이와 화살표를 즉시 맞춘다.
    /// AnimateLogPanelHeight와 계산식은 비슷하지만 목적이 다르다. 이 함수가 없으면 Canvas가 켜질 때마다
    /// Scene에 저장된 높이에서 시작 높이까지 접히는 불필요한 시작 연출이 보이게 된다.
    /// </summary>
    private void SetLogPanelSizeWithoutAnimation(bool expandPanel)
    {
        if (resizableLogPanel != null)
        {
            float targetHeight = expandPanel ? expandedPanelHeight : collapsedPanelHeight;
            resizableLogPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        }

        SetArrowDirectionForPanelState(expandPanel);
    }

    /// <summary>
    /// 축소 상태에서는 원래 방향, 확대 상태에서는 180도 뒤집힌 방향으로 화살표를 표시한다.
    /// 위치와 X/Y 회전은 유지하고 Z축 회전만 변경한다.
    /// </summary>
    private void SetArrowDirectionForPanelState(bool expandPanel)
    {
        if (expandCollapseArrow == null)
        {
            return;
        }

        Vector3 arrowAngles = expandCollapseArrow.localEulerAngles;
        arrowAngles.z = expandPanel ? 180f : 0f;
        expandCollapseArrow.localEulerAngles = arrowAngles;
    }

    /// <summary>
    /// ScrollRect의 세로 위치가 최신 로그가 있는 맨 아래에 가까운지 확인한다.
    /// Unity ScrollRect는 0이 아래, 1이 위이므로 작은 값일수록 최신 기록을 보고 있는 상태다.
    /// </summary>
    private bool IsCurrentlyViewingLatestLog()
    {
        return combatLogScrollRect == null ||
               combatLogScrollRect.verticalNormalizedPosition <= 0.02f;
    }
}
