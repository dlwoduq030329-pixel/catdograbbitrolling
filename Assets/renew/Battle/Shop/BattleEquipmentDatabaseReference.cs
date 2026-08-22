using UnityEngine;

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
}
