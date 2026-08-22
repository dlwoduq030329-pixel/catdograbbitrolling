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
        assert count == 1, (path, i, count, old[:150])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

p = "Assets/renew/Battle/Shop/BattleCardShopSystem.cs"
apply(p, [
    (
'''    private static EquipData GetComparableEquipment(WeaponKind kind)
    {
        switch (kind)
        {
            case WeaponKind.Body: return DataConfig.bodyDa;
            case WeaponKind.Head: return DataConfig.headDa;
            case WeaponKind.TwoHand: return DataConfig.leftDa;
            default: return DataConfig.leftDa ?? DataConfig.rightDa;
        }
    }

    private static int GetReplacementRefund(WeaponKind kind)
    {
        if (kind == WeaponKind.Body) return DataConfig.GetSaleValue(DataConfig.bodyDa != null ? DataConfig.bodyDa.cost : 0);
        if (kind == WeaponKind.Head) return DataConfig.GetSaleValue(DataConfig.headDa != null ? DataConfig.headDa.cost : 0);
        if (kind == WeaponKind.TwoHand)
        {
            int left = DataConfig.GetSaleValue(DataConfig.leftDa != null ? DataConfig.leftDa.cost : 0);
            int right = ReferenceEquals(DataConfig.leftDa, DataConfig.rightDa)
                ? 0 : DataConfig.GetSaleValue(DataConfig.rightDa != null ? DataConfig.rightDa.cost : 0);
            return left + right;
        }
        if (DataConfig.leftDa == null || DataConfig.rightDa == null) return 0;
        return DataConfig.GetSaleValue(DataConfig.leftDa.cost);
    }

    private static string FormatEquipment(EquipData equipment)
    {
        return equipment == null
            ? "EMPTY"
            : $"{equipment.cardname} [{equipment.weapon}] STR+{equipment.stroffset} WIS+{equipment.wisoffset} DEX+{equipment.dexoffset} VIT+{equipment.vitoffset}";
    }

    private static int GetSlotRefund(EquipData equipment)
    {
        return DataConfig.GetSaleValue(equipment != null ? equipment.cost : 0);
    }

    private void Reroll()''',
'''    // (2026-08-22 정리, 사용자 확인: GetComparableEquipment/GetReplacementRefund/FormatEquipment/
    // GetSlotRefund 4개 삭제됨 - 호출부가 저장소 전체에서 0개였다. 방금 지운 EnsureEquipmentConfirmationView
    // (제목이 "EQUIPMENT COMPARISON"였음)가 완성됐다면 "현재 장착 장비 vs 구매하려는 장비" 비교 텍스트를
    // 만드는 데 썼을 헬퍼들로 추정되지만, 그 UI 자체가 한 번도 연결된 적 없어 이 헬퍼들도 같이 고아가 됐다.)

    private void Reroll()'''
    ),
])
