using UnityEngine;

/// <summary>
/// 상점과 상자에서 공통으로 사용하는 장비 추첨 규칙이다.
/// 밸런스 값은 <see cref="BattleShopBalanceData"/>에서 읽고, 난수 추첨 책임은 이 클래스가 가진다.
/// </summary>
public static class BattleEquipmentRewardRoller
{
    /// <summary>현재 스테이지에 적용되는 가중치 구간을 찾아 장비 등급을 뽑는다.</summary>
    public static weaponSt RollRarity(BattleShopBalanceData balanceData, int currentStage)
    {
        BattleShopStageRarityWeights selectedWeights = null;
        BattleShopStageRarityWeights[] stageWeights = balanceData != null ? balanceData.rarityByStage : null;
        if (stageWeights != null)
        {
            for (int i = 0; i < stageWeights.Length; i++)
            {
                BattleShopStageRarityWeights candidate = stageWeights[i];
                if (candidate != null && candidate.minimumStage <= currentStage &&
                    (selectedWeights == null || candidate.minimumStage > selectedWeights.minimumStage))
                    selectedWeights = candidate;
            }
        }

        if (selectedWeights == null) return weaponSt.Common;
        int totalWeight = selectedWeights.commonWeight + selectedWeights.rareWeight +
            selectedWeights.epicWeight + selectedWeights.legendaryWeight;
        if (totalWeight <= 0) return weaponSt.Common;

        int remainingRoll = Random.Range(0, totalWeight);
        if ((remainingRoll -= selectedWeights.commonWeight) < 0) return weaponSt.Common;
        if ((remainingRoll -= selectedWeights.rareWeight) < 0) return weaponSt.Rare;
        if ((remainingRoll -= selectedWeights.epicWeight) < 0) return weaponSt.Epic;
        return weaponSt.Legendary;
    }

    /// <summary>현재 스테이지에 적용되는 가중치 구간을 찾아 장비 부위를 뽑는다.</summary>
    public static WeaponKind RollEquipmentKind(BattleShopBalanceData balanceData, int currentStage)
    {
        BattleShopStageEquipmentWeights selectedWeights = null;
        BattleShopStageEquipmentWeights[] stageWeights = balanceData != null ? balanceData.equipmentByStage : null;
        if (stageWeights != null)
        {
            for (int i = 0; i < stageWeights.Length; i++)
            {
                BattleShopStageEquipmentWeights candidate = stageWeights[i];
                if (candidate != null && candidate.minimumStage <= currentStage &&
                    (selectedWeights == null || candidate.minimumStage > selectedWeights.minimumStage))
                    selectedWeights = candidate;
            }
        }

        if (selectedWeights == null) return WeaponKind.Hand;
        int totalWeight = selectedWeights.handWeight + selectedWeights.bodyWeight +
            selectedWeights.headWeight + selectedWeights.twoHandWeight;
        if (totalWeight <= 0) return WeaponKind.Hand;

        int remainingRoll = Random.Range(0, totalWeight);
        if ((remainingRoll -= selectedWeights.handWeight) < 0) return WeaponKind.Hand;
        if ((remainingRoll -= selectedWeights.bodyWeight) < 0) return WeaponKind.Body;
        if ((remainingRoll -= selectedWeights.headWeight) < 0) return WeaponKind.Head;
        return WeaponKind.TwoHand;
    }
}
