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
'''    private void OnDisable()
    {
        ReleaseModalLock();
    }

    private void RefreshView()
    {''',
'''    /// <summary>오브젝트가 비활성화될 때(씬 전환 등) 모달 잠금을 확실히 풀어둔다.</summary>
    private void OnDisable()
    {
        ReleaseModalLock();
    }

    /// <summary>
    /// 상점 UI 전체를 지금 상태(currentState)에 맞게 다시 그린다 — 골드/리롤가격/6슬롯 상품 이미지·
    /// 가격·품절 표시·구매 가능 여부까지 전부. 구매/판매/리롤/최초 진입 등 상태가 바뀌는 모든 지점에서
    /// 마지막에 이 메서드를 부른다(진열 데이터를 바꾸는 로직과 화면을 그리는 로직을 분리하는 패턴).
    /// </summary>
    private void RefreshView()
    {'''
    ),
    (
'''    private void FillExistingEmptySlots(StoreState state)
    {
        List<int> cards = BuildEligibleCards();
        List<int> equipment = new List<int>();
        if (equipmentDatabase != null)
            for (int i = 1; i < equipmentDatabase.equip.Count; i++) equipment.Add(i);

        for (int i = 0; i < SlotCount; i++)
        {
            if (state.Kinds[i] != OfferKind.None) continue;
            if (cards.Count > 0)
            {
                state.Kinds[i] = OfferKind.Card;
                state.OfferedCards[i] = cards[Random.Range(0, cards.Count)];
            }
            else if (equipment.Count > 0)
            {
                state.Kinds[i] = OfferKind.Equipment;
                state.OfferedEquipment[i] = CreateRandomEquipment(PickEquipmentCandidate(equipment));
            }
        }
    }''',
'''    /// <summary>
    /// RefreshView가 매번(구매/판매/리롤/진입 후 전부) 호출하는 안전망이다. 정상적인 흐름이라면
    /// GenerateOffers가 6슬롯을 전부 채우므로 Kinds[i] == None인 슬롯은 사실상 나오지 않는데
    /// (GenerateOffers의 fallback 주석 참고 — 카드+장비 후보가 동시에 완전히 바닥나야 발생),
    /// 혹시라도 None 슬롯이 남아 있으면 여기서 "지금 시점 기준으로" 다시 계산한 카드/장비 후보로
    /// 채운다. GenerateOffers의 fallback과 다른 점: 저기는 상점 생성 시점의 스냅샷(cardFallbacks 등)을
    /// 쓰지만, 여기는 호출될 때마다 BuildEligibleCards를 새로 불러 최신 보유 현황을 반영한다.
    /// </summary>
    private void FillExistingEmptySlots(StoreState state)
    {
        List<int> eligibleCards = BuildEligibleCards();
        List<int> eligibleEquipment = new List<int>();
        if (equipmentDatabase != null)
            for (int i = 1; i < equipmentDatabase.equip.Count; i++) eligibleEquipment.Add(i);

        for (int i = 0; i < SlotCount; i++)
        {
            if (state.Kinds[i] != OfferKind.None) continue;
            if (eligibleCards.Count > 0)
            {
                state.Kinds[i] = OfferKind.Card;
                state.OfferedCards[i] = eligibleCards[Random.Range(0, eligibleCards.Count)];
            }
            else if (eligibleEquipment.Count > 0)
            {
                state.Kinds[i] = OfferKind.Equipment;
                state.OfferedEquipment[i] = CreateRandomEquipment(PickEquipmentCandidate(eligibleEquipment));
            }
        }
    }'''
    ),
    (
'''    private void EnsureView()
    {
        if (viewRoot != null) return;
        if (TryBindSceneStoreView()) return;
        Debug.LogError("[Shop] Battle Canvas 아래에서 Event_Store 프리팹 인스턴스를 찾지 못했습니다.", this);
    }''',
'''    /// <summary>
    /// 상점 뷰가 아직 없으면(viewRoot == null) 딱 한 번만 TryBindSceneStoreView로 만든다. TryEnter가
    /// 상점 타일에 들어갈 때마다 호출하지만, 두 번째 방문부터는 viewRoot가 이미 있어 즉시 반환한다.
    /// </summary>
    private void EnsureView()
    {
        if (viewRoot != null) return;
        if (TryBindSceneStoreView()) return;
        Debug.LogError("[Shop] Battle Canvas 아래에서 Event_Store 프리팹 인스턴스를 찾지 못했습니다.", this);
    }'''
    ),
])
