using UnityEngine;

/// <summary>
/// Battle 카드 실행 결과에 애니메이션과 레거시 VFX 연출만 연결한다.
/// 실제 피해와 상태 변화는 BattleCardEffectPipeline이 담당한다.
/// </summary>
public static class BattleLegacyCardPresentationBridge
{
    private static readonly string[] CardCodes =
    {
        "SWING", "BODYSLAM", "HIT_DOWN", "FACE_GUARD", "STALE_JERKY",
        "WEIRD_MUSHROOM", "HEALING_POTION", "POISON_POTION", "SCARECROW",
        "VITAL_STRIKE", "WILD_SLASH", "FINISHING_BLOW", "WHIRLWIND",
        "POWER_STRIKE", "DIVINE_STRIKE", "HEALING_BREATH", "CONSECRATION",
        "HEALING_TOUCH", "WEAPON_BLESSING", "HOLY_ARROW", "METEOR", "LIGHTNING",
        "ICE_MAGIC", "GROUND_ERUPTION", "FIRE_BALL", "CURSE_MAGIC",
        "RAIN_OF_ARROWS", "EXPLOSION"
    };

    public static string ResolveCardCode(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < CardCodes.Length ? CardCodes[cardIndex] : string.Empty;
    }

    public static void Play(
        GameObject player,
        GameObject selectedTarget,
        MapInfo selectedTile,
        string cardCode,
        BattleCardCategory category)
    {
        if (player == null || string.IsNullOrWhiteSpace(cardCode)) return;

        PlayerSkills legacy = player.GetComponentInChildren<PlayerSkills>(true);
        GameObject visualPrefab = null;
        if (legacy != null)
        {
            legacy.PlayPresentationOnly(cardCode);
            visualPrefab = legacy.GetPresentationPrefab(cardCode);
        }
        else if (!BattleCharacterAnimationBridge.PlayState(player, cardCode) &&
                 category == BattleCardCategory.Attack)
        {
            BattleCharacterAnimationBridge.PlayAttack(player);
        }

        if (visualPrefab == null)
        {
            BattleCardVfxRegistry registry = BattleCardVfxRegistry.Load();
            visualPrefab = registry != null ? registry.Find(cardCode) : null;
        }

        // 지속 영역 VFX는 BattleHealingArea가 영역 수명과 함께 관리한다.
        if (cardCode != "CONSECRATION")
            SpawnVisualOnly(visualPrefab, ResolvePosition(
                player, selectedTarget, selectedTile, cardCode), cardCode);
    }

    private static Vector3 ResolvePosition(
        GameObject player, GameObject target, MapInfo tile, string cardCode)
    {
        switch (cardCode)
        {
            case "EXPLOSION":
            case "CONSECRATION":
                return player.transform.position;
            case "GROUND_ERUPTION":
            case "METEOR":
                return tile != null ? tile.transform.position :
                    target != null ? target.transform.position : player.transform.position;
            default:
                return target != null ? target.transform.position :
                    tile != null ? tile.transform.position : player.transform.position;
        }
    }

    private static void SpawnVisualOnly(
        GameObject prefab, Vector3 position, string cardCode)
    {
        if (prefab == null) return;

        GameObject instance = Object.Instantiate(prefab, position, prefab.transform.rotation);
        if (cardCode == "ICE_MAGIC")
        {
            // 레거시 IceMagic.Init은 피해까지 실행하므로 호출하지 않고 시각 자식만 활성화한다.
            foreach (Transform child in instance.transform)
                child.gameObject.SetActive(true);
        }
        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            particles.Play(true);
        Object.Destroy(instance, 2.5f);
    }
}
