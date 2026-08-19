using UnityEngine;

/// <summary>Resources에서 레거시 프리팹 참조만 안전하게 가져오는 브릿지 에셋.</summary>
public sealed class BattleScarecrowPrefabReference : ScriptableObject
{
    [SerializeField] private GameObject prefab;
    public GameObject Prefab => prefab;
}
