using System;
using System.Collections.Generic;

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
