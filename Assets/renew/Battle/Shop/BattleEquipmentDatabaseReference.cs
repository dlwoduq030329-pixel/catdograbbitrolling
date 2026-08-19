using UnityEngine;

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
}
