using UnityEngine;

/// <summary>Battle에서 원본 Store 프리팹을 직접 수정하지 않고 불러오기 위한 참조 자산이다.</summary>
[CreateAssetMenu(fileName = "BattleLegacyStorePrefabReference", menuName = "Renew/Battle/Legacy Store Reference")]
public sealed class BattleLegacyStorePrefabReference : ScriptableObject
{
    private const string ResourcePath = "Battle/Shop/BattleLegacyStorePrefabReference";
    [SerializeField] private GameObject prefab;
    public GameObject Prefab => prefab;
    public static BattleLegacyStorePrefabReference Load() =>
        Resources.Load<BattleLegacyStorePrefabReference>(ResourcePath);
}
