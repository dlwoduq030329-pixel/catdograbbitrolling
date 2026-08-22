# -*- coding: utf-8 -*-
# Unity가 살아있는 상태라 복사된 5개 파일의 .meta guid를 이미 자동으로 새로 배정해줬다
# (원본과 충돌 나지 않게 스스로 재발급함). 그래서 우리가 직접 guid를 새로 만들 필요는 없고,
# 대신 복사된 prefab 파일 내부의 "옛 텍스처 guid 참조"를 Unity가 새로 배정한 "복사된 텍스처의
# 새 guid"로 바꿔주고, 참조 ScriptableObject도 복사된 prefab의 새 guid를 가리키게 갱신한다.
def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

DST = "Assets/renew/Battle/Rewards/ChestVfx"

# old(Assets/Game 원본) guid -> Unity가 복사본에 새로 배정한 guid
GUID_MAP = {
    "529390fdf55d1fc41a68e854e4c0361f": "5c0f658fc4e429a45acc2b885877a25c",  # TreasureChest_Start.prefab
    "c93f94d0a95b49e40b335baf2f0f0767": "fcfc6c0af881c8844924c61ff2dcfc4b",  # M_TeasureBody.png
    "8b9f7979c47e48945b01b615b5bfa629": "b066774539be0a64395376b6923f2aa1",  # M_TreasureLid_Closed.png
    "1e673faa81ef5d14280ced574740fec1": "b5d7a9540e25ee446ac2ccb5cdf54193",  # M_TreasureLid_Opened.png
    "36779b2adbc7eb64da67baf7e74c458d": "b29fb41727b82cc468f497a86af484f4",  # M_TreasureMask.png
}

# 1) 복사된 prefab 내부의 텍스처 guid 참조 4개만 새 guid로 교체(스크립트/기타 guid는 그대로 둔다)
prefab_path = f"{DST}/TreasureChest_Start.prefab"
content = load(prefab_path)
texture_old_guids = [
    "c93f94d0a95b49e40b335baf2f0f0767",
    "8b9f7979c47e48945b01b615b5bfa629",
    "1e673faa81ef5d14280ced574740fec1",
    "36779b2adbc7eb64da67baf7e74c458d",
]
for old_guid in texture_old_guids:
    new_guid = GUID_MAP[old_guid]
    count = content.count(f"guid: {old_guid}")
    assert count >= 1, (old_guid, count)
    content = content.replace(f"guid: {old_guid}", f"guid: {new_guid}")
    print(f"PREFAB replaced {count}x  {old_guid} -> {new_guid}")
save(prefab_path, content)

# 2) BattleLegacyChestPrefabReference.asset이 복사된 prefab(새 guid)을 가리키도록 갱신
ref_path = "Assets/renew/Battle/Resources/Battle/Rewards/BattleLegacyChestPrefabReference.asset"
content = load(ref_path)
old_line = "prefab: {fileID: 1717158328947291858, guid: 529390fdf55d1fc41a68e854e4c0361f, type: 3}"
new_line = "prefab: {fileID: 1717158328947291858, guid: 5c0f658fc4e429a45acc2b885877a25c, type: 3}"
count = content.count(old_line)
assert count == 1, count
content = content.replace(old_line, new_line, 1)
save(ref_path, content)
print("REFERENCE ASSET updated ->", ref_path)
