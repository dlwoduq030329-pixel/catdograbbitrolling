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

p = "Assets/renew/Battle/Shop/BattleLegacyStoreViewAdapter.cs"
apply(p, [
    # 1) Binding 클래스 요약 추가
    (
'''public static class BattleLegacyStoreViewAdapter
{
    public sealed class Binding
    {''',
'''public static class BattleLegacyStoreViewAdapter
{
    /// <summary>
    /// <see cref="TryBind"/>가 레거시 상점 프리팹에서 이름으로 찾아낸 살아있는 씬 오브젝트 참조 묶음이다.
    /// 프리팹 원본이나 설정값이 아니라 "지금 이 상점 세션에서 실제로 화면에 붙어 있는 슬롯 버튼/이미지/
    /// 텍스트가 바로 이것"이라는 확정된 연결 결과를 담는다 — 호출부(BattleCardShopSystem)는 이 결과만
    /// 가지고 상점 UI를 갱신하면 되고, 이름 검색이나 컴포넌트 탐색을 다시 할 필요가 없다.
    /// </summary>
    public sealed class Binding
    {'''
    ),

    # 2) TryBind 요약 추가
    (
'''    public static bool TryBind(GameObject legacyRoot, int slotCount, out Binding binding)
    {''',
'''    /// <summary>
    /// 이미 Instantiate된 레거시 상점 프리팹 루트(<paramref name="legacyRoot"/>)에서 이름으로 자식을
    /// 찾아 <paramref name="slotCount"/>개 슬롯 전부와 나머지 UI 요소(골드 텍스트, 리롤, 닫기 버튼)를
    /// 한 번에 연결한다. 슬롯 하나라도 필요한 요소(StoreCardOwn, Item01, Button 등)를 못 찾으면 그
    /// 즉시 실패로 처리한다 — 일부만 연결된 상태로 값을 돌려주지 않는다("전부 성공 아니면 전부 실패").
    /// </summary>
    public static bool TryBind(GameObject legacyRoot, int slotCount, out Binding binding)
    {'''
    ),

    # 3) item -> itemTransform, price -> cardPriceText (읽을 때 헷갈리지 않게)
    (
'''            Transform item = FindNamedChild(slotRoot, "Item01");
            if (item == null) return false;

            Image cardImage = legacySlots[i].StoreImage;
            TMP_Text cardName = legacySlots[i].StoreNameText;
            TMP_Text price = legacySlots[i].StorePriceText;
            Button button = FindNamedChild(item, "Button")?.GetComponent<Button>();
            if (cardImage == null || cardName == null || price == null || button == null) return false;

            result.SlotRoots[i] = slotRoot.gameObject;
            result.Buttons[i] = button;
            result.CardImages[i] = cardImage;
            result.CardNames[i] = cardName;
            result.CardPrices[i] = price;''',
'''            // "item"은 오브젝트 자체가 아니라 그 아래 Button을 찾기 위한 Transform이라 itemTransform으로 명명.
            Transform itemTransform = FindNamedChild(slotRoot, "Item01");
            if (itemTransform == null) return false;

            Image cardImage = legacySlots[i].StoreImage;
            TMP_Text cardName = legacySlots[i].StoreNameText;
            TMP_Text cardPriceText = legacySlots[i].StorePriceText;
            Button button = FindNamedChild(itemTransform, "Button")?.GetComponent<Button>();
            if (cardImage == null || cardName == null || cardPriceText == null || button == null) return false;

            result.SlotRoots[i] = slotRoot.gameObject;
            result.Buttons[i] = button;
            result.CardImages[i] = cardImage;
            result.CardNames[i] = cardName;
            result.CardPrices[i] = cardPriceText;'''
    ),

    # 4) EscButton 영문 주석 -> 한글로 번역
    (
'''        // "EscButton" still carries its original prefab-authored persistent onClick
        // (a direct GameObject.SetActive(false) on the Event_Store root). That call
        // bypasses BattleCardShopSystem.Close() entirely, so it never restores the
        // battle HUD or releases the modal input lock. The caller must replace this
        // button's onClick with its own Close() before use.''',
'''        // "EscButton"은 프리팹 제작 당시부터 박혀 있던 영구(persistent) onClick을 여전히 그대로 들고 있다
        // (Event_Store 루트를 직접 SetActive(false)만 하는 이벤트). 이 이벤트는 BattleCardShopSystem.Close()를
        // 전혀 거치지 않으므로, 전투 HUD 복구도 입력 잠금 해제도 일어나지 않는다. 이 버튼을 실제로 쓰기 전에
        // 호출부가 반드시 onClick을 자신의 Close()로 교체해야 한다(BattleCardShopSystem이 이렇게 하고 있음).'''
    ),

    # 5) FindNamedChild 요약 추가
    (
'''    private static Transform FindNamedChild(Transform root, string objectName)
    {''',
'''    /// <summary>root의 모든 자손(자기 자신 제외)을 재귀로 뒤져 이름이 일치하는 첫 Transform을 반환한다.</summary>
    private static Transform FindNamedChild(Transform root, string objectName)
    {'''
    ),
])
