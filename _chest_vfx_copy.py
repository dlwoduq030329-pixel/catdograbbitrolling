# -*- coding: utf-8 -*-
import re

def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

DST = "Assets/renew/Battle/Rewards/ChestVfx"

GUID_MAP = {
    "529390fdf55d1fc41a68e854e4c0361f": "3c05d0d390044a3dae124164412f704d",  # TreasureChest_Start.prefab
    "c93f94d0a95b49e40b335baf2f0f0767": "921d73a1d2c04845984b757ccabcc911",  # M_TeasureBody.png
    "8b9f7979c47e48945b01b615b5bfa629": "cc3e5a0658d049a281e448f248c0cf12",  # M_TreasureLid_Closed.png
    "1e673faa81ef5d14280ced574740fec1": "a30513b6817b4ac7b5add7fc9befd529",  # M_TreasureLid_Opened.png
    "36779b2adbc7eb64da67baf7e74c458d": "ee80929a5f354910b805c490c4d8b0ac",  # M_TreasureMask.png
}

# 1) 각 .meta 파일: 자기 자신의 guid 줄만 새 guid로 교체
meta_files = [
    ("TreasureChest_Start.prefab.meta", "529390fdf55d1fc41a68e854e4c0361f"),
    ("M_TeasureBody.png.meta", "c93f94d0a95b49e40b335baf2f0f0767"),
    ("M_TreasureLid_Closed.png.meta", "8b9f7979c47e48945b01b615b5bfa629"),
    ("M_TreasureLid_Opened.png.meta", "1e673faa81ef5d14280ced574740fec1"),
    ("M_TreasureMask.png.meta", "36779b2adbc7eb64da67baf7e74c458d"),
]
for filename, old_guid in meta_files:
    path = f"{DST}/{filename}"
    content = load(path)
    new_guid = GUID_MAP[old_guid]
    pattern = re.compile(r"^guid: " + old_guid + r"$", re.MULTILINE)
    matches = pattern.findall(content)
    assert len(matches) == 1, (path, len(matches))
    content = pattern.sub(f"guid: {new_guid}", content)
    save(path, content)
    print("META OK:", path, "->", new_guid)

# 2) 복사된 prefab 파일 안에서, 4개 텍스처 guid 참조만 새 guid로 교체(스크립트/기타 guid는 그대로 둔다)
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

# 3) BattleLegacyChestPrefabReference.asset이 새 복사본(prefab)을 가리키도록 갱신
ref_path = "Assets/renew/Battle/Resources/Battle/Rewards/BattleLegacyChestPrefabReference.asset"
content = load(ref_path)
old_line = "prefab: {fileID: 1717158328947291858, guid: 529390fdf55d1fc41a68e854e4c0361f, type: 3}"
new_line = "prefab: {fileID: 1717158328947291858, guid: 3c05d0d390044a3dae124164412f704d, type: 3}"
count = content.count(old_line)
assert count == 1, count
content = content.replace(old_line, new_line, 1)
save(ref_path, content)
print("REFERENCE ASSET updated ->", ref_path)
