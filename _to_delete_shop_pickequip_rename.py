# -*- coding: utf-8 -*-
def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

p = "Assets/renew/Battle/Shop/BattleCardShopSystem.cs"
content = load(p)
old = '''    private int PickEquipmentCandidate(List<int> candidates)
    {
        if (candidates == null || candidates.Count == 0) return -1;
        WeaponKind preferred = shopConfig != null
            ? shopConfig.RollEquipmentKind(DataConfig.stage)
            : equipmentDatabase.equip[candidates[0]].weaponKind;
        List<int> pool = candidates.FindAll(index => equipmentDatabase.equip[index].weaponKind == preferred);
        if (pool.Count == 0) pool = candidates;

        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++) totalWeight += IsEquipped(pool[i]) ? 1 : 4;
        int roll = Random.Range(0, Mathf.Max(1, totalWeight));
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= IsEquipped(pool[i]) ? 1 : 4;
            if (roll < 0) return pool[i];
        }
        return pool[0];
    }'''
new = '''    private int PickEquipmentCandidate(List<int> candidates)
    {
        if (candidates == null || candidates.Count == 0) return -1;
        // 1차 추첨: 부위(WeaponKind)를 먼저 정한다.
        WeaponKind preferredKind = shopConfig != null
            ? shopConfig.RollEquipmentKind(DataConfig.stage)
            : equipmentDatabase.equip[candidates[0]].weaponKind;
        // 후보 중 그 부위만 걸러낸 풀. 그 부위 재고가 하나도 없으면(kindFilteredPool.Count == 0)
        // 부위 제한을 포기하고 원래 후보 전체(candidates)를 그대로 쓴다.
        List<int> kindFilteredPool = candidates.FindAll(index => equipmentDatabase.equip[index].weaponKind == preferredKind);
        if (kindFilteredPool.Count == 0) kindFilteredPool = candidates;

        // 2차 추첨: 같은 풀 안에서 "이미 장착 중=가중치1, 미장착=가중치4"로 다시 뽑는다.
        int totalEquipWeight = 0;
        for (int i = 0; i < kindFilteredPool.Count; i++) totalEquipWeight += IsEquipped(kindFilteredPool[i]) ? 1 : 4;
        int weightedRoll = Random.Range(0, Mathf.Max(1, totalEquipWeight));
        for (int i = 0; i < kindFilteredPool.Count; i++)
        {
            weightedRoll -= IsEquipped(kindFilteredPool[i]) ? 1 : 4;
            if (weightedRoll < 0) return kindFilteredPool[i];
        }
        return kindFilteredPool[0];
    }'''
assert content.count(old) == 1
content = content.replace(old, new, 1)
save(p, content)
print("OK")
