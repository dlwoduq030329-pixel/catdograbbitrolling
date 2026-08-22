using UnityEngine;

/// <summary>
/// 플레이어 기본 공격의 최종 사거리, MP 비용, 위력을 공급한다.
/// 장비/스탯 시스템 연결 전에는 Inspector 기본값을 사용한다.
///
/// 실제 연결 상태(2026-08-22 확인): ConfigureBasicAttack()을 부르는 곳이 저장소 전체에 아직 0곳이다.
/// 즉 장비를 구매/장착해도 이 값들은 항상 Inspector 기본값(사거리 1칸, MP 1, 위력 1)으로 고정되어 있고,
/// 장비 시스템(BattleCardShopSystem 등)이 실제로 이 메서드를 호출해서 값을 갱신하는 연결은 아직 없다.
/// 클래스 요약의 "장비/스탯 시스템 연결 전에는"이라는 문구가 가리키는 그 "연결 전" 상태가 곧 현재 상태다.
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

    /// <summary>장비 시스템이 계산한 최종 기본 공격 값을 한 번에 적용하기 위한 메서드(현재 호출부 없음 — 장비 스탯 반영 기능이 아직 미구현 상태임을 보여주는 지점).</summary>
    public void ConfigureBasicAttack(int rangeTiles, int mpCost, float power)
    {
        basicAttackRangeTiles = Mathf.Max(1, rangeTiles);
        basicAttackMPCost = Mathf.Max(0, mpCost);
        basicAttackPower = Mathf.Max(0f, power);
    }
}
