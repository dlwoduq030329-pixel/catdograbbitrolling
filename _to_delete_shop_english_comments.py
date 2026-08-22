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

p = "Assets/renew/Battle/Shop/BattleCardShopSystem.cs"
apply(p, [
    (
'''            // The per-slot "Button" object inside Item01 is inactive by default in the
            // prefab (legacy design showed it only after a click elsewhere), so it can
            // never receive a raycasted click on its own. The slot root stays active, so
            // route the click through the same hover relay instead.
            //''',
'''            // Item01 안의 슬롯별 "Button" 오브젝트는 프리팹 기본값이 비활성 상태다(레거시 설계상
            // 다른 곳 클릭 후에야 보이게 돼 있었음), 그래서 이 버튼은 스스로 레이캐스트 클릭을
            // 받을 수 없다. 슬롯 루트는 항상 활성 상태이므로, 클릭도 같은 호버 릴레이(hover)를
            // 통해 전달한다.
            //'''
    ),
    (
'''            // Remove only runtime listeners. Unity keeps the prefab's persistent
            // DOTween callbacks, whose non-visual StoreManager targets were removed.''',
'''            // 런타임에 붙은 리스너만 제거한다. 프리팹에 미리 박혀 있는(persistent) DOTween
            // 콜백은 Unity가 그대로 유지하는데, 그 콜백이 가리키던 비시각적 StoreManager
            // 대상은 이미 제거된 상태다(참조가 끊긴 콜백이지만 굳이 지울 필요는 없음).'''
    ),
    (
'''            // Preserve persistent animation callbacks and replace runtime logic.''',
'''            // 프리팹에 박혀 있는(persistent) 애니메이션 콜백은 그대로 두고, 런타임 로직만 교체한다.'''
    ),
])
