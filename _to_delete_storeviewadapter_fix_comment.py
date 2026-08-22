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
    (
'''/// <summary>
/// 원본 Store 프리팹(Assets/Game/UI/UITexture/Store/Store.prefab)의 판매 슬롯과
/// 표시 요소를 Battle 상점에 연결한다.
///
/// 이 프리팹에는 StoreManager/StoreCardOwn 같은 레거시 상점 스크립트가 붙어있지
/// 않다 (직접 확인: 프리팹 전체에서 두 스크립트의 GUID가 한 번도 등장하지 않는다).
/// "StoreItems" 스크롤 목록 아래 있는 "ShopItem01 (1)/(2)/(3)" 오브젝트는 로직 없는
/// 순수 UI 프리팹 인스턴스(Item01 하위에 CardImage/CardName/Price/Button)일 뿐이다.
/// 그래서 리플렉션으로 레거시 스크립트 필드를 읽던 예전 방식 대신, 이름으로 직접
/// 찾아 바인딩한다. 이전 방식은 StoreManager를 못 찾아 항상 실패했고, 그 결과
/// Battle 상점이 매번 레거시 UI 대신 임시 회색 박스 UI로 폴백하고 있었다.
///
/// 참고: 같은 프리팹 안에 "Inventory" 섹션도 동일한 이름("ShopItem01 (1)" 등)의
/// 오브젝트를 재사용하므로, 반드시 "StoreItems" 하위에서만 검색해야 한다.
/// </summary>''',
'''/// <summary>
/// Battle 상점이 실제로 바인딩하는 대상은 <c>BattleLegacyStorePrefabReference</c>가 가리키는
/// <c>Assets/renew/Battle/Event_Store.prefab</c>이다(2026-08-22 정리: 아래 문단은 원래
/// <c>Assets/Game/UI/UITexture/Store/Store.prefab</c>을 직접 조사하고 쓴 것이라, 실제 바인딩
/// 대상과 다른 프리팹을 설명하고 있었다 — StoreManager는 두 프리팹 모두에 없지만, StoreCardOwn은
/// <c>Event_Store.prefab</c>의 슬롯 6곳에는 실제로 붙어 있다(직접 재확인:
/// StoreCardOwn.cs.meta guid가 Event_Store.prefab 안에 6회 등장). 아래 CardImage/CardName/Price
/// 필드 접근이 실제로 StoreCardOwn 컴포넌트를 통해 동작하는 이유다.
///
/// "StoreItems" 스크롤 목록 아래 있는 "ShopItem01 (1)/(2)/(3)" 오브젝트 각각에 StoreCardOwn이
/// 붙어 있고, 그 컴포넌트의 StoreImage/StoreNameText/StorePriceText/StorePreviewImage 필드로
/// 이미지·텍스트를 가져온다. Button만 StoreCardOwn에 없어서 "Item01" 자식 아래 이름으로 직접 찾는다.
///
/// 참고: 같은 프리팹 안에 "Inventory" 섹션도 동일한 이름("ShopItem01 (1)" 등)의
/// 오브젝트를 재사용하므로, 반드시 "StoreItems" 하위에서만 검색해야 한다.
/// </summary>'''
    ),
])
