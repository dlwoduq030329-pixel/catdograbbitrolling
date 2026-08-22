using UnityEngine;

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
}
