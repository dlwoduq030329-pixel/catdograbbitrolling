using System;
using UnityEngine;

[Serializable]
public sealed class BattleShopStageRarityWeights
{
    [Min(1)] public int minimumStage = 1;
    [Min(0)] public int common = 80;
    [Min(0)] public int rare = 20;
    [Min(0)] public int epic;
    [Min(0)] public int legendary;
}

[Serializable]
public sealed class BattleShopStageEquipmentWeights
{
    [Min(1)] public int minimumStage = 1;
    [Min(0)] public int hand = 50;
    [Min(0)] public int body = 20;
    [Min(0)] public int head = 20;
    [Min(0)] public int twoHand = 10;
}

[CreateAssetMenu(fileName = "BattleShopConfig", menuName = "Renew/Battle/Shop Config")]
public sealed class BattleShopConfig : ScriptableObject
{
    private const string ResourcePath = "Battle/Shop/BattleShopConfig";

    [Range(1, 6)] public int cardSlots = 3;
    [Range(1, 6)] public int equipmentSlots = 3;
    [Min(0)] public int initialRerollPrice = 10;
    [Min(0)] public int maximumRerollPrice = 160;
    public BattleShopStageRarityWeights[] rarityByStage =
    {
        new BattleShopStageRarityWeights { minimumStage = 1, common = 80, rare = 20 },
        new BattleShopStageRarityWeights { minimumStage = 2, common = 55, rare = 35, epic = 10 },
        new BattleShopStageRarityWeights { minimumStage = 3, common = 35, rare = 40, epic = 20, legendary = 5 },
        new BattleShopStageRarityWeights { minimumStage = 4, common = 20, rare = 35, epic = 30, legendary = 15 },
    };
    public BattleShopStageEquipmentWeights[] equipmentByStage =
    {
        new BattleShopStageEquipmentWeights { minimumStage = 1, hand = 55, body = 20, head = 20, twoHand = 5 },
        new BattleShopStageEquipmentWeights { minimumStage = 2, hand = 45, body = 20, head = 20, twoHand = 15 },
        new BattleShopStageEquipmentWeights { minimumStage = 3, hand = 40, body = 20, head = 20, twoHand = 20 },
    };

    public weaponSt RollRarity(int stage)
    {
        BattleShopStageRarityWeights selected = null;
        for (int i = 0; i < rarityByStage.Length; i++)
        {
            BattleShopStageRarityWeights candidate = rarityByStage[i];
            if (candidate != null && candidate.minimumStage <= stage &&
                (selected == null || candidate.minimumStage > selected.minimumStage))
                selected = candidate;
        }

        if (selected == null) return weaponSt.Common;
        int total = selected.common + selected.rare + selected.epic + selected.legendary;
        if (total <= 0) return weaponSt.Common;
        int roll = UnityEngine.Random.Range(0, total);
        if ((roll -= selected.common) < 0) return weaponSt.Common;
        if ((roll -= selected.rare) < 0) return weaponSt.Rare;
        if ((roll -= selected.epic) < 0) return weaponSt.Epic;
        return weaponSt.Legendary;
    }

    public WeaponKind RollEquipmentKind(int stage)
    {
        BattleShopStageEquipmentWeights selected = null;
        for (int i = 0; i < equipmentByStage.Length; i++)
        {
            BattleShopStageEquipmentWeights candidate = equipmentByStage[i];
            if (candidate != null && candidate.minimumStage <= stage &&
                (selected == null || candidate.minimumStage > selected.minimumStage)) selected = candidate;
        }
        if (selected == null) return WeaponKind.Hand;
        int total = selected.hand + selected.body + selected.head + selected.twoHand;
        if (total <= 0) return WeaponKind.Hand;
        int roll = UnityEngine.Random.Range(0, total);
        if ((roll -= selected.hand) < 0) return WeaponKind.Hand;
        if ((roll -= selected.body) < 0) return WeaponKind.Body;
        if ((roll -= selected.head) < 0) return WeaponKind.Head;
        return WeaponKind.TwoHand;
    }

    public static BattleShopConfig Load() => Resources.Load<BattleShopConfig>(ResourcePath);
}
