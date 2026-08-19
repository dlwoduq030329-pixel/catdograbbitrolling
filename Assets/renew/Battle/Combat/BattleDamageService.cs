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
    /// <summary>유효한 대상에게 피해를 적용하고 Enemy 피격 인식 및 결과 데이터를 함께 처리한다.</summary>
    public static bool TryApplyDamage(
        GameObject attacker,
        GameObject target,
        float amount,
        BattleDamageType damageType,
        out BattleDamageResult result)
    {
        result = default;
        if (target == null || amount <= 0f)
        {
            return false;
        }

        BattleHealth health = target.GetComponent<BattleHealth>();
        if (health == null || health.IsDead)
        {
            return false;
        }

        BattleStatusEffects attackerStatus = attacker != null
            ? attacker.GetComponent<BattleStatusEffects>()
            : null;
        BattleStatusEffects targetStatus = target.GetComponent<BattleStatusEffects>();
        float modifiedDamage = attackerStatus != null
            ? attackerStatus.ModifyOutgoingDamage(amount, damageType)
            : amount;
        if (targetStatus != null)
        {
            modifiedDamage = targetStatus.ModifyIncomingDamage(modifiedDamage, damageType);
        }

        float appliedDamage = health.TakeDamage(modifiedDamage);
        if (appliedDamage <= 0f)
        {
            return false;
        }

        EnemyAwareness awareness = target.GetComponent<EnemyAwareness>();
        if (awareness != null && attacker != null)
        {
            awareness.NotifyDamaged(attacker.transform);
        }

        result = new BattleDamageResult(
            attacker,
            target,
            damageType,
            modifiedDamage,
            appliedDamage,
            health.CurrentHealth,
            health.IsDead);
        return true;
    }
}
