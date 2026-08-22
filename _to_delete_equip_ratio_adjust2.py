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

# 1) BattleShopConfig.cs 기본값 배열: 옛 필드명(hand/body/head/twoHand)이 아직 남아있던 것을
#    새 필드명(handWeight/...)으로 고치는 동시에 40/30/20/10 비율로 통일한다.
#    (주의: 이 배열이 옛 필드명을 그대로 쓰고 있었다는 건 클래스 필드가 handWeight로 이미 바뀐 상태에서
#    이 배열만 놓쳐서 실제로는 컴파일이 안 되는 상태였다는 뜻 — 비율 조정과 함께 반드시 고쳐야 함.)
p1 = "Assets/renew/Battle/Shop/BattleShopConfig.cs"
apply(p1, [
    (
'''    public BattleShopStageEquipmentWeights[] equipmentByStage =
    {
        new BattleShopStageEquipmentWeights { minimumStage = 1, hand = 55, body = 20, head = 20, twoHand = 5 },
        new BattleShopStageEquipmentWeights { minimumStage = 2, hand = 45, body = 20, head = 20, twoHand = 15 },
        new BattleShopStageEquipmentWeights { minimumStage = 3, hand = 40, body = 20, head = 20, twoHand = 20 },
    };''',
'''    public BattleShopStageEquipmentWeights[] equipmentByStage =
    {
        // 2026-08-22 정리+밸런스 조정(사용자 확인): 이 배열이 필드 rename 이후에도 옛 이름(hand/body/head/twoHand)을
        // 그대로 쓰고 있어 실제로는 컴파일이 안 되는 상태였다 — 새 필드명으로 고치면서 손55~40/몸통·머리 고정20/
        // 양손5~20으로 스테이지마다 달랐던 표를 손40/몸통30/머리20/양손10 고정 비율로 전체 스테이지 동일하게 통일했다.
        new BattleShopStageEquipmentWeights { minimumStage = 1, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
        new BattleShopStageEquipmentWeights { minimumStage = 2, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
        new BattleShopStageEquipmentWeights { minimumStage = 3, handWeight = 40, bodyWeight = 30, headWeight = 20, twoHandWeight = 10 },
    };'''
    ),
])

# 2) 실제 로드되는 BattleShopConfig.asset의 직렬화된 값도 함께 갱신
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
