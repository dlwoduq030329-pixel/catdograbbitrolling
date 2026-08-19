using UnityEngine;

/// <summary>
/// Player와 Enemy가 공유하는 반복 공격 MP 비용을 계산한다.
/// MP 차감과 공격 가능 여부는 판단하지 않는다.
/// </summary>
public static class BattleAttackCostService
{
    /// <summary>기본 비용에 이번 턴 성공한 공격 횟수의 다음 배율을 적용한다.</summary>
    public static int CalculateRepeatedAttackCost(int baseCost, int successfulAttackCount)
    {
        return Mathf.Max(0, baseCost) * (Mathf.Max(0, successfulAttackCount) + 1);
    }
}
