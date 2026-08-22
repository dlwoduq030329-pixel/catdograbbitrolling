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
'''    private void Reroll()
    {
        if (currentState == null) return;
        if (DataConfig.playerMoney < currentState.RerollPrice)
        { Debug.LogWarning($"[Shop] 리롤 골드 부족: {currentState.RerollPrice}G 필요", this); return; }
        DataConfig.playerMoney -= currentState.RerollPrice;
        Debug.Log($"[Shop] 상품 리롤 / {currentState.RerollPrice}G", this);
        int maximumPrice = shopConfig != null ? shopConfig.maximumRerollPrice : 160;
        currentState.RerollPrice = Mathf.Min(maximumPrice, currentState.RerollPrice * 2);
        ResetPurchaseSelection();
        GenerateOffers(currentState);
        RefreshView();
    }

    private void Close()
    {
        CancelEquipmentPurchase();
        ResetPurchaseSelection();
        HideOfferDetails();
        if (viewRoot != null) viewRoot.SetActive(false);
        BattleGameManager.Instance?.SetShopOpen(false);
        ReleaseModalLock();
        currentStore = null;
        currentState = null;
    }

    public void ForceClose()
    {
        Close();
    }

    private void AcquireModalLock()
    {
        if (holdsModalLock) return;
        BattleGameManager.Instance?.LockBattleInputForOverlay();
        holdsModalLock = true;
    }

    private void ReleaseModalLock()
    {
        if (!holdsModalLock) return;
        holdsModalLock = false;
        BattleGameManager.Instance?.UnlockBattleInputAfterOverlay();
    }''',
'''    /// <summary>
    /// 골드를 내고 6슬롯 전체를 GenerateOffers로 완전히 새로 뽑는다(일부 슬롯만 바꾸는 게 아니라
    /// 진열 전체가 리셋됨 — 안 팔린 카드/장비도 전부 사라지고 새 목록으로 대체된다). 다음 리롤 가격은
    /// 2배로 오르되 shopConfig.maximumRerollPrice(기본 160G)에서 상한이 걸린다. Close()와는 무관한
    /// 별개 동작이다 — Close()는 Reroll()을 호출하지 않는다(오해하기 쉬운 부분이라 명시해둠).
    /// </summary>
    private void Reroll()
    {
        if (currentState == null) return;
        if (DataConfig.playerMoney < currentState.RerollPrice)
        { Debug.LogWarning($"[Shop] 리롤 골드 부족: {currentState.RerollPrice}G 필요", this); return; }
        DataConfig.playerMoney -= currentState.RerollPrice;
        Debug.Log($"[Shop] 상품 리롤 / {currentState.RerollPrice}G", this);
        int maximumPrice = shopConfig != null ? shopConfig.maximumRerollPrice : 160;
        currentState.RerollPrice = Mathf.Min(maximumPrice, currentState.RerollPrice * 2);
        ResetPurchaseSelection();
        GenerateOffers(currentState);
        RefreshView();
    }

    /// <summary>
    /// 상점 UI를 정상적으로 닫는 내부 경로다(ESC나 닫기 버튼이 이걸 호출 — TryBindSceneStoreView에서
    /// binding.EscButton.onClick과 CreateButton으로 만든 CLOSE 버튼 둘 다 이 메서드에 연결됨).
    /// 대기 중이던 장비구매/선택 상태를 전부 취소하고, 모달 입력잠금을 풀고, 이 타일의 currentState
    /// 참조를 비운다(다음에 다른 상점 타일에 들어갈 때 실수로 이전 상태를 쓰지 않도록).
    /// </summary>
    private void Close()
    {
        CancelEquipmentPurchase();
        ResetPurchaseSelection();
        HideOfferDetails();
        if (viewRoot != null) viewRoot.SetActive(false);
        BattleGameManager.Instance?.SetShopOpen(false);
        ReleaseModalLock();
        currentStore = null;
        currentState = null;
    }

    /// <summary>
    /// Close()와 별개로 존재하는 "외부에서 강제로 닫기" 공개 API다. BattleGameManager가 전투 종료/
    /// 플레이어 사망 등으로 열려 있는 모든 오버레이를 한 번에 정리할 때 ChestRewardSystem.ForceClose()와
    /// 나란히 호출한다(BattleGameManager.cs:477-478). 지금은 Close()와 동작이 완전히 같지만, 나중에
    /// "정상 닫기"와 "강제 닫기"를 다르게 처리해야 할 경우(예: 강제 닫기는 확인 없이 즉시)를 위해
    /// 호출부를 분리해둔 것으로 보인다.
    /// </summary>
    public void ForceClose()
    {
        Close();
    }

    /// <summary>
    /// 상점이 열려 있는 동안 전투 입력을 잠근다(모달 UI 뒤에서 실수로 유닛을 조작하지 못하게).
    /// holdsModalLock으로 중복 잠금을 막는다 — 이미 잠근 상태에서 또 호출해도 안전.
    /// </summary>
    private void AcquireModalLock()
    {
        if (holdsModalLock) return;
        BattleGameManager.Instance?.LockBattleInputForOverlay();
        holdsModalLock = true;
    }

    /// <summary>AcquireModalLock으로 잠근 입력을 되돌린다. 잠근 적이 없으면 아무 것도 안 한다.</summary>
    private void ReleaseModalLock()
    {
        if (!holdsModalLock) return;
        holdsModalLock = false;
        BattleGameManager.Instance?.UnlockBattleInputAfterOverlay();
    }'''
    ),
])
