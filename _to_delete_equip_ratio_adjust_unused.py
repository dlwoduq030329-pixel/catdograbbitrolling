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

# 1) BattleShopConfig.cs 기본값(향후 새 자산 생성 시 초깃값) 갱신
p1 = "Assets/renew/Battle/Shop/BattleShopConfig.cs"
apply(p1, [
    (
'''    public BattleShopStageEquipmentWeights[] equipmentByStage =
    {
        new BattleShopStageEquipmentWeights { minimumStage = 1, handWeight = 55, bodyWeight = 20, headWeight = 20, twoHandWeight = 5 },
        new BattleShopStageEquipmentWeights { minimumStage = 2, handWeight = 45, bodyWeight = 20, headWeight = 20, twoHandWeight = 15 },
        new BattleShopStageEquipmentWeights { minimumStage = 3, handWeight = 40, bodyWeight = 20, headWeight = 20, twoHandWeight = 20 },
    };''',
'''    public BattleShopStageEquipmentWeights[] equipmentByStage =
    {
        // 2026-08-22 밸런스 조정(사용자 확인): 손 55~40 / 몸통·머리 고정20 / 양손 5~20으로
        // 스테이지가 오를수록 손 비중이 줄고 양손이 느는 기존 표를, 손40/몸통30/머리20/양손10
        // 고정 비율로 전체 스테이지 동일하게 통일했다.
        new BattleShopStageEquipmentWeights { minimumStage = 1, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
        new BattleShopStageEquipmentWeights { minimumStage = 2, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
        new BattleShopStageEquipmentWeights { minimumStage = 3, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
    };'''
    ),
])

# 2) 실제 로드되는 BattleShopConfig.asset의 직렬화된 값도 함께 갱신
#    (ScriptableObject 자산은 이미 저장된 값이 .cs의 기본값보다 우선 적용되므로 .asset도 같이 고쳐야 실제 반영됨)
p2 = "Assets/renew/Battle/Resources/Battle/Shop/BattleShopConfig.asset"
apply(p2, [
    (
'''  equipmentByStage:
  - minimumStage: 1
    hand: 55
    body: 20
    head: 20
    twoHand: 5
  - minimumStage: 2
    hand: 45
    body: 20
    head: 20
    twoHand: 15
  - minimumStage: 3
    hand: 40
    body: 20
    head: 20
    twoHand: 20''',
'''  equipmentByStage:
  - minimumStage: 1
    handWeight: 40
    bodyWeight: 30
    headWeight: 20
    twoHandWeight: 10
  - minimumStage: 2
    handWeight: 40
    bodyWeight: 30
    headWeight: 20
    twoHandWeight: 10
  - minimumStage: 3
    handWeight: 40
    bodyWeight: 30
    headWeight: 20
    twoHandWeight: 10'''
    ),
])
