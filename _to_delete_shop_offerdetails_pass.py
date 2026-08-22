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
'''    private void ShowOfferDetails(int slot)
    {''',
'''    /// <summary>
    /// 마우스가 슬롯 위에 있을 때(hover) 또는 슬롯을 클릭해 선택했을 때, 화면 오른쪽 상세 정보
    /// 영역(tagText/propertyText/damageText/equipmentInfoText + hoverPreviewImage)을 그 슬롯
    /// 내용으로 채운다. 카드/장비 중 무엇이 들어있는지에 따라 보여줄 텍스트 조합이 다르다(카드는
    /// TAG/PROPERTY/DAMAGE, 장비는 STR/DEX/VIT/WIS 스탯). 슬롯이 비어있거나 이미 팔렸으면
    /// HideOfferDetails로 정리한다. 호출부는 두 갈래다: (1) BattleShopOfferHover의 onEnter로
    /// 마우스를 올릴 때마다(단, purchaseButtonMode == None일 때만 — 뭔가 선택된 상태에서는 hover가
    /// 정보를 안 바꾼다), (2) SelectOfferForPurchase가 클릭으로 선택을 "고정"할 때 한 번.
    /// </summary>
    private void ShowOfferDetails(int slot)
    {'''
    ),
    (
'''    private void SelectOfferForPurchase(int slot)
    {
        if (currentState == null || slot < 0 || slot >= SlotCount || currentState.Sold[slot]) return;
        selectedPurchaseSlot = slot;
        selectedSellCardIndex = -1;
        purchaseButtonMode = PurchaseButtonMode.BuyOffer;
        ShowOfferDetails(slot);
        if (purchaseButton == null) return;
        purchaseButton.gameObject.SetActive(true);
        TMP_Text label = purchaseButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "BUY";
    }''',
'''    /// <summary>
    /// 진열 슬롯을 클릭해 "이걸 사겠다"고 선택을 확정한다(BattleShopOfferHover의 onClick으로 연결됨).
    /// purchaseButtonMode를 BuyOffer로 바꿔 공용 BUY/SELL 버튼을 "BUY"로 활성화하고, 정보 표시를
    /// 이 슬롯으로 고정한다(고정된 뒤에는 다른 슬롯에 마우스를 올려도 ShowOfferDetails의 hover 분기가
    /// purchaseButtonMode != None이라 무시됨 — 클릭으로 선택한 내용이 안 바뀌는 이유). 실제 구매는
    /// 이 메서드가 아니라 BUY 버튼 클릭 시 OnPurchaseButtonClicked → BuySelectedOffer → Buy(slot)에서
    /// 일어난다.
    /// </summary>
    private void SelectOfferForPurchase(int slot)
    {
        if (currentState == null || slot < 0 || slot >= SlotCount || currentState.Sold[slot]) return;
        selectedPurchaseSlot = slot;
        selectedSellCardIndex = -1;
        purchaseButtonMode = PurchaseButtonMode.BuyOffer;
        ShowOfferDetails(slot);
        if (purchaseButton == null) return;
        purchaseButton.gameObject.SetActive(true);
        TMP_Text label = purchaseButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "BUY";
    }'''
    ),
    (
'''    private void HideOfferDetails()
    {
        SetTextVisible(tagText, false, string.Empty);
        SetTextVisible(propertyText, false, string.Empty);
        SetTextVisible(damageText, false, string.Empty);
        SetTextVisible(equipmentInfoText, false, string.Empty);
        SetPreviewImage(null, string.Empty);
    }''',
'''    /// <summary>
    /// 오른쪽 상세 정보 영역을 전부 비운다(ShowOfferDetails가 켠 텍스트 4개 + 미리보기 이미지).
    /// 호출부가 많은 이유는 "정보를 지워야 하는 상황"이 여러 갈래라서다 — 마우스가 슬롯을 벗어날 때
    /// (BattleShopOfferHover의 onExit, purchaseButtonMode == None일 때만), 상점을 닫을 때(Close),
    /// 구매/판매를 확정하거나 취소해서 선택이 풀릴 때(BuySelectedOffer/ResetPurchaseSelection 계열),
    /// 슬롯이 비어있는 상태로 ShowOfferDetails가 불렸을 때. 전부 "지금 화면에 뭔가 상세정보가
    /// 떠 있으면 안 되는 시점"이라는 공통점이 있다.
    /// </summary>
    private void HideOfferDetails()
    {
        SetTextVisible(tagText, false, string.Empty);
        SetTextVisible(propertyText, false, string.Empty);
        SetTextVisible(damageText, false, string.Empty);
        SetTextVisible(equipmentInfoText, false, string.Empty);
        SetPreviewImage(null, string.Empty);
    }'''
    ),
])
