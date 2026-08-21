using System;
using UnityEngine;

/// <summary>전투 피해의 속성 분류. 방어력과 저항 계산을 추가할 때 같은 진입점을 확장한다.</summary>
public enum BattleDamageType
{
    [InspectorName("물리 피해")]
    Physical,
    [InspectorName("마법 피해")]
    Magic,
    [InspectorName("고정 피해")]
    True
}

/// <summary>한 번의 피해 적용 결과를 호출자와 UI에 전달하는 변경 불가능한 데이터다.</summary>
public readonly struct BattleDamageResult
{
    public GameObject Attacker { get; }
    public GameObject Target { get; }
    public BattleDamageType DamageType { get; }
    public float RequestedDamage { get; }
    public float AppliedDamage { get; }
    public float RemainingHealth { get; }
    public bool Killed { get; }

    public BattleDamageResult(
        GameObject attacker,
        GameObject target,
        BattleDamageType damageType,
        float requestedDamage,
        float appliedDamage,
        float remainingHealth,
        bool killed)
    {
        Attacker = attacker;
        Target = target;
        DamageType = damageType;
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
        RemainingHealth = remainingHealth;
        Killed = killed;
    }
}

/// <summary>
/// 공격자가 요청한 피해를 대상의 BattleHealth에 전달하는 공용 진입점이다.
/// 현재 MVP에서는 방어력 보정 없이 요청 피해를 그대로 적용한다.
/// </summary>
public static class BattleDamageService
{
    /// <summary>피해가 실제 HP에 반영된 직후 UI·VFX가 결과를 구독하는 공용 알림이다.</summary>
    public static event Action<BattleDamageResult> DamageApplied;

    /// <summary>
    /// 공격자가 요청한 피해를 공격자·대상의 상태이상으로 보정한 뒤 대상의 보호막과 HP에 적용한다.
    /// 피해가 실제로 적용되면 Enemy에게 공격자를 어그로 대상으로 알리고,
    /// 전투 Log와 VFX가 같은 결과를 사용하도록 <see cref="BattleDamageResult"/>와 이벤트를 전달한다.
    /// false를 반환하면 피해가 적용되지 않았으며 <paramref name="result"/>는 기본값이다.
    /// </summary>
    public static bool TryApplyDamage(
        GameObject attacker,
        GameObject target,
        float amount,
        BattleDamageType damageType,
        out BattleDamageResult result)
    {
        // 실패한 호출자가 이전 피해 결과를 실수로 재사용하지 않도록 먼저 결과를 비운다.
        result = default;

        // 대상이 없거나 양수가 아닌 피해는 전투 상태를 변경할 수 없는 잘못된 요청이다.
        if (target == null || amount <= 0f)
        {
            return false;
        }

        // 실제 피해를 받을 BattleHealth가 없거나 이미 사망한 대상에는 피해를 적용하지 않는다.
        BattleHealth targetHealth = target.GetComponent<BattleHealth>();
        if (targetHealth == null || targetHealth.IsDead)
        {
            return false;
        }

        // 공격자 상태이상은 주는 피해를 변경한다. 환경 피해처럼 공격자가 없으면 원래 피해를 사용한다.
        BattleStatusEffects attackerStatusEffects = attacker != null
            ? attacker.GetComponent<BattleStatusEffects>()
            : null;

        // 대상 상태이상은 받는 피해를 변경한다. 두 보정은 공격자 보정 후 대상 보정 순서로 적용한다.
        BattleStatusEffects targetStatusEffects = target.GetComponent<BattleStatusEffects>();
        float damageAfterAttackerStatusEffects = CalculateDamageAfterAttackerStatusEffects(
            amount,
            damageType,
            attackerStatusEffects);
        float finalDamage = CalculateDamageAfterTargetStatusEffects(
            damageAfterAttackerStatusEffects,
            damageType,
            targetStatusEffects);

        // BattleHealth가 보호막을 먼저 소모하고 남은 피해를 HP에 적용한다.
        // 반환값은 보호막 흡수량과 실제 HP 감소량을 합한 실제 적용 피해다.
        float appliedDamage = targetHealth.TakeDamage(finalDamage);
        if (appliedDamage <= 0f)
        {
            return false;
        }

        // 피해 대상이 Enemy라면 공격자를 기억시켜 이후 AI 판단에서 추격·공격 대상으로 사용하게 한다.
        EnemyAwareness targetEnemyAwareness = target.GetComponent<EnemyAwareness>();
        if (targetEnemyAwareness != null && attacker != null)
        {
            targetEnemyAwareness.NotifyDamaged(attacker.transform);
        }

        // 호출자, Log와 VFX가 서로 다른 값을 다시 계산하지 않도록 적용 직후 결과를 한곳에서 만든다.
        result = new BattleDamageResult(
            attacker,
            target,
            damageType,
            finalDamage,
            appliedDamage,
            targetHealth.CurrentHealth,
            targetHealth.IsDead);

        // 전투 Log는 실제로 적용된 피해와 사망 여부만 기록한다.
        string attackerName = attacker != null ? attacker.name : "Environment";
        BattleCombatLog.Add(
            $"{attackerName} → {target.name}  -{appliedDamage:0.#} HP" +
            (targetHealth.IsDead ? "  [DEFEATED]" : string.Empty));

        // 피해 적용이 모두 끝난 뒤 이벤트를 보내 VFX와 UI가 최종 상태를 안전하게 읽게 한다.
        DamageApplied?.Invoke(result);
        return true;
    }

