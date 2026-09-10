/// <summary>카드가 대상을 직접 선택하는지 자동으로 찾는지 확인합니다.</summary>
internal static class BattleCardTargetSelectionRules
{
    internal static bool UsesAutomaticLowestHealthEnemyTarget(BattleCardData card)
    {
        return card != null &&
               card.targetSelectionMode == BattleCardTargetSelectionMode.LowestHealthEnemyInRange;
    }
}
