using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 한 종류의 표시 이름과 프리팹만 담은 최소 데이터다. 전투 능력치가 없는 비전투 유닛이라
/// BattleEnemyData(체력·공격력·AI 설정 등)와 완전히 분리했다.
/// 실제 대사·퀘스트·상점 연결은 아직 구현하지 않았고, description은 그때까지 쓸 메모 자리다.
/// </summary>
[System.Serializable]
public class NpcData
{
    [Header("식별 정보 및 프리팹")]
    [InspectorName("고유 식별자")]
    [Tooltip("NPC 종류를 구분하는 이름입니다.")]
    public string id;
    [InspectorName("표시 이름")]
    [Tooltip("NPC의 표시용 이름입니다. 대사·퀘스트 UI 등에서 사용할 예정입니다.")]
    public string displayName;
    [InspectorName("NPC 프리팹")]
    [Tooltip("StageSpawner가 생성할 프리팹입니다. 비워두면 StageSpawner의 기본 임시 프리팹(또는 Cube)을 대신 사용합니다.")]
    public GameObject prefab;
    [InspectorName("설명 (미구현)")]
    [Tooltip("대사·퀘스트 기능을 붙이기 전까지 임시로 남겨두는 메모입니다. 실제 기능에는 아직 사용하지 않습니다.")]
    [TextArea]
    public string description;
}

/// <summary>renew 전투에서 사용하는 NPC 데이터 목록 에셋.</summary>
[CreateAssetMenu(fileName = "NpcDatabase", menuName = "Renew/전투/NPC 데이터베이스")]
public class NpcDatabase : ScriptableObject
{
    [InspectorName("NPC 데이터 목록")]
    [Tooltip("NPC 종류별 원본 설정입니다. 무작위 배치는 등급 구분 없이 이 목록 전체에서 선택합니다.")]
    [SerializeField] private List<NpcData> npcs = new List<NpcData>();

    public int Count => npcs.Count;

    /// <summary>인덱스가 유효하면 데이터를 반환하고, 범위를 벗어나면 null을 반환한다.</summary>
    public NpcData GetAt(int index)
    {
        return index >= 0 && index < npcs.Count ? npcs[index] : null;
    }

    /// <summary>목록에서 완전 무작위로 NPC 데이터를 하나 반환한다. 목록이 비어 있으면 null이다.</summary>
    public NpcData GetRandom()
    {
        return npcs.Count > 0
            ? npcs[UnityEngine.Random.Range(0, npcs.Count)]
            : null;
    }
}
