using System;
using UnityEngine;

/// <summary>
/// 전투 Unit의 최대 체력과 현재 체력을 보관하는 공용 런타임 컴포넌트다.
/// 피해량 계산, 공격 판정과 사망 연출은 담당하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleHealth : MonoBehaviour
{
    [Header("체력 상태")]
    [InspectorName("최대 체력")]
    [SerializeField, Min(1f)] private float maxHealth = 1f;
    [InspectorName("현재 체력")]
    [SerializeField, Min(0f)] private float currentHealth = 1f;
    [InspectorName("현재 보호막")]
    [SerializeField, Min(0f)] private float currentShield;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float CurrentShield => currentShield;
    public bool IsDead => currentHealth <= 0f;

    public event Action<BattleHealth> HealthChanged;
    public event Action<BattleHealth> ShieldChanged;
    public event Action<BattleHealth> Died;

    /// <summary>DB 최대 체력을 적용하고 현재 체력을 최대치로 회복해 새 전투 상태를 시작한다.</summary>
    public void Initialize(float value)
    {
        maxHealth = Mathf.Max(1f, value);
        currentHealth = maxHealth;
        currentShield = 0f;
        HealthChanged?.Invoke(this);
        ShieldChanged?.Invoke(this);
    }

    /// <summary>0보다 큰 피해를 현재 체력에 적용하고 실제 감소량을 반환한다.</summary>
    public float TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return 0f;
        }

        float remainingDamage = amount;
        float absorbedDamage = Mathf.Min(currentShield, remainingDamage);
        if (absorbedDamage > 0f)
        {
            currentShield -= absorbedDamage;
            remainingDamage -= absorbedDamage;
            ShieldChanged?.Invoke(this);
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - remainingDamage);
        float healthDamage = previousHealth - currentHealth;
        float appliedDamage = absorbedDamage + healthDamage;
        if (healthDamage > 0f)
        {
            HealthChanged?.Invoke(this);
        }

        if (IsDead)
        {
            Died?.Invoke(this);
        }

        return appliedDamage;
    }

    /// <summary>생존한 대상의 체력을 최대 체력까지 회복하고 실제 회복량을 반환합니다.</summary>
    public float Heal(float amount)
    {
        BattleStatusEffects status = GetComponent<BattleStatusEffects>();
        if (status != null)
        {
            amount = status.ModifyHealing(amount);
        }
        if (IsDead || amount <= 0f || currentHealth >= maxHealth)
        {
            return 0f;
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float appliedHealing = currentHealth - previousHealth;
        HealthChanged?.Invoke(this);
        return appliedHealing;
    }

    /// <summary>전투 피해를 먼저 흡수할 보호막을 추가하고 실제 증가량을 반환합니다.</summary>
    public float AddShield(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return 0f;
        }

        currentShield += amount;
        ShieldChanged?.Invoke(this);
        return amount;
    }

    /// <summary>다음 Player 턴 시작 등 보호막 만료 시 남은 수치를 모두 제거합니다.</summary>
    public void ClearShield()
    {
        if (currentShield <= 0f)
        {
            return;
        }

        currentShield = 0f;
        ShieldChanged?.Invoke(this);
    }
}
