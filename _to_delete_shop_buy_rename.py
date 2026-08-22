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
old = '''    private void Buy(int slot)
    {
        if (currentState == null || slot < 0 || slot >= SlotCount || currentState.Sold[slot]) return;

        if (currentState.Kinds[slot] == OfferKind.Equipment)
        {
            BuyEquipment(slot);
            return;
        }
        if (currentState.Kinds[slot] != OfferKind.Card || currentState.OfferedCards[slot] < 0) return;

        int index = currentState.OfferedCards[slot];
        CardData card = BattleCardConnector.FindOriginalCard(index, originalCardDatabase);
        if (card == null) return;
        int owned = DataConfig.CardsCount.TryGetValue(index, out int count) ? count : 0;
        int price = Mathf.Max(0, card.cardCost * 2);
        if (owned >= 2) { Debug.LogWarning($"[Shop] {card.name} 보유 한도 2장", this); RefreshView(); return; }
        if (DataConfig.playerMoney < price) { Debug.LogWarning($"[Shop] 골드 부족: {price}G 필요", this); return; }

        DataConfig.playerMoney -= price;
        DataConfig.AddDic(index, 1);
        currentState.Sold[slot] = true;
        Debug.Log($"[Shop] 카드 구매: {card.name} / {price}G", this);
        RefreshView();
    }'''
new = '''    private void Buy(int slot)
    {
        // 이미 팔린 슬롯이거나 범위 밖이면 아무 것도 안 한다(연타 방지 겸 방어 코드).
        if (currentState == null || slot < 0 || slot >= SlotCount || currentState.Sold[slot]) return;

        if (currentState.Kinds[slot] == OfferKind.Equipment)
        {
            BuyEquipment(slot);
            return;
        }
        if (currentState.Kinds[slot] != OfferKind.Card || currentState.OfferedCards[slot] < 0) return;

        int cardIndex = currentState.OfferedCards[slot];
        CardData originalCard = BattleCardConnector.FindOriginalCard(cardIndex, originalCardDatabase);
        if (originalCard == null) return;
        int ownedCount = DataConfig.CardsCount.TryGetValue(cardIndex, out int currentOwnedCount) ? currentOwnedCount : 0;
        int price = Mathf.Max(0, originalCard.cardCost * 2);
        // 카드 1종당 최대 2장 보유 규칙 — BuildEligibleCards가 애초에 후보에서 걸러내지만, fallback
        // 재사용(GenerateOffers)이나 다른 슬롯에서 같은 카드를 이미 산 경우까지 대비해 여기서도 다시 확인한다.
        if (ownedCount >= 2) { Debug.LogWarning($"[Shop] {originalCard.name} 보유 한도 2장", this); RefreshView(); return; }
        if (DataConfig.playerMoney < price) { Debug.LogWarning($"[Shop] 골드 부족: {price}G 필요", this); return; }

        DataConfig.playerMoney -= price;
        DataConfig.AddDic(cardIndex, 1);
        currentState.Sold[slot] = true;
        Debug.Log($"[Shop] 카드 구매: {originalCard.name} / {price}G", this);
        RefreshView();
    }'''
assert content.count(old) == 1
content = content.replace(old, new, 1)
save(p, content)
print("OK")
