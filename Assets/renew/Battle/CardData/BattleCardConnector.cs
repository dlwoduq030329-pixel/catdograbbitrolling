using UnityEngine;

/// <summary>
/// 원본 카드 데이터베이스 인덱스를 전투 카드 데이터와 행동 요청으로 변환한다.
/// </summary>
public static class BattleCardConnector
{
    /// <summary>원본 카드와 전투 확장 데이터를 결합해 행동 요청을 생성한다.</summary>
    public static bool TryCreateActionRequest(
        BattleCardData battleCard,
        CardDatabase originalDatabase,
        out BattleActionRequest request)
    {
        CardData originalCard = battleCard != null
            ? FindOriginalCard(battleCard.legacyCardIndex, originalDatabase)
            : null;
        if (battleCard == null || originalCard == null)
        {
            request = null;
            return false;
        }

        float power = Mathf.Max(originalCard.damage, originalCard.heal, battleCard.shield);
        request = new BattleActionRequest(
            originalCard.name,
            BattleActionType.Card,
            Mathf.Max(1, battleCard.rangeTiles),
            Mathf.Max(0, originalCard.cost),
            power);
        return true;
    }

    /// <summary>원본 카드 인덱스에 연결된 전투 카드 데이터를 찾는다.</summary>
    public static BattleCardData ResolveBattleCard(
        int legacyCardIndex,
        BattleCardDatabase battleDatabase)
    {
        return battleDatabase != null
            ? battleDatabase.FindByLegacyCardIndex(legacyCardIndex)
            : null;
    }

    /// <summary>
    /// 전투 카드 데이터가 있으면 해당 값을 사용하고, 없으면 원본 카드 값으로 기본 행동 요청을 생성한다.
    /// </summary>
    public static bool TryCreateActionRequest(
        int legacyCardIndex,
        BattleCardDatabase battleDatabase,
        out BattleActionRequest request,
        out BattleCardData battleCard)
    {
        battleCard = ResolveBattleCard(legacyCardIndex, battleDatabase);
        if (battleCard != null)
        {
            return TryCreateActionRequest(battleCard, null, out request);
        }

        CardData legacyCard = FindOriginalCard(legacyCardIndex);
        if (legacyCard == null)
        {
            request = null;
            return false;
        }

        request = new BattleActionRequest(
            legacyCard.name,
            BattleActionType.Card,
            1,
            legacyCard.cost,
            Mathf.Max(legacyCard.damage, legacyCard.heal));

        Debug.LogWarning(
            $"전투 카드 DB 연결이 없어 기존 카드 기본값으로 변환했습니다: {legacyCard.name} ({legacyCardIndex})");
        return true;
    }

    /// <summary>원본 카드 목록에서 고유 인덱스 값이 일치하는 데이터를 찾는다.</summary>
    public static CardData FindOriginalCard(int cardIndex, CardDatabase originalDatabase = null)
    {
        CardDatabase database = originalDatabase != null
            ? originalDatabase
            : DataPool.Instance != null ? DataPool.Instance.cardDatabase : null;
        if (database == null)
        {
            return null;
        }

        foreach (CardData card in database.cards)
        {
            if (card != null && card.index == cardIndex)
            {
                return card;
            }
        }

        return null;
    }
}
