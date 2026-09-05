using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 중 Player와 Enemy에게 일정 턴 동안 적용되는 상태이상 종류다.
/// STR·DEX 같은 영구 능력치는 CharactorStatus가 관리하며 이 enum에 넣지 않는다.
/// </summary>
public enum BattleStatusType
{
    /// <summary>기절: Player와 Enemy 모두 다음 행동 턴 전체를 건너뛴다. 2026-09-05: Enemy 전용
    /// BattleEnemyControlState 중복 저장소를 폐지하고 이 저장소 하나로 통합했다(EnemyTurnActor가
    /// Has(Stun)을 직접 읽는다).</summary>
    Stun,
    /// <summary>속박: 공격은 가능하지만 이동할 수 없다. 2026-09-05: Enemy 전용 BattleEnemyControlState
    /// 중복 저장소를 폐지하고 이 저장소 하나로 통합했다(EnemyTurnActor가 Has(Root)를 직접 읽는다).</summary>
    Root,
    /// <summary>독: Player 턴 시작마다 현재 HP의 10% × 중첩 피해를 받고 최대 2중첩된다.</summary>
    Poison,
    /// <summary>동상: 한 칸 이동 비용과 카드·기본 공격 MP 비용이 각각 1 증가한다.</summary>
    Frostbite,
    /// <summary>화상: Player 턴 시작마다 현재 HP의 10% 피해를 받고, 화상 상태 공격자의 물리 피해가 30% 감소한다.</summary>
    Burn,
    /// <summary>파쇄: 대상이 받는 물리 피해가 30% 증가한다.</summary>
    Shred,
    /// <summary>불안: 대상이 받는 마법 피해가 30% 증가한다.</summary>
    Nervous,
    /// <summary>상처: 대상이 받는 모든 피해가 30% 증가하고 회복량이 40% 감소한다.</summary>
    Wound,
    /// <summary>도발: enum과 표시만 존재하며 현재 행동 규칙에는 아직 연결되지 않았다.</summary>
    Taunt,
    /// <summary>둔화: enum과 표시만 존재하며 현재 이동 규칙에는 아직 연결되지 않았다.</summary>
    Slow,
    /// <summary>취약: 대상이 받는 모든 피해가 25% 증가한다.</summary>
    Vulnerable
}

/// <summary>
/// 상태 아이콘 UI가 표시할 상태 종류, 남은 턴과 중첩 수를 한 항목으로 전달하는 읽기 전용 데이터다.
/// UI가 BattleStatusEffects의 내부 저장 구조나 문자열 변환 규칙에 의존하지 않도록 분리한다.
/// </summary>
public readonly struct BattleStatusDisplayEntry
{
    public BattleStatusType Type { get; }
    public int RemainingTurns { get; }
    public int StackCount { get; }

    public BattleStatusDisplayEntry(
        BattleStatusType type,
        int remainingTurns,
        int stackCount)
    {
        Type = type;
        RemainingTurns = remainingTurns;
        StackCount = stackCount;
    }
}

/// <summary>Player와 Enemy가 공유하는 턴제 상태 효과 저장소와 계산 진입점.</summary>
[DisallowMultipleComponent]
public sealed class BattleStatusEffects : MonoBehaviour
{
    [Serializable]
    private sealed class StatusEntry
    {
        /// <summary>이 Entry가 나타내는 상태이상 종류.</summary>
        public BattleStatusType type;
        /// <summary>앞으로 처리할 Player 턴 시작 횟수. 0이 되면 목록에서 제거된다.</summary>
        public int turns;
        /// <summary>동일 상태의 중첩 수. 현재는 독만 최대 2중첩을 사용한다.</summary>
        public int stacks = 1;
        /// <summary>상태를 적용한 Unit. 지속 피해의 공격자·전투 로그 연결을 위해 보관한다.</summary>
        public GameObject source;
    }

    [SerializeField] private List<StatusEntry> statuses = new List<StatusEntry>();
    public event Action<BattleStatusEffects> Changed;

