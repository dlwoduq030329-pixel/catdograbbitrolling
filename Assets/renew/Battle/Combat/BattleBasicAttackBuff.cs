using UnityEngine;

/// <summary>현재 플레이어 턴 동안 유지되는 기본 공격 피해 보너스.</summary>
[DisallowMultipleComponent]
public sealed class BattleBasicAttackBuff : MonoBehaviour
{
    public float BonusDamage { get; private set; }

    public void Add(float amount)
    {
        BonusDamage += Mathf.Max(0f, amount);
        Debug.Log($"[Buff] {name} 기본 공격 피해 +{BonusDamage:0.##}", this);
    }

    public void Clear()
    {
        BonusDamage = 0f;
    }
}
