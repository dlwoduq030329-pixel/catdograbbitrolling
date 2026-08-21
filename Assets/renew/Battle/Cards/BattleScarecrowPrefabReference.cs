using UnityEngine;

/// <summary>
/// Resources가 레거시 허수아비 Prefab을 간접 로드하기 위해 만든 참조 전용 에셋이다.
/// 완성된 Battle 허수아비 Prefab을 소환 코드에 직접 연결하면 필요 없으므로 삭제한다.
/// </summary>
public sealed class BattleScarecrowPrefabReference : ScriptableObject
{
    [SerializeField] private GameObject prefab;
    /// <summary>BattleScarecrowBridge가 실제 생성할 레거시 외형 Prefab.</summary>
    public GameObject Prefab => prefab;
}
