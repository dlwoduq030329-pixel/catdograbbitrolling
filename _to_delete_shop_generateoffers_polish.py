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
'''    private void GenerateOffers(StoreState state)
    {
        List<int> cardCandidates = BuildEligibleCards();
        List<int> equipmentCandidates = new List<int>();
        if (equipmentDatabase != null)
            for (int i = 1; i < equipmentDatabase.equip.Count; i++) equipmentCandidates.Add(i);
        List<int> cardFallbacks = new List<int>(cardCandidates);
        List<int> equipmentFallbacks = new List<int>(equipmentCandidates);''',
'''    private void GenerateOffers(StoreState state)
    {
        // cardCandidates: 이번 진열에 쓸 수 있는 카드 인덱스 목록(BuildEligibleCards, 보유 2장 미만 등
        // 조건을 만족하는 카드). equipmentCandidates: 장비 데이터베이스의 1번(0번은 "미장착"이라 제외)부터
        // 끝까지 전부 — 카드와 달리 별도 자격 조건 없이 전체 장비가 후보다. 두 목록 다 아래 for문에서
        // 슬롯 하나를 채울 때마다 RemoveAt/Remove로 실제로 줄어든다(그래야 같은 상품이 한 상점 안에서
        // 중복으로 안 뜬다).
        List<int> cardCandidates = BuildEligibleCards();
        List<int> equipmentCandidates = new List<int>();
        if (equipmentDatabase != null)
            for (int i = 1; i < equipmentDatabase.equip.Count; i++) equipmentCandidates.Add(i);
        // cardFallbacks/equipmentFallbacks: 위 두 목록을 소모되기 "전" 시점에 통째로 복사해둔 원본이다.
        // 아래 fallback 분기(카드/장비 후보가 바닥나 슬롯이 빈 채로 남을 때)에서만 쓰이며, 여기서 뽑은
        // 상품은 이미 다른 슬롯에 나온 것과 중복될 수 있다(원본 개수 부족을 메우기 위한 최후 수단이라
        // 중복 방지를 포기하는 구조). "판매 콜백"이나 "이미 2장 있는 카드" 처리와는 무관하다.
        List<int> cardFallbacks = new List<int>(cardCandidates);
        List<int> equipmentFallbacks = new List<int>(equipmentCandidates);'''
    ),
    (
'''            // 데이터베이스가 작으면 지금 조건에 맞는 고유 상품이 6개보다 적을 수 있다.
            // 화면에 보이는 상점 슬롯을 빈칸으로 남겨두지 않기 위해, 카드를 먼저 재사용하고
            // 카드 보상이 아예 없을 때만 장비로 채운다.''',
'''            // 데이터베이스가 작으면 지금 조건에 맞는 고유 상품이 6개보다 적을 수 있다.
            // 화면에 보이는 상점 슬롯을 빈칸으로 남겨두지 않기 위해, 카드를 먼저 재사용하고
            // 카드 보상이 아예 없을 때만 장비로 채운다.
            //
            // 주의: 위 카드/장비 우선순위 스왑(바로 위 if/else if)이 있어서, 이 fallback은
            // "카드 후보와 장비 후보가 동시에 바닥났을 때"만 실행된다 — 장비 후보(equipmentCandidates)는
            // 데이터베이스 전체 장비 수만큼 있어서 한 상점(6슬롯)에서 바닥나는 일이 사실상 없으므로,
            // 실제로는 카드 후보(보유 중인 카드가 이미 많아 뽑을 카드가 없는 경우)가 원인이 되는
            // 경우가 거의 전부다. 그런데도 카드를 먼저 재사용하게 되어 있어, 이 fallback이 도는 순간엔
            // 장비가 뽑힐 확률이 더 낮다 — "장비가 아예 안 나온다"는 체감의 원인일 수 있다(발생 자체는
            // 드묾: 카드/장비 둘 다 동시에 완전히 바닥나야 함).''',
    ),
])
