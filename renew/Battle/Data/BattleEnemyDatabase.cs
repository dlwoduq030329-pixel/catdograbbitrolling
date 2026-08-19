using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Enemy의 전투 등급 분류.</summary>
public enum BattleEnemyRank
{
    [InspectorName("토템")]
    Totem,
    [InspectorName("일반")]
    Normal,
    [InspectorName("정예")]
    Elite,
    [InspectorName("보스")]
    Boss
}

/// <summary>기본 공격 방식 분류.</summary>
public enum BattleEnemyAttackType
{
    [InspectorName("근거리")]
    Melee,
    [InspectorName("원거리")]
    Ranged
}

/// <summary>플레이어를 감지하지 못했을 때 수행할 기본 행동.</summary>
public enum EnemyIdleBehavior
{
    [InspectorName("제자리 대기")]
    Stationary,
    [InspectorName("주변 배회")]
    Wander
}

/// <summary>공용 BT 노드의 우선순위를 결정할 AI 성향.</summary>
public enum EnemyAIProfileType
{
    [InspectorName("공격형")]
    Aggressive,
    [InspectorName("방어형")]
    Defensive,
    [InspectorName("지원형")]
    Support,
    [InspectorName("보스형")]
    Boss
}

/// <summary>
/// Enemy 한 종류의 프리팹, 전투 수치, 고정 MP, AI 설정을 묶은 직렬화 데이터다.
/// 런타임 상태는 저장하지 않으며 BattleEnemyDatabase 에셋 안에서 편집한다.
/// </summary>
[System.Serializable]
public class BattleEnemyData
{
    [Header("식별 정보 및 프리팹")]
    [InspectorName("고유 식별자")]
    public string id;
    [InspectorName("표시 이름")]
    public string displayName;
    [InspectorName("적 프리팹")]
    public GameObject prefab;
    [InspectorName("등급")]
    public BattleEnemyRank rank = BattleEnemyRank.Normal;
    [InspectorName("공격 방식")]
    public BattleEnemyAttackType attackType = BattleEnemyAttackType.Melee;

    [Header("전투 능력치")]
    [InspectorName("최대 체력")]
    [Min(1f)] public float maxHP = 10f;
    [InspectorName("공격력")]
    [Min(0f)] public float attackDamage = 1f;
    [InspectorName("공격 사거리 (칸)")]
    [Min(1)] public int attackRangeTiles = 1;
    [InspectorName("공격 속도")]
    [Min(0.01f)] public float attackSpeed = 1f;
    [Tooltip("밀기 힘이 이 값 이상일 때만 Enemy가 밀립니다. 일반 Enemy의 기본 무게는 1입니다.")]
    [InspectorName("밀기 무게")]
    [Min(1)] public int pushWeight = 1;
    [InspectorName("스킬 재사용 대기시간")]
    [Min(0f)] public float skillCooldown;

    [Header("적 고정 행동력")]
    [InspectorName("최대 행동력")]
    [Range(0, 10)] public int minTurnMP = 3;
    [InspectorName("Maximum turn MP")]
    [FormerlySerializedAs("maxMP")]
    [Range(0, 10)] public int maxTurnMP = 6;
    [InspectorName("이동 1칸 행동력 비용")]
    [Min(1)] public int moveMPCostPerTile = 1;
    [InspectorName("기본 공격 행동력 비용")]
    [Min(0)] public int basicAttackMPCost = 1;

    [Header("인공지능 설정")]
    [InspectorName("인공지능 성향")]
    public EnemyAIProfileType aiProfile = EnemyAIProfileType.Aggressive;
    [InspectorName("플레이어 감지 거리")]
    [Min(0f)] public float detectRange = 5f;
    [InspectorName("주변 경보 거리")]
    [Min(0f)] public float alertRange = 6f;
    [InspectorName("비감지 상태 행동")]
    public EnemyIdleBehavior idleBehavior = EnemyIdleBehavior.Stationary;
    [InspectorName("배회 반경 (칸)")]
    [Min(0)] public int wanderRadiusTiles = 3;
    [InspectorName("턴당 배회 칸 수")]
    [Min(1)] public int wanderTilesPerTurn = 1;
    [InspectorName("배회 확률")]
    [Range(0f, 1f)] public float wanderChance = 0.6f;
}

/// <summary>renew 전투에서 사용하는 Enemy 데이터 목록 에셋.</summary>
[CreateAssetMenu(fileName = "BattleEnemyDatabase", menuName = "Renew/전투/적 데이터베이스")]
public class BattleEnemyDatabase : ScriptableObject
{
    [InspectorName("적 데이터 목록")]
    [SerializeField] private List<BattleEnemyData> enemies = new List<BattleEnemyData>();

    public int Count => enemies.Count;

    private void OnValidate()
    {
        foreach (BattleEnemyData enemy in enemies)
        {
            if (enemy == null) continue;
            enemy.minTurnMP = Mathf.Clamp(enemy.minTurnMP, 0, 10);
            enemy.maxTurnMP = Mathf.Clamp(enemy.maxTurnMP, enemy.minTurnMP, 10);
            enemy.moveMPCostPerTile = Mathf.Max(1, enemy.moveMPCostPerTile);
            enemy.basicAttackMPCost = Mathf.Max(0, enemy.basicAttackMPCost);
        }
    }

    /// <summary>인덱스가 유효하면 데이터를 반환하고, 범위를 벗어나면 null을 반환한다.</summary>
    public BattleEnemyData GetAt(int index)
    {
        return index >= 0 && index < enemies.Count ? enemies[index] : null;
    }

    /// <summary>목록에서 임의의 Enemy 데이터를 하나 반환한다.</summary>
    public BattleEnemyData GetRandom()
    {
        return enemies.Count > 0
            ? enemies[UnityEngine.Random.Range(0, enemies.Count)]
            : null;
    }

    /// <summary>고유 ID가 일치하는 첫 데이터를 반환한다.</summary>
    public BattleEnemyData FindById(string id)
    {
        return enemies.Find(enemy => enemy != null && enemy.id == id);
    }
}
