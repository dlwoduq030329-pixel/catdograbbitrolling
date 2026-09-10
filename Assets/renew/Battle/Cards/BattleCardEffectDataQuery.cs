using UnityEngine;

/// <summary>카드 데이터에서 필요한 효과 설정을 찾습니다.</summary>
internal static class BattleCardEffectDataQuery
{
    public static bool TryFindFirstEffect(
        BattleCardData card,
        BattleCardEffectType effectType,
        out BattleCardEffectData foundEffect)
    {
        foundEffect = card != null && card.effects != null
            ? card.effects.Find(effect => effect != null && effect.effectType == effectType)
            : null;
        return foundEffect != null;
    }

    public static bool ContainsEffect(BattleCardData card, BattleCardEffectType effectType)
    {
        return card != null && card.effects != null &&
               card.effects.Exists(effect => effect != null && effect.effectType == effectType);
    }

    public static int FindLongestMovementDistance(BattleCardData card, BattleCardEffectType effectType)
    {
        int longestDistance = 0;
        if (card == null || card.effects == null)
            return longestDistance;

        foreach (BattleCardEffectData effect in card.effects)
        {
            if (effect != null && effect.effectType == effectType)
                longestDistance = Mathf.Max(longestDistance, effect.distanceTiles);
        }
        return longestDistance;
    }

    public static int FindStrongestPushForce(BattleCardData card)
    {
        int strongestForce = 1;
        if (card == null || card.effects == null)
            return strongestForce;

        foreach (BattleCardEffectData effect in card.effects)
        {
            if (effect != null && effect.effectType == BattleCardEffectType.Push)
                strongestForce = Mathf.Max(strongestForce, effect.pushForce);
        }
        return strongestForce;
    }
}
