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

p = "Assets/renew/Battle/Shop/BattleEquipmentDatabaseReference.cs"
apply(p, [
    (
'''using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleEquipmentDatabaseReference",
    menuName = "Renew/Battle/Equipment Database Reference")]
public sealed class BattleEquipmentDatabaseReference : ScriptableObject
{
    private const string ResourcePath = "Battle/Shop/BattleEquipmentDatabaseReference";

    [SerializeField] private EquipDatabase database;
    public EquipDatabase Database => database;

    public static BattleEquipmentDatabaseReference Load() =>
        Resources.Load<BattleEquipmentDatabaseReference>(ResourcePath);
}''',
'''using UnityEngine;

/// <summary>
/// Battle에서 원본 장비 데이터베이스(<c>EquipDatabase</c>)를 직접 참조를 여러 곳에 심지 않고
/// Resources 폴더 경유로 한 곳에서 불러오기 위한 참조 자산이다. <c>BattleLegacyChestPrefabReference</c>/
/// <c>BattleLegacyStorePrefabReference</c>와 같은 패턴(ScriptableObject + Resources.Load 정적 접근자) —
/// 사실상 이 프로젝트 전역에서 하나만 존재하는 "리소스 폴더 기반 싱글턴"으로 봐도 된다.
/// </summary>
[CreateAssetMenu(
    fileName = "BattleEquipmentDatabaseReference",
    menuName = "Renew/Battle/Equipment Database Reference")]
public sealed class BattleEquipmentDatabaseReference : ScriptableObject
{
    private const string ResourcePath = "Battle/Shop/BattleEquipmentDatabaseReference";

    [SerializeField] private EquipDatabase database;
    /// <summary>이 자산이 가리키는 원본 장비 데이터베이스.</summary>
    public EquipDatabase Database => database;

    /// <summary>Resources 폴더에서 이 참조 자산 하나를 불러온다.</summary>
    public static BattleEquipmentDatabaseReference Load() =>
        Resources.Load<BattleEquipmentDatabaseReference>(ResourcePath);
}'''
    ),
])

p2 = "Assets/renew/Battle/Shop/BattleLegacyStorePrefabReference.cs"
apply(p2, [
    (
'''using UnityEngine;

/// <summary>Battle에서 원본 Store 프리팹을 직접 수정하지 않고 불러오기 위한 참조 자산이다.</summary>
[CreateAssetMenu(fileName = "BattleLegacyStorePrefabReference", menuName = "Renew/Battle/Legacy Store Reference")]
public sealed class BattleLegacyStorePrefabReference : ScriptableObject
{
    private const string ResourcePath = "Battle/Shop/BattleLegacyStorePrefabReference";
    [SerializeField] private GameObject prefab;
    public GameObject Prefab => prefab;
    public static BattleLegacyStorePrefabReference Load() =>
        Resources.Load<BattleLegacyStorePrefabReference>(ResourcePath);
}''',
'''using UnityEngine;

/// <summary>
/// Battle에서 원본 Store 프리팹을 직접 수정하지 않고 불러오기 위한 참조 자산이다.
/// 실제로 이 자산이 가리키는 대상은 <c>Assets/renew/Battle/Event_Store.prefab</c>이고
/// (Assets/Game 쪽 원본 Store.prefab이 아니다 — BattleLegacyStoreViewAdapter 리뷰에서 guid로 확인),
/// <c>BattleLegacyChestPrefabReference</c>/<c>BattleEquipmentDatabaseReference</c>와 같은 패턴
/// (ScriptableObject + Resources.Load 정적 접근자)인 "리소스 폴더 기반 싱글턴" 자산이다.
/// </summary>
[CreateAssetMenu(fileName = "BattleLegacyStorePrefabReference", menuName = "Renew/Battle/Legacy Store Reference")]
public sealed class BattleLegacyStorePrefabReference : ScriptableObject
{
    private const string ResourcePath = "Battle/Shop/BattleLegacyStorePrefabReference";
    [SerializeField] private GameObject prefab;
    /// <summary>이 자산이 가리키는 레거시 상점 프리팹(Event_Store.prefab).</summary>
    public GameObject Prefab => prefab;
    /// <summary>Resources 폴더에서 이 참조 자산 하나를 불러온다.</summary>
    public static BattleLegacyStorePrefabReference Load() =>
        Resources.Load<BattleLegacyStorePrefabReference>(ResourcePath);
}'''
    ),
])
