using UnityEngine;

/// <summary>
/// 플레이어 기본 공격의 최종 사거리, MP 비용, 위력을 공급한다.
/// Inspector 기본값과 PlayerWeapon의 현재 장비 보너스를 분리해서 보관하고 최종 전투 수치를 계산한다.
/// </summary>
public class PlayerCombatData : MonoBehaviour
{
    [Header("기본 공격 설정")]
    [InspectorName("기본 공격 사거리 (칸)")]
    [SerializeField, Min(1)] private int basicAttackRangeTiles = 1;
    [InspectorName("기본 공격 행동력 비용")]
    [SerializeField, Min(0)] private int basicAttackMPCost = 1;
    [InspectorName("물리 기본 공격 위력")]
    [SerializeField, Min(0f)] private float basicAttackPower = 1f;

    [InspectorName("마법 기본 공격 위력")]
    [SerializeField, Min(0f)] private float basicMagicAttackPower = 1f;

    [Header("장비 능력치의 기본 공격(물딜/마딜) 위력 환산 계수")]
    [Tooltip("장비 STR 1당 기본 공격(물딜) 위력에 더할 값입니다.")]
    [SerializeField] private float strengthPowerCoefficient = 1f;

    [Tooltip("장비 INT 1당 기본 공격(마딜) 위력에 더할 값입니다.")]
    [SerializeField] private float intelligencePowerCoefficient = 1f;



    private PlayerWeapon playerWeapon;
    private PlayerEquipmentStats equipmentStats;

    /// <summary>Inspector 기본 사거리와 현재 장비 사거리 보너스를 합친 최종 공격 사거리다.</summary>
    public int BasicAttackRangeTiles => Mathf.Max(
        1,
        basicAttackRangeTiles + Mathf.RoundToInt(equipmentStats.AttackRangeBonus));
    public int BasicAttackMPCost => basicAttackMPCost;

    /// <summary>물리 기본 위력에 장비 STR 보너스만 환산해 더한 최종 물리 평타 위력이다.</summary>
    public float PhysicalBasicAttackPower => Mathf.Max(
        0f,
        basicAttackPower + equipmentStats.StrengthBonus * strengthPowerCoefficient);

    /// <summary>마법 기본 위력에 장비 INT 보너스만 환산해 더한 최종 마법 평타 위력이다.</summary>
    public float MagicBasicAttackPower => Mathf.Max(
        0f,
        basicMagicAttackPower + equipmentStats.IntelligenceBonus * intelligencePowerCoefficient);

    /// <summary>피해 타입에 맞는 최종 평타 위력을 반환한다.</summary>
    public float GetBasicAttackPower(BattleDamageType damageType)
    {
        return damageType == BattleDamageType.Magic
            ? MagicBasicAttackPower
            : PhysicalBasicAttackPower;
    }

    /// <summary>
    /// 같은 Player의 PlayerWeapon을 연결하고 현재 장비 보너스를 즉시 받은 뒤 이후 변경도 구독한다.
    /// </summary>
    public void Bind(PlayerWeapon sourcePlayerWeapon)
    {
        if (playerWeapon != null)
            playerWeapon.EquipmentStatsChanged -= ApplyEquipmentStats;

        playerWeapon = sourcePlayerWeapon;
        equipmentStats = playerWeapon != null
            ? playerWeapon.TotalEquipmentStats
            : default;

        if (playerWeapon != null)
            playerWeapon.EquipmentStatsChanged += ApplyEquipmentStats;
    }

    /// <summary>기존 호출부 호환용 설정 함수다. 전달받은 위력을 물리·마법 기본값에 함께 적용한다.</summary>
    public void ConfigureBasicAttack(int rangeTiles, int mpCost, float power)
    {
        ConfigureBasicAttack(rangeTiles, mpCost, power, power);
    }

    /// <summary>사거리, 비용, 물리 위력, 마법 위력을 각각 설정한다.</summary>
    public void ConfigureBasicAttack(
        int rangeTiles,
        int mpCost,
        float physicalPower,
        float magicPower)
    {
        basicAttackRangeTiles = Mathf.Max(1, rangeTiles);
        basicAttackMPCost = Mathf.Max(0, mpCost);
        basicAttackPower = Mathf.Max(0f, physicalPower);
        basicMagicAttackPower = Mathf.Max(0f, magicPower);
    }

    private void ApplyEquipmentStats(PlayerEquipmentStats newEquipmentStats)
    {
        equipmentStats = newEquipmentStats;
    }

    private void OnDestroy()
    {
        if (playerWeapon != null)
            playerWeapon.EquipmentStatsChanged -= ApplyEquipmentStats;
    }
}
