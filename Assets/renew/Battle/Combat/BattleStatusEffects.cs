using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleStatusType
{
    Stun,
    Root,
    Poison,
    Frostbite,
    Burn,
    Shred,
    Nervous,
    Wound,
    Taunt,
    Slow,
    Vulnerable
}

/// <summary>Player와 Enemy가 공유하는 턴제 상태 효과 저장소와 계산 진입점.</summary>
[DisallowMultipleComponent]
public sealed class BattleStatusEffects : MonoBehaviour
{
    [Serializable]
    private sealed class StatusEntry
    {
        public BattleStatusType type;
        public int turns;
        public int stacks = 1;
        public GameObject source;
    }

    [SerializeField] private List<StatusEntry> statuses = new List<StatusEntry>();
    public event Action<BattleStatusEffects> Changed;

    public bool Has(BattleStatusType type) => Find(type) != null;
    public int GetTurns(BattleStatusType type) => Find(type)?.turns ?? 0;
    public int GetStacks(BattleStatusType type) => Find(type)?.stacks ?? 0;

    public string BuildCompactLabel()
    {
        List<string> labels = new List<string>();
        foreach (StatusEntry entry in statuses)
        {
            if (entry.type == BattleStatusType.Stun || entry.type == BattleStatusType.Root)
            {
                continue;
            }
            string name = GetDisplayName(entry.type);
            string stack = entry.type == BattleStatusType.Poison && entry.stacks > 1
                ? $"x{entry.stacks}"
                : string.Empty;
            labels.Add($"{name}{stack} {entry.turns}");
        }
        return string.Join("  ", labels);
    }

    public void Apply(BattleStatusType type, int turns, GameObject source = null)
    {
        int safeTurns = Mathf.Max(1, turns);
        StatusEntry entry = Find(type);
        if (entry == null)
        {
            entry = new StatusEntry { type = type, turns = safeTurns, source = source };
            statuses.Add(entry);
        }
        else
        {
            entry.turns += safeTurns;
            entry.source = source != null ? source : entry.source;
            if (type == BattleStatusType.Poison)
            {
                entry.stacks = Mathf.Min(2, entry.stacks + 1);
            }
        }
        Debug.Log(
            $"[Status] {name}에게 {GetDisplayName(type)} 적용 | " +
            $"지속 {entry.turns}턴 | 중첩 {entry.stacks} | " +
            $"시전자 {(source != null ? source.name : "없음")}",
            this);
        Changed?.Invoke(this);
    }

    public int ClearAllNegativeStatuses()
    {
        int removed = statuses.Count;
        if (removed == 0) return 0;
        statuses.Clear();
        Debug.Log($"[Status] {name}의 상태이상 {removed}개 제거", this);
        Changed?.Invoke(this);
        return removed;
    }

    public int ModifyMoveCost(int cost) => Has(BattleStatusType.Frostbite) ? cost + 1 : cost;
    public int ModifyAttackCost(int cost) => Has(BattleStatusType.Frostbite) ? cost + 1 : cost;
    public float ModifyHealing(float amount) => Has(BattleStatusType.Wound) ? amount * 0.6f : amount;

    public float ModifyOutgoingDamage(float amount, BattleDamageType type)
    {
        return type == BattleDamageType.Physical && Has(BattleStatusType.Burn)
            ? amount * 0.7f
            : amount;
    }

    public float ModifyIncomingDamage(float amount, BattleDamageType type)
    {
        float multiplier = 1f;
        if (type == BattleDamageType.Physical && Has(BattleStatusType.Shred)) multiplier *= 1.3f;
        if (type == BattleDamageType.Magic && Has(BattleStatusType.Nervous)) multiplier *= 1.3f;
        if (Has(BattleStatusType.Wound)) multiplier *= 1.3f;
        if (Has(BattleStatusType.Vulnerable)) multiplier *= 1.25f;
        return amount * multiplier;
    }

    public void ProcessPlayerTurnStart()
    {
        BattleHealth health = GetComponent<BattleHealth>();
        if (health != null && !health.IsDead)
        {
            int poisonStacks = GetStacks(BattleStatusType.Poison);
            if (poisonStacks > 0)
            {
                float damage = health.TakeDamage(health.CurrentHealth * 0.1f * poisonStacks);
                Debug.Log($"[Status] {name} 독 피해 {damage:0.##} | {poisonStacks}중첩 | HP {health.CurrentHealth:0.##}", this);
            }
            if (!health.IsDead && Has(BattleStatusType.Burn))
            {
                float damage = health.TakeDamage(health.CurrentHealth * 0.1f);
                Debug.Log($"[Status] {name} 화상 피해 {damage:0.##} | HP {health.CurrentHealth:0.##}", this);
            }
        }

        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            statuses[i].turns--;
            if (statuses[i].turns <= 0)
            {
                Debug.Log($"[Status] {name}의 {GetDisplayName(statuses[i].type)} 만료", this);
                statuses.RemoveAt(i);
            }
        }
        Changed?.Invoke(this);
    }

    public static void ProcessAllPlayerTurnStart()
    {
        BattleStatusEffects[] all = FindObjectsByType<BattleStatusEffects>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (BattleStatusEffects status in all) status.ProcessPlayerTurnStart();
    }

    public static BattleStatusEffects GetOrAdd(GameObject unit)
    {
        return unit != null ? BattleComponentResolver.GetOrAdd<BattleStatusEffects>(unit, null) : null;
    }

    private StatusEntry Find(BattleStatusType type) => statuses.Find(entry => entry.type == type);

    private static string GetDisplayName(BattleStatusType type)
    {
        switch (type)
        {
            case BattleStatusType.Poison: return "Poison";
            case BattleStatusType.Frostbite: return "Frostbite";
            case BattleStatusType.Burn: return "Burn";
            case BattleStatusType.Shred: return "Shred";
            case BattleStatusType.Nervous: return "Nervous";
            case BattleStatusType.Wound: return "Wound";
            case BattleStatusType.Taunt: return "Taunt";
            case BattleStatusType.Slow: return "Slow";
            case BattleStatusType.Stun: return "Stun";
            case BattleStatusType.Root: return "Root";
            case BattleStatusType.Vulnerable: return "Vulnerable";
            default: return type.ToString();
        }
    }
}

public static class BattleStatusEffectCodes
{
    public static bool TryParse(string code, out BattleStatusType type)
    {
        switch (code)
        {
            case "기절": type = BattleStatusType.Stun; return true;
            case "속박": type = BattleStatusType.Root; return true;
            case "독": type = BattleStatusType.Poison; return true;
            case "동상": type = BattleStatusType.Frostbite; return true;
            case "화상": type = BattleStatusType.Burn; return true;
            case "파쇄": type = BattleStatusType.Shred; return true;
            case "불안": type = BattleStatusType.Nervous; return true;
            case "상처": type = BattleStatusType.Wound; return true;
            case "도발": type = BattleStatusType.Taunt; return true;
            case "둔화": type = BattleStatusType.Slow; return true;
            case "취약": type = BattleStatusType.Vulnerable; return true;
            default: type = default; return false;
        }
    }
}
