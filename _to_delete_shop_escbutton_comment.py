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
old = '''        // EscButton keeps a prefab-authored persistent onClick that calls
        // Event_Store.SetActive(false) directly, bypassing Close() (and therefore
        // SetShopOpen(false)/UnlockBattleInputAfterOverlay). Replacing the event object
        // drops that legacy call and routes the button through our own cleanup.'''
new = '''        // EscButton은 프리팹 제작 당시부터 박혀 있던 영구(persistent) onClick을 그대로 들고 있다
        // (Event_Store를 직접 SetActive(false)만 하는 이벤트). 이 이벤트는 Close()를 거치지 않으므로
        // SetShopOpen(false)/UnlockBattleInputAfterOverlay도 호출되지 않는다. onClick 이벤트 객체를
        // 통째로 새로 만들어 교체하면 그 레거시 호출이 사라지고, 우리 쪽 정리 로직(Close)만 남는다.'''
assert content.count(old) == 1
content = content.replace(old, new, 1)
save(p, content)
print("OK")
