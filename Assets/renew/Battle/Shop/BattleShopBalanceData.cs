using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 특정 스테이지부터 적용되는 장비 등급(Common/Rare/Epic/Legendary) 상대 가중치 한 구간이다.
/// 4개 값은 확률(%)이 아니라 상대 가중치라 합이 100일 필요는 없다.
/// </summary>
[Serializable]
public sealed class BattleShopStageRarityWeights
{
    /// <summary>이 구간이 적용되는 최소 스테이지.</summary>
    [Min(1)] public int minimumStage = 1;
    [FormerlySerializedAs("common")]
    [Min(0)] public int commonWeight = 80;
    [FormerlySerializedAs("rare")]
    [Min(0)] public int rareWeight = 20;
    [FormerlySerializedAs("epic")]
    [Min(0)] public int epicWeight;
    [FormerlySerializedAs("legendary")]
    [Min(0)] public int legendaryWeight;
}

/// <summary>
/// 특정 스테이지부터 적용되는 장비 부위(손/몸통/머리/양손) 상대 가중치 한 구간이다.
/// 실제 추첨은 <c>BattleEquipmentRewardRoller</c>가 이 값을 읽어 수행한다.
/// </summary>
[Serializable]
public sealed class BattleShopStageEquipmentWeights
{
    /// <summary>이 구간이 적용되는 최소 스테이지.</summary>
    [Min(1)] public int minimumStage = 1;
    [FormerlySerializedAs("hand")]
    [Min(0)] public int handWeight = 50;
    [FormerlySerializedAs("body")]
    [Min(0)] public int bodyWeight = 20;
    [FormerlySerializedAs("head")]
    [Min(0)] public int headWeight = 20;
    [FormerlySerializedAs("twoHand")]
    [Min(0)] public int twoHandWeight = 10;
}

/// <summary>
/// 상점과 상자가 공유하는 순수 밸런스 데이터다.
/// 값을 보관하는 책임만 가지며 추첨이나 Resources.Load 같은 실행 로직은 포함하지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "BattleShopBalanceData", menuName = "Renew/Battle/Shop Balance Data")]
public sealed class BattleShopBalanceData : ScriptableObject
{
    /// <summary>한 번에 진열되는 카드 슬롯 수.</summary>
    [Range(1, 6)] public int cardSlots = 3;
    /// <summary>한 번에 진열되는 장비 슬롯 수.</summary>
    [Range(1, 6)] public int equipmentSlots = 3;
    /// <summary>첫 리롤에 드는 골드 비용. 이후 리롤마다 오르는 가격 계산 로직은 이 파일이 아니라 상점 호출부에 있다.</summary>
    [Min(0)] public int initialRerollPrice = 10;
    /// <summary>리롤 가격이 오르더라도 여기서 상한을 둔다.</summary>
    [Min(0)] public int maximumRerollPrice = 160;

    /// <summary>이 인덱스 미만의 카드는 시작 덱 필수 카드로 판단해 판매하지 못하게 한다.</summary>
    [Header("카드 거래")]
    [Min(0)] public int defaultCardTypeCount = 5;
    /// <summary>카드 원가에 곱하는 상점 구매 가격 배율.</summary>
    [Min(0f)] public float cardPurchasePriceMultiplier = 2f;
    /// <summary>카드 원가 중 판매 시 돌려받는 비율.</summary>
    [Range(0f, 1f)] public float cardSaleRefundRate = 0.5f;

    [Header("장비 거래")]
    [Range(0f, 1f)] public float equipmentSaleRefundRate = 0.5f;
    [Min(0)] public int handBasePrice = 60;
    [Min(0)] public int headBasePrice = 70;
    [Min(0)] public int bodyBasePrice = 75;
    [Min(0)] public int twoHandBasePrice = 90;
    [Min(0f)] public float rarePriceMultiplier = 1.6f;
    [Min(0f)] public float epicPriceMultiplier = 2.5f;
    [Min(0f)] public float legendaryPriceMultiplier = 4f;
    [Min(0)] public int rareBonusStatRolls = 4;
    [Min(0)] public int epicBonusStatRolls = 6;
    [Min(0)] public int legendaryBonusStatRolls = 10;

    public BattleShopStageRarityWeights[] rarityByStage =
    {
        new BattleShopStageRarityWeights { minimumStage = 1, commonWeight = 80, rareWeight = 20 },
        new BattleShopStageRarityWeights { minimumStage = 2, commonWeight = 55, rareWeight = 35, epicWeight = 10 },
        new BattleShopStageRarityWeights { minimumStage = 3, commonWeight = 35, rareWeight = 40, epicWeight = 20, legendaryWeight = 5 },
        new BattleShopStageRarityWeights { minimumStage = 4, commonWeight = 20, rareWeight = 35, epicWeight = 30, legendaryWeight = 15 },
    };
    public BattleShopStageEquipmentWeights[] equipmentByStage =
    {
        // 2026-08-22 정리+밸런스 조정(사용자 확인): 이 배열이 필드 rename 이후에도 옛 이름(hand/body/head/twoHand)을
        // 그대로 쓰고 있어 실제로는 컴파일이 안 되는 상태였다 — 새 필드명으로 고치면서 손55~40/몸통·머리 고정20/
        // 양손5~20으로 스테이지마다 달랐던 표를 손40/몸통30/머리20/양손10 고정 비율로 전체 스테이지 동일하게 통일했다.
        new BattleShopStageEquipmentWeights { minimumStage = 1, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
        new BattleShopStageEquipmentWeights { minimumStage = 2, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
        new BattleShopStageEquipmentWeights { minimumStage = 3, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
    };

}
