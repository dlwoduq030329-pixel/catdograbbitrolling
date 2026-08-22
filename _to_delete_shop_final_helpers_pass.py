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
'''    private void SetPreviewImage(Sprite sprite, string displayName)
    {''',
'''    /// <summary>
    /// 오른쪽 상세정보 영역의 큰 미리보기 이미지+이름 텍스트를 채우거나(sprite != null) 비운다
    /// (sprite == null이면 전부 숨김). ShowOfferDetails/HideOfferDetails/SelectInventoryCardForSale/
    /// SelectEquipmentSlotForSale 등 "지금 뭘 보여줄지 바뀌는" 모든 지점에서 공통으로 이 메서드를 거친다
    /// — 미리보기 이미지를 켜고 끄는 로직이 이 한 곳에만 있다.
    /// </summary>
    private void SetPreviewImage(Sprite sprite, string displayName)
    {'''
    ),
    (
'''                // InventoryStore.OnPointerDown is disabled along with the rest of the
                // legacy sell chain (StoreManager/StoreSet), so clicking an owned card
                // did nothing. Route the click through the same hover relay used by the
                // shop offers so tapping a card shows the SELL button instead.''',
'''                // InventoryStore.OnPointerDown은 legacy 판매 체인(StoreManager/StoreSet)이 같이
                // 비활성화되면서 덩달아 죽어, 보유 카드를 눌러도 아무 반응이 없었다. 상점 진열
                // 슬롯과 같은 호버 릴레이를 재사용해 클릭 시 SELL 버튼이 뜨도록 연결한다.'''
    ),
    (
'''    private void RefreshOwnedEquipmentSlot(EquipStore slot)
    {''',
'''    /// <summary>
    /// EquipStore.Init()은 DataPool.Instance를 요구해 Battle 상점에서 그대로 못 쓴다(DataPool이
    /// 갱신 안 돼 있을 수 있음). 그래서 리플렉션으로 private 필드(state/thisIMG)에 직접 접근해
    /// "지금 장착된 장비 아이콘을 그려주는 부분"만 골라 흉내낸다 — EquipStore 클래스 자체를 고치지
    /// 않고 화면 표시만 대신 처리하는 우회 방법. state는 이 슬롯이 왼손/오른손/몸통/머리 중 무엇인지,
    /// thisIMG는 아이콘을 그릴 Image 컴포넌트.
    /// </summary>
    private void RefreshOwnedEquipmentSlot(EquipStore slot)
    {'''
    ),
    (
'''    private static int GetEquippedIndex(EquipState state)
    {''',
'''    /// <summary>
    /// 지정한 장비 부위(state)에 지금 장착된 장비의 데이터베이스 인덱스를 반환한다(0=미장착).
    /// RefreshOwnedEquipmentSlot 하나에서만 쓰이는 작은 매핑 헬퍼라 그 바로 아래에 놓여 있다.
    /// </summary>
    private static int GetEquippedIndex(EquipState state)
    {'''
    ),
    (
'''    private static void SetTextVisible(TMP_Text target, bool visible, string value)
    {''',
'''    /// <summary>
    /// 텍스트 오브젝트를 값과 함께 켜거나(visible=true) 통째로 숨긴다(visible=false, 이때는 value를
    /// 빈 문자열로 넘기는 게 관례). ShowOfferDetails/HideOfferDetails가 4개 텍스트를 한 줄씩 켜고 끄는 데 씀.
    /// </summary>
    private static void SetTextVisible(TMP_Text target, bool visible, string value)
    {'''
    ),
    (
'''    private static string GetTargetLabel(BattleCardData card)
    {''',
'''    /// <summary>
    /// 카드의 대상(targetType: Self/Enemy/Ally/Character/Tile/AllEnemies)을 상세정보 TAG 줄에
    /// 표시할 영문 라벨로 바꾼다. ShowOfferDetails에서만 씀.
    /// </summary>
    private static string GetTargetLabel(BattleCardData card)
    {'''
    ),
    (
'''    private static string GetPropertyLabel(BattleCardData card)
    {''',
'''    /// <summary>
    /// 카드의 속성(cardType: PhysicalDamage/MagicDamage/그 외=서포트)을 상세정보 PROPERTY 줄에
    /// 표시할 영문 라벨로 바꾼다. ShowOfferDetails에서만 씀.
    /// </summary>
    private static string GetPropertyLabel(BattleCardData card)
    {'''
    ),
])

# FindNamedComponent -> FindComponentByName, FindNamedTransform -> FindTransformByName
# (파일 전체에서 각각 여러 번 나오는 식별자라 count==1 가드 없이 전체 치환)
content = load(p)
before_fc = content.count("FindNamedComponent")
before_ft = content.count("FindNamedTransform")
assert before_fc == 7, before_fc
assert before_ft == 4, before_ft
content = content.replace("FindNamedComponent", "FindComponentByName")
content = content.replace("FindNamedTransform", "FindTransformByName")
# FindComponentByName 정의부/FindTransformByName 정의부에 설명 주석 추가
content = content.replace(
'''    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            if (component != null && component.gameObject.name == objectName) return component;
        return null;
    }

    private static Transform FindTransformByName(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != null && child.gameObject.name == objectName) return child;
        return null;
    }''',
'''    /// <summary>
    /// root의 모든 자손(비활성 포함) 중에서 GameObject 이름이 objectName과 정확히 일치하는 T 컴포넌트를
    /// 찾는다. 레거시 프리팹(Event_Store 등)은 Inspector 참조 없이 이름만으로 슬롯/버튼/텍스트를
    /// 찾아 쓰는 구조라, TryBindSceneStoreView가 이 방식으로 필요한 UI 요소들을 전부 연결한다.
    /// 이름 검색이라 프리팹 구조가 바뀌면(오브젝트 이름 변경) 조용히 null을 반환하니 주의.
    /// </summary>
    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            if (component != null && component.gameObject.name == objectName) return component;
        return null;
    }

    /// <summary>FindComponentByName과 같은 방식의 이름 검색이지만 컴포넌트가 아니라 Transform 자체를 찾는다.</summary>
    private static Transform FindTransformByName(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != null && child.gameObject.name == objectName) return child;
        return null;
    }'''
)
save(p, content)
print("rename done:", before_fc, before_ft)
