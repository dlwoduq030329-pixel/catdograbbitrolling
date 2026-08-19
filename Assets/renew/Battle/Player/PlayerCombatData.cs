using UnityEngine;

/// <summary>
/// 플레이어 기본 공격의 최종 사거리, MP 비용, 위력을 공급한다.
/// 장비/스탯 시스템 연결 전에는 Inspector 기본값을 사용한다.
/// </summary>
public class PlayerCombatData : MonoBehaviour
{
    [Header("기본 공격 설정")]
    [InspectorName("기본 공격 사거리 (칸)")]
    [SerializeField, Min(1)] private int basicAttackRangeTiles = 1;
    [InspectorName("기본 공격 행동력 비용")]
    [SerializeField, Min(0)] private int basicAttackMPCost = 1;
    [InspectorName("기본 공격 위력 (피해 연결용)")]
    [SerializeField, Min(0f)] private float basicAttackPower = 1f;

    public int BasicAttackRangeTiles => basicAttackRangeTiles;
    public int BasicAttackMPCost => basicAttackMPCost;
    public float BasicAttackPower => basicAttackPower;

    /// <summary>장비 시스템이 계산한 최종 기본 공격 값을 한 번에 적용한다.</summary>
    public void ConfigureBasicAttack(int rangeTiles, int mpCost, float power)
    {
        basicAttackRangeTiles = Mathf.Max(1, rangeTiles);
        basicAttackMPCost = Mathf.Max(0, mpCost);
        basicAttackPower = Mathf.Max(0f, power);
    }
}