    /// <summary>지정한 상태가 현재 하나라도 적용되어 있는지 반환한다.</summary>
    public bool Has(BattleStatusType type) => Find(type) != null;
    /// <summary>지정한 상태의 남은 턴을 반환하며 적용되지 않았으면 0을 반환한다.</summary>
    public int GetTurns(BattleStatusType type) => Find(type)?.turns ?? 0;
    /// <summary>지정한 상태의 중첩을 반환하며 적용되지 않았으면 0을 반환한다.</summary>
    public int GetStacks(BattleStatusType type) => Find(type)?.stacks ?? 0;

    /// <summary>
    /// 현재 활성 상태를 UI가 사용할 구조화된 표시 데이터로 복사한다.
    /// 전달받은 목록을 먼저 비우고 내부 StatusEntry를 읽기 전용 값으로 변환하므로,
    /// 호출자는 같은 List 인스턴스를 재사용해 상태 변경 때마다 새 목록이 생성되는 것을 피할 수 있다.
    /// </summary>
    public void CopyActiveStatusesTo(List<BattleStatusDisplayEntry> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        foreach (StatusEntry entry in statuses)
        {
            destination.Add(new BattleStatusDisplayEntry(
                entry.type,
                entry.turns,
                entry.stacks));
        }
    }

    /// <summary>
    /// 상태를 새로 등록하거나 같은 상태의 남은 턴을 누적한다.
    /// 독만 재적용 시 최대 2중첩되고, source가 전달되면 마지막 적용자로 교체한다.
    /// </summary>
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

    /// <summary>
    /// 정화 카드가 현재 목록의 모든 상태를 즉시 제거하고 제거 개수를 반환한다.
    /// 자연 만료 함수가 아니며, 현재는 긍정/부정 분류 없이 목록 전체를 지운다.
    /// </summary>
    public int ClearAllNegativeStatuses()
    {
        int removed = statuses.Count;
        if (removed == 0) return 0;
        statuses.Clear();
        Debug.Log($"[Status] {name}의 상태이상 {removed}개 제거", this);
        Changed?.Invoke(this);
        return removed;
    }

    /// <summary>동상이 있으면 한 칸 이동 MP를 1 증가시킨다. 추후 Movement 비용 계산 내부로 이동한다.</summary>
    public int ModifyMoveCost(int cost) => Has(BattleStatusType.Frostbite) ? cost + 1 : cost;
    /// <summary>동상이 있으면 카드와 기본 공격 MP를 1 증가시킨다. 추후 각 비용 계산 내부로 이동한다.</summary>
    public int ModifyAttackCost(int cost) => Has(BattleStatusType.Frostbite) ? cost + 1 : cost;
    /// <summary>상처가 있으면 회복량을 원래 값의 60%로 줄인다. 추후 회복 계산 내부로 이동한다.</summary>
    public float ModifyHealing(float amount) => Has(BattleStatusType.Wound) ? amount * 0.6f : amount;

    /// <summary>
    /// Player 턴 시작에 이 Unit의 독·화상 지속 피해를 적용한 뒤 모든 상태의 남은 턴을 1 감소시킨다.
    /// Enemy에게 붙은 컴포넌트도 같은 시점에 호출되므로 Enemy의 지속 피해 역시 Player 턴 시작에 처리된다.
    /// </summary>
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

    /// <summary>
    /// BattleGameManager가 Player 턴 시작마다 호출해 활성 상태의 모든 Unit 상태를 진행시킨다.
    /// 현재는 턴마다 Scene 전체를 검색하므로 UnitRegistry가 공식 원본이 되면 등록 목록 순회로 교체한다.
    /// </summary>
    public static void ProcessAllPlayerTurnStart()
    {
        BattleStatusEffects[] all = FindObjectsByType<BattleStatusEffects>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (BattleStatusEffects status in all) status.ProcessPlayerTurnStart();
    }

    /// <summary>
    /// Unit에 상태 컴포넌트가 없으면 런타임에 추가한다. 임시 호환 API이며 Player·Enemy Prefab 직접 연결 후 제거한다.
    /// </summary>
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

/// <summary>
/// 카드 데이터의 한글 effectCode를 상태 enum으로 바꾸는 레거시 변환기다.
/// BattleCardEffectData가 enum을 직접 저장하도록 이전한 뒤 삭제한다.
/// </summary>
public static class BattleStatusEffectCodes
{
    /// <summary>정확히 일치하는 한글 상태 이름을 변환하며 알 수 없는 문자열이면 false를 반환한다.</summary>
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
