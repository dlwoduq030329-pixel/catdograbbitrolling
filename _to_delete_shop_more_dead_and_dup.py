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
    # 1) GetRarityColor 삭제 (호출부 0개, 완전한 죽은 코드)
    (
'''    private static Color GetRarityColor(weaponSt rarity)
    {
        switch (rarity)
        {
            case weaponSt.Rare: return Color.green;
            case weaponSt.Epic: return new Color(0.64f, 0.21f, 0.93f, 1f);
            case weaponSt.Legendary: return Color.yellow;
            default: return Color.white;
        }
    }
''',
'''    // (2026-08-22 정리, 사용자 확인: GetRarityColor 삭제됨 - 저장소 전체에서 호출부 0개.
    // 등급별 색상을 표시하려던 흔적으로 보이나 실제로 UI에 연결된 적이 없다.)
'''
    ),
    # 2) TryBindSceneStoreView 끝의 RefreshOwnedInventory() 중복 호출 제거
    #    (TryEnter가 이 메서드 성공 직후 곧바로 RefreshView()를 호출하고, RefreshView() 끝에서
    #    다시 RefreshOwnedInventory()를 호출하므로 여기서의 호출은 매번 낭비되는 중복이었다.)
    (
'''        HideOfferDetails();
        RefreshOwnedInventory();
        viewRoot.SetActive(false);
        return true;
    }''',
'''        HideOfferDetails();
        // (2026-08-22 정리, 사용자 확인: 여기 있던 RefreshOwnedInventory() 중복 호출 제거 - 이 메서드가
        // 끝나면 TryEnter가 곧바로 RefreshView()를 부르고, RefreshView()도 끝에서 RefreshOwnedInventory()를
        // 부르기 때문에 원래 매번 인벤토리를 두 번 그리고 있었다.)
        viewRoot.SetActive(false);
        return true;
    }'''
    ),
])
