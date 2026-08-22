# -*- coding: utf-8 -*-
def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

def apply(path, replacements):
    content = load(path)
    for i, (old, new) in enumerate(replacements, start=1):
        count = content.count(old)
        assert count == 1, (path, i, count, old[:120])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

p = "Assets/renew/Battle/Shop/BattleShopConfig.cs"
apply(p, [
    (
'''using System;
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
}''',
'''using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 특정 스테이지부터 적용되는 장비 등급(Common/Rare/Epic/Legendary) 상대 가중치 한 구간이다.
/// 4개 값은 확률(%)이 아니라 "가중치"라 합이 100일 필요는 없다 — <see cref="BattleShopConfig.RollRarity"/>가
/// 넷을 더한 합계 구간에서 난수 하나를 뽑아 순서대로(Common -> Rare -> Epic -> Legendary) 깎아나가는
/// 가중 추첨(weighted lottery) 방식으로 등급을 정한다.
/// </summary>
[Serializable]
public sealed class BattleShopStageRarityWeights
{
    /// <summary>이 구간이 적용되는 최소 스테이지. RollRarity는 "현재 스테이지 이하인 구간 중 가장 큰 minimumStage"를 고른다.</summary>
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
/// <see cref="BattleShopStageRarityWeights"/>와 같은 가중 추첨 방식을 <see cref="BattleShopConfig.RollEquipmentKind"/>가 사용한다.
/// </summary>
[Serializable]
public sealed class BattleShopStageEquipmentWeights
{
    /// <summary>이 구간이 적용되는 최소 스테이지. RollEquipmentKind는 "현재 스테이지 이하인 구간 중 가장 큰 minimumStage"를 고른다.</summary>
    [Min(1)] public int minimumStage = 1;
    [FormerlySerializedAs("hand")]
    [Min(0)] public int handWeight = 50;
    [FormerlySerializedAs("body")]
    [Min(0)] public int bodyWeight = 20;
    [FormerlySerializedAs("head")]
    [Min(0)] public int headWeight = 20;
    [FormerlySerializedAs("twoHand")]
    [Min(0)] public int twoHandWeight = 10;
}'''
    ),
    (
'''[CreateAssetMenu(fileName = "BattleShopConfig", menuName = "Renew/Battle/Shop Config")]
public sealed class BattleShopConfig : ScriptableObject
{
    private const string ResourcePath = "Battle/Shop/BattleShopConfig";

    [Range(1, 6)] public int cardSlots = 3;
    [Range(1, 6)] public int equipmentSlots = 3;
    [Min(0)] public int initialRerollPrice = 10;
    [Min(0)] public int maximumRerollPrice = 160;''',
'''/// <summary>
/// 전투 상점의 슬롯 구성, 리롤 가격, 스테이지별 장비 등급/부위 가중치를 담는 설정 자산이다.
/// 카드 자체의 등장 확률은 여기서 다루지 않는다(카드 후보 선정은 <c>BattleCardShopSystem</c> 쪽 로직).
/// <see cref="RollRarity"/>/<see cref="RollEquipmentKind"/>가 이 설정을 읽어 상점 장비와
/// (같은 설정을 재사용하는) 상자 장비 보상의 등급·부위를 결정한다.
/// </summary>
[CreateAssetMenu(fileName = "BattleShopConfig", menuName = "Renew/Battle/Shop Config")]
public sealed class BattleShopConfig : ScriptableObject
{
    private const string ResourcePath = "Battle/Shop/BattleShopConfig";

    /// <summary>한 번에 진열되는 카드 슬롯 수.</summary>
    [Range(1, 6)] public int cardSlots = 3;
    /// <summary>한 번에 진열되는 장비 슬롯 수.</summary>
    [Range(1, 6)] public int equipmentSlots = 3;
    /// <summary>첫 리롤에 드는 골드 비용. 이후 리롤마다 오르는 가격 계산 로직은 이 파일이 아니라 상점 호출부에 있다.</summary>
    [Min(0)] public int initialRerollPrice = 10;
    /// <summary>리롤 가격이 오르더라도 여기서 상한을 둔다.</summary>
    [Min(0)] public int maximumRerollPrice = 160;'''
    ),
    (
'''    public weaponSt RollRarity(int stage)
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
    }''',
'''    /// <summary>
    /// 현재 스테이지에 맞는 <see cref="BattleShopStageRarityWeights"/> 구간을 고른 뒤(스테이지 이하 중 최댓값),
    /// 그 구간의 4개 가중치 합계 범위에서 난수를 하나 뽑아 Common -> Rare -> Epic -> Legendary 순서로
    /// 가중치를 깎아나가며 등급을 정한다(가중 추첨). 구간이 없거나 가중치 합이 0이면 항상 Common.
    /// </summary>
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
        int total = selected.commonWeight + selected.rareWeight + selected.epicWeight + selected.legendaryWeight;
        if (total <= 0) return weaponSt.Common;
        int roll = UnityEngine.Random.Range(0, total);
        if ((roll -= selected.commonWeight) < 0) return weaponSt.Common;
        if ((roll -= selected.rareWeight) < 0) return weaponSt.Rare;
        if ((roll -= selected.epicWeight) < 0) return weaponSt.Epic;
        return weaponSt.Legendary;
    }

    /// <summary>
    /// 현재 스테이지에 맞는 <see cref="BattleShopStageEquipmentWeights"/> 구간을 고른 뒤(스테이지 이하 중 최댓값),
    /// <see cref="RollRarity"/>와 같은 방식의 가중 추첨으로 장비 부위(손/몸통/머리/양손)를 정한다.
    /// 구간이 없거나 가중치 합이 0이면 항상 Hand.
    /// </summary>
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
        int total = selected.handWeight + selected.bodyWeight + selected.headWeight + selected.twoHandWeight;
        if (total <= 0) return WeaponKind.Hand;
        int roll = UnityEngine.Random.Range(0, total);
        if ((roll -= selected.handWeight) < 0) return WeaponKind.Hand;
        if ((roll -= selected.bodyWeight) < 0) return WeaponKind.Body;
        if ((roll -= selected.headWeight) < 0) return WeaponKind.Head;
        return WeaponKind.TwoHand;
    }'''
    ),
    (
'''    public static BattleShopConfig Load() => Resources.Load<BattleShopConfig>(ResourcePath);''',
'''    /// <summary>Resources 폴더에서 이 설정 자산 하나를 불러온다(사실상 싱글턴 접근자).</summary>
    public static BattleShopConfig Load() => Resources.Load<BattleShopConfig>(ResourcePath);'''
    ),
])
