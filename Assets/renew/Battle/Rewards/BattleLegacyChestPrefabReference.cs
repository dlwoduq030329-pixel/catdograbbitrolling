using UnityEngine;

/// <summary>Battle에서 레거시 상자 외형을 원본 수정 없이 불러오기 위한 참조 자산이다.</summary>
[CreateAssetMenu(fileName = "BattleLegacyChestPrefabReference", menuName = "Renew/Battle/Legacy Chest Reference")]
public sealed class BattleLegacyChestPrefabReference : ScriptableObject
{
    private const string ResourcePath = "Battle/Rewards/BattleLegacyChestPrefabReference";
    [SerializeField] private GameObject prefab;
    public GameObject Prefab => prefab;

    public static BattleLegacyChestPrefabReference Load() =>
        Resources.Load<BattleLegacyChestPrefabReference>(ResourcePath);
}
