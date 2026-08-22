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
'''    private int PickEquipmentCandidate(List<int> candidates)
    {''',
'''    /// <summary>
    /// 후보 목록에서 장비 하나를 뽑는다. 1차로 <c>shopConfig.RollEquipmentKind</c>(부위별 가중치
    /// 추첨, 현재 40/30/20/10 손/몸통/머리/양손)로 선호 부위를 정하고, 그 부위 후보가 있으면
    /// 풀을 좁힌다(없으면 전체 후보 그대로 사용). 2차로 같은 풀 안에서 "이미 장착 중=가중치1,
    /// 미장착=가중치4"로 다시 추첨해 안 써본 장비가 더 잘 나오게 한다.
    /// </summary>
    private int PickEquipmentCandidate(List<int> candidates)
    {'''
    ),
    (
'''    private static bool IsEquipped(int equipmentIndex)
    {''',
'''    /// <summary>이 장비 인덱스가 지금 왼손/오른손/몸통/머리 중 하나로 장착되어 있는지 확인한다.</summary>
    private static bool IsEquipped(int equipmentIndex)
    {'''
    ),
    (
'''    private EquipData CreateRandomEquipment(int equipmentIndex)
    {''',
'''    /// <summary>
    /// 지정한 장비 원본을 복제(Clone)해 등급을 <c>shopConfig.RollRarity</c>로 굴리고, 부위별
    /// 기본가(양손90/몸통75/머리70/한손60)에 등급 배율(rare1.6/epic2.5/legendary4)을 곱해
    /// 가격을 매긴다. 등급이 높을수록 보너스 스탯 굴림 횟수(bonusRolls)도 늘어난다
    /// (rare4/epic6/legendary10회, 매 회 STR/WIS/DEX/VIT 중 하나를 무작위로 +1).
    /// </summary>
    private EquipData CreateRandomEquipment(int equipmentIndex)
    {'''
    ),
    (
'''    private List<int> BuildEligibleCards()
    {''',
'''    /// <summary>
    /// 상점에 카드로 진열될 수 있는 후보를 모은다 — battleCardDatabase의 각 카드가 originalCardDatabase에도
    /// 실제로 존재하고(BattleCardConnector로 연결 확인), 이미 2장을 보유하지 않은 경우만 후보로 포함한다
    /// (카드 보유 한도 2장은 Buy에서도 같은 기준으로 다시 확인함).
    /// </summary>
    private List<int> BuildEligibleCards()
    {'''
    ),
    (
'''    private void Buy(int slot)
    {''',
'''    /// <summary>
    /// 슬롯 클릭 시 구매를 처리하는 진입점이다. 장비 슬롯이면 <see cref="BuyEquipment"/>로 위임하고,
    /// 카드 슬롯이면 가격(카드 원가의 2배)과 보유 한도(2장)를 확인한 뒤 골드를 차감하고 카드를 지급한다.
    /// </summary>
    private void Buy(int slot)
    {'''
    ),
    (
'''    private void BuyEquipment(int slot)
    {
        EquipData equipment = currentState.OfferedEquipment[slot];
        if (equipment == null) return;''',
'''    /// <summary>
    /// 장비 슬롯 구매를 시작한다. pendingEquipmentSlot을 세팅한 뒤 바로
    /// <see cref="ConfirmEquipmentPurchaseInHand"/>(null)를 호출해 확인창 없이 즉시 구매/장착까지
    /// 이어간다(그 이유는 ConfirmEquipmentPurchaseInHand 요약 참고).
    /// </summary>
    private void BuyEquipment(int slot)
    {
        EquipData equipment = currentState.OfferedEquipment[slot];
        if (equipment == null) return;'''
    ),
])