    /// <summary>
    /// 공격자에게 걸린 상태이상이 공격자가 주는 피해를 어떻게 바꾸는지 계산한다.
    /// 현재 규칙은 화상 상태의 공격자가 주는 물리 피해를 30% 감소시키는 것이다.
    /// </summary>
    private static float CalculateDamageAfterAttackerStatusEffects(
        float originalDamage,
        BattleDamageType damageType,
        BattleStatusEffects attackerStatusEffects)
    {
        bool burnReducesPhysicalDamage =
            damageType == BattleDamageType.Physical &&
            attackerStatusEffects != null &&
            attackerStatusEffects.Has(BattleStatusType.Burn);

        return burnReducesPhysicalDamage
            ? originalDamage * 0.7f
            : originalDamage;
    }

    /// <summary>
    /// 대상에게 걸린 상태이상이 대상이 받는 피해를 어떻게 바꾸는지 계산한다.
    /// 파쇄는 물리 피해 30%, 불안은 마법 피해 30%, 상처는 모든 피해 30%,
    /// 취약은 모든 피해 25%를 증가시키며 동시에 적용되면 배율을 곱한다.
    /// </summary>
    private static float CalculateDamageAfterTargetStatusEffects(
        float incomingDamage,
        BattleDamageType damageType,
        BattleStatusEffects targetStatusEffects)
    {
        if (targetStatusEffects == null)
        {
            return incomingDamage;
        }

        float damageMultiplier = 1f;

        // 파쇄 상태의 대상은 물리 피해를 30% 더 받는다.
        if (damageType == BattleDamageType.Physical &&
            targetStatusEffects.Has(BattleStatusType.Shred))
        {
            damageMultiplier *= 1.3f;
        }

        // 불안 상태의 대상은 마법 피해를 30% 더 받는다.
        if (damageType == BattleDamageType.Magic &&
            targetStatusEffects.Has(BattleStatusType.Nervous))
        {
            damageMultiplier *= 1.3f;
        }

        // 상처 상태의 대상은 피해 속성과 관계없이 30% 더 받는다.
        if (targetStatusEffects.Has(BattleStatusType.Wound))
        {
            damageMultiplier *= 1.3f;
        }

        // 취약 상태의 대상은 피해 속성과 관계없이 25% 더 받는다.
        if (targetStatusEffects.Has(BattleStatusType.Vulnerable))
        {
            damageMultiplier *= 1.25f;
        }

        return incomingDamage * damageMultiplier;
    }
}
