using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleCombatLog에 저장된 최근 전투 기록을 Inspector에서 연결한 스크롤 Text에 표시한다.
/// 로그 데이터 생성은 담당하지 않으며, 변경 이벤트를 받아 화면 갱신과 자동 스크롤만 수행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCombatLogView : MonoBehaviour
{
    [Header("Combat Log UI")]
    [SerializeField] private TMP_Text logText;
    [SerializeField] private ScrollRect logScrollRect;
    [SerializeField, Range(1, 500)] private int maximumVisibleEntries = 100;
    [SerializeField] private bool mirrorToConsole;

    private Coroutine scrollToNewestRoutine;

    private void OnEnable()
    {
        BattleCombatLog.Changed -= HandleLogChanged;
        BattleCombatLog.Changed += HandleLogChanged;
        RefreshText();
    }

    private void OnDisable()
    {
        BattleCombatLog.Changed -= HandleLogChanged;
        if (scrollToNewestRoutine != null)
        {
            StopCoroutine(scrollToNewestRoutine);
            scrollToNewestRoutine = null;
        }
    }

    private void HandleLogChanged()
    {
        if (mirrorToConsole && BattleCombatLog.Entries.Count > 0)
        {
            Debug.Log($"[Battle Log] {BattleCombatLog.Entries[BattleCombatLog.Entries.Count - 1]}", this);
        }

        RefreshText();
    }

    private void RefreshText()
    {
        if (logText == null)
        {
            return;
        }

        IReadOnlyList<string> entries = BattleCombatLog.Entries;
        int firstVisibleEntry = Mathf.Max(0, entries.Count - maximumVisibleEntries);
        StringBuilder visibleLog = new StringBuilder();
        for (int i = firstVisibleEntry; i < entries.Count; i++)
        {
            if (visibleLog.Length > 0) visibleLog.AppendLine();
            visibleLog.Append(entries[i]);
        }

        logText.text = visibleLog.ToString();
        if (logScrollRect != null && isActiveAndEnabled)
        {
            if (scrollToNewestRoutine != null) StopCoroutine(scrollToNewestRoutine);
            scrollToNewestRoutine = StartCoroutine(ScrollToNewestAfterLayout());
        }
    }

    /// <summary>레이아웃 계산이 끝난 다음 최신 로그가 있는 아래쪽으로 스크롤한다.</summary>
    private IEnumerator ScrollToNewestAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (logScrollRect != null)
        {
            logScrollRect.verticalNormalizedPosition = 0f;
            logScrollRect.StopMovement();
        }

        scrollToNewestRoutine = null;
    }
}
