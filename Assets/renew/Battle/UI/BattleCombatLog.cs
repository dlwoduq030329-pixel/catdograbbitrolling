using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Stores short player-facing combat records independently from the final UI layout.</summary>
public static class BattleCombatLog
{
    private static readonly List<string> entries = new List<string>();
    public static IReadOnlyList<string> Entries => entries;
    public static event Action Changed;

    public static void Clear()
    {
        entries.Clear();
        Changed?.Invoke();
    }

    public static void Add(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        entries.Add(message);
        Changed?.Invoke();
    }
}

/// <summary>Displays recent combat records in an assigned TMP_Text without creating UI objects.</summary>
[DisallowMultipleComponent]
public sealed class BattleCombatLogView : MonoBehaviour
{
    [SerializeField] private TMP_Text logText;
    [SerializeField, Range(1, 20)] private int maximumVisibleEntries = 6;
    [SerializeField] private bool mirrorToConsole;

    public void Configure(TMP_Text targetText, int maximumEntries, bool writeToConsole)
    {
        logText = targetText;
        maximumVisibleEntries = Mathf.Clamp(maximumEntries, 1, 20);
        mirrorToConsole = writeToConsole;
        Refresh();
    }

    private void OnEnable()
    {
        BattleCombatLog.Changed -= HandleChanged;
        BattleCombatLog.Changed += HandleChanged;
        Refresh();
    }

    private void OnDisable() => BattleCombatLog.Changed -= HandleChanged;

    private void HandleChanged()
    {
        if (mirrorToConsole && BattleCombatLog.Entries.Count > 0)
            Debug.Log($"[Battle Log] {BattleCombatLog.Entries[BattleCombatLog.Entries.Count - 1]}", this);
        Refresh();
    }

    private void Refresh()
    {
        if (logText == null) return;
        IReadOnlyList<string> entries = BattleCombatLog.Entries;
        int start = Mathf.Max(0, entries.Count - maximumVisibleEntries);
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = start; i < entries.Count; i++)
        {
            if (builder.Length > 0) builder.AppendLine();
            builder.Append(entries[i]);
        }
        logText.text = builder.ToString();
    }
}
