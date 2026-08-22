# -*- coding: utf-8 -*-
import os

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
        assert count == 1, (path, i, count, old[:80])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

# 1) BattlePlayerRegistrationService.cs: use the renamed factory
p1 = "Assets/renew/Battle/Player/BattlePlayerRegistrationService.cs"
apply(p1, [
    ('BattlePlayerRuntimeDataFactory.TryCreate(player, out playerMP, out combatData, out playerHealth)',
     'BattlePlayerCombatDataFactory.TryCreate(player, out playerMP, out combatData, out playerHealth)'),
])

# 2) BattleGameManager.cs: field type -> renamed binder (field name kept, no Scene YAML impact)
p2 = "Assets/renew/Battle/Core/BattleGameManager.cs"
apply(p2, [
    ('[SerializeField] private BattlePlayerRuntimeBinder playerRuntimeBinder;',
     '[SerializeField] private BattlePlayerRegistrationBinder playerRuntimeBinder;'),
])

# 3) retire old files (device_bash can't delete -> rename to _to_delete_ prefix)
old_files = [
    "Assets/renew/Battle/Player/BattlePlayerRuntimeBinder.cs",
    "Assets/renew/Battle/Player/BattlePlayerRuntimeBinder.cs.meta",
    "Assets/renew/Battle/Player/BattlePlayerRuntimeDataFactory.cs",
    "Assets/renew/Battle/Player/BattlePlayerRuntimeDataFactory.cs.meta",
]
for f in old_files:
    new_name = os.path.join(os.path.dirname(f), "_to_delete_" + os.path.basename(f))
    os.rename(f, new_name)
    print("renamed old file ->", new_name)

print("ALL DONE")
