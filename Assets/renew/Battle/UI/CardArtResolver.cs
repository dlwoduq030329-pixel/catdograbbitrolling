using UnityEngine;

/// <summary>
/// 코스트 숫자를 지운 카드 아트("no number" 스프라이트)를 이름으로 찾아주는 공용 유틸리티.
/// 손패(BattleCardHandView), 인벤토리(InventoryCard, InventoryStore), 상점(BattleCardShopSystem)에서
/// 같은 방식으로 카드 원본 스프라이트를 대체 스프라이트로 바꿀 때 사용한다.
/// </summary>
public static class CardArtResolver
{
    private const string NoNumberResourceFolder = "UI/Cards/NoNumber/";

    /// <summary>
    /// 코스트 숫자를 지운 버전이 있으면 그것을, 없으면(equip/tribe 등 이번에 작업하지 않은 아트) 원본을 반환한다.
    /// </summary>
    public static Sprite ResolveDisplaySprite(Sprite originalSprite)
    {
        Sprite noNumber = ResolveNoNumberSprite(originalSprite);
        return noNumber != null ? noNumber : originalSprite;
    }

    /// <summary>코스트 숫자를 지운 스프라이트가 있으면 반환하고, 없으면 null을 반환한다.</summary>
    public static Sprite ResolveNoNumberSprite(Sprite originalSprite)
    {
        if (originalSprite == null) return null;

        string spriteName = originalSprite.name;
        const string multiSuffix = "_0";
        if (spriteName.EndsWith(multiSuffix))
        {
            spriteName = spriteName.Substring(0, spriteName.Length - multiSuffix.Length);
        }

        return Resources.Load<Sprite>(NoNumberResourceFolder + spriteName);
    }
}
