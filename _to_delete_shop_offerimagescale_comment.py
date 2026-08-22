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
old = '''    // Item01 is 158x268 and the card image is naturally 100x150.8; at this scale the
    // image reaches ~150 wide / ~227 tall, filling the slot without reaching the
    // (always-visible) price text near the bottom.'''
new = '''    // Item01 슬롯 크기는 158x268이고 카드 이미지 원본은 100x150.8이다. 이 배율(1.5배)이면
    // 이미지가 가로 150/세로 227 정도로 커져서 슬롯을 거의 채우면서도, 항상 보이는 하단
    // 가격 텍스트 영역까지는 침범하지 않는다.'''
assert content.count(old) == 1
content = content.replace(old, new, 1)
save(p, content)
print("OK")
