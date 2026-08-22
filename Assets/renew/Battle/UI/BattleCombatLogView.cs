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

    private Coroutine activeAutoScrollCoroutine;

    /// <summary>
    /// 전투 로그 화면이 활성화되면 변경 이벤트를 중복 없이 구독하고,
    /// 비활성화되어 있던 동안 추가된 기록까지 포함해 현재 텍스트를 즉시 다시 만든다.
    /// </summary>
    private void OnEnable()
    {
        BattleCombatLog.LogEntriesChanged -= OnCombatLogEntriesChanged;
        BattleCombatLog.LogEntriesChanged += OnCombatLogEntriesChanged;
        RebuildCombatLogText();
    }

    /// <summary>
    /// 화면이 비활성화되면 전투 로그 이벤트 구독을 해제하고,
    /// 다음 프레임 레이아웃 갱신을 기다리던 자동 스크롤 코루틴도 중지한다.
    /// </summary>
    private void OnDisable()
    {
        BattleCombatLog.LogEntriesChanged -= OnCombatLogEntriesChanged;
        if (activeAutoScrollCoroutine != null)
        {
            StopCoroutine(activeAutoScrollCoroutine);
            activeAutoScrollCoroutine = null;
        }
    }

    /// <summary>
    /// BattleCombatLog에 기록이 추가되거나 전체 초기화됐을 때 호출된다.
    /// 디버그 옵션이 켜져 있으면 마지막 기록을 Console에 복사하고, 화면용 로그 텍스트를 다시 만든다.
    /// </summary>
    private void OnCombatLogEntriesChanged()
    {
        if (logNewestEntryToConsole && BattleCombatLog.LogEntries.Count > 0)
        {
            Debug.Log(
                $"[Battle Log] {BattleCombatLog.LogEntries[BattleCombatLog.LogEntries.Count - 1]}",
                this);
        }

        RebuildCombatLogText();
    }

    /// <summary>
    /// 저장된 전체 전투 기록 중 Inspector에서 지정한 최대 개수만 최근 순서대로 선택하고,
    /// 각 기록 사이에 줄바꿈을 넣어 하나의 TMP 텍스트로 다시 구성한다.
    /// 텍스트 높이는 같은 프레임에 즉시 확정되지 않으므로, 갱신 후 자동 스크롤 코루틴을 시작한다.
    /// </summary>
    private void RebuildCombatLogText()
    {
        if (combatLogText == null)
        {
            return;
        }

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
        if (combatLogScrollRect != null && isActiveAndEnabled)
        {
            if (activeAutoScrollCoroutine != null)
            {
                StopCoroutine(activeAutoScrollCoroutine);
            }

            activeAutoScrollCoroutine = StartCoroutine(
                ScrollToLatestEntryAfterLayout());
        }
    }

    /// <summary>
    /// TMP 텍스트와 VerticalLayoutGroup이 새 높이를 계산할 수 있도록 한 프레임 기다린 뒤,
    /// ScrollRect를 최신 로그가 위치한 맨 아래로 이동하고 남아 있는 스크롤 관성을 정지한다.
    /// </summary>
    private IEnumerator ScrollToLatestEntryAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (combatLogScrollRect != null)
        {
            combatLogScrollRect.verticalNormalizedPosition = 0f;
            combatLogScrollRect.StopMovement();
        }

        activeAutoScrollCoroutine = null;
    }
}
