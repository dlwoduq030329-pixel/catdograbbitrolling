using UnityEngine;

/// <summary>
/// 상점과 상자에서 공통으로 사용하는 장비 추첨 규칙이다.
/// 밸런스 값은 <see cref="BattleShopBalanceData"/>에서 읽고, 난수 추첨 책임은 이 클래스가 가진다.
/// </summary>
public static class BattleEquipmentRewardRoller
{
    /// <summary>
    /// 현재 Stage 이하인 설정 중 minimumStage가 가장 높은 구간을 선택하고,
    /// Common/Rare/Epic/Legendary 상대 가중치로 장비 등급 하나를 뽑는다.
    /// 설정이 없거나 전체 가중치가 0이면 안전 기본값 Common을 반환한다.
    /// </summary>
    public static weaponSt RollRarity(BattleShopBalanceData balanceData, int currentStage)
    {
        // selectedWeights는 실제 추첨 결과가 아니라 현재 Stage에 적용할 확률표 한 개다.
        BattleShopStageRarityWeights selectedWeights = null;
        BattleShopStageRarityWeights[] stageWeights = balanceData != null ? balanceData.rarityByStage : null;
        if (stageWeights != null)
        {
            for (int i = 0; i < stageWeights.Length; i++)
            {
                // candidate는 rarityByStage 배열에 들어 있는 Stage별 확률표 후보 하나다.
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

        // 전체 가중치 범위에서 난수 하나를 만든 뒤 각 등급 가중치를 순서대로 빼서 구간을 찾는다.
        int remainingRoll = Random.Range(0, totalWeight);
        if ((remainingRoll -= selectedWeights.commonWeight) < 0) return weaponSt.Common;
        if ((remainingRoll -= selectedWeights.rareWeight) < 0) return weaponSt.Rare;
        if ((remainingRoll -= selectedWeights.epicWeight) < 0) return weaponSt.Epic;
        return weaponSt.Legendary;
    }

    /// <summary>
    /// 현재 Stage에 적용할 장비 부위 확률표를 선택하고 Hand/Body/Head/TwoHand 중 하나를 뽑는다.
    /// 카드 선택이나 실제 EquipData 선택은 하지 않으며, 반환된 부위로 Shop System이 장비 후보를 좁힌다.
    /// 설정이 없거나 전체 가중치가 0이면 안전 기본값 Hand를 반환한다.
    /// </summary>
    public static WeaponKind RollEquipmentKind(BattleShopBalanceData balanceData, int currentStage)
    {
        // selectedWeights는 현재 Stage에 적용할 장비 부위 확률표 한 개다.
        BattleShopStageEquipmentWeights selectedWeights = null;
        BattleShopStageEquipmentWeights[] stageWeights = balanceData != null ? balanceData.equipmentByStage : null;
        if (stageWeights != null)
        {
            for (int i = 0; i < stageWeights.Length; i++)
            {
                // candidate 중 현재 Stage 이하이며 시작 Stage가 가장 높은 설정을 선택한다.
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

        // 카드나 장비 목록 순서가 아니라 네 부위의 가중치 구간을 순서대로 검사한다.
        int remainingRoll = Random.Range(0, totalWeight);
        if ((remainingRoll -= selectedWeights.handWeight) < 0) return WeaponKind.Hand;
        if ((remainingRoll -= selectedWeights.bodyWeight) < 0) return WeaponKind.Body;
        if ((remainingRoll -= selectedWeights.headWeight) < 0) return WeaponKind.Head;
        return WeaponKind.TwoHand;
    }
}
