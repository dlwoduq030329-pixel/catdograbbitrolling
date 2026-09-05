using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Enemy의 전투 등급 분류. BattleCardEffectPipeline이 Elite·Boss만 상태이상 저항(보호 등급)으로 특별 취급한다.
/// Totem은 현재 어떤 코드에서도 분기하지 않으며 실제 데이터 에셋에도 의도적으로 배정된 사례가 없는 미사용 값이다.
/// </summary>
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

/// <summary>
/// 플레이어를 감지하지 못했을 때 수행할 기본 행동.
/// idleBehavior와 아래 wanderRadiusTiles·wanderTilesPerTurn·wanderChance는 EnemyTurnActor.TryWanderStep이
/// 읽어서 실제로 동작한다(2026-09-04 구현). Wander면 매 턴 wanderChance 확률로 배회 여부를 굴리고,
/// 성공하면 최초 배회 시도 위치(스폰 지점 근사) 기준 wanderRadiusTiles 반경 안에서 매턴
/// wanderTilesPerTurn 칸까지 무작위로 인접 이동한다.
/// </summary>
public enum EnemyIdleBehavior
{
    [InspectorName("제자리 대기")]
    Stationary,
    [InspectorName("주변 배회")]
    Wander
}

/// <summary>
/// 공용 BT 노드의 우선순위를 결정할 AI 성향(설계 의도).
/// 현재는 BattleEnemyData.aiProfile에 값만 저장될 뿐 이를 읽어 분기하는 코드가 없어 실제로는 아무 효과가 없다.
/// AI 스킬 확장 시 연결하거나, 계획이 없으면 필드 자체를 제거한다.
/// </summary>
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
    [InspectorName("Attack Damage Type")]
    [Tooltip("Controls the enemy attack indicator color. Physical is red and Magic is blue.")]
    public BattleDamageType attackDamageType = BattleDamageType.Physical;

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

    /// <summary>
    /// 목록에서 등급·층 구분 없이 완전 무작위로 Enemy 데이터를 하나 반환한다. EnemySpawner가 databaseIndex를
    /// 지정하지 않았을 때 사용한다. 층별로 출현 등급을 조정하려면 이 무작위 방식으로는 제어할 수 없으므로
    /// 필터링 가능한 별도 API(GetRandomByRank 등) 추가를 검토한다.
    /// </summary>
    public BattleEnemyData GetRandom()
    {
        return enemies.Count > 0
            ? enemies[UnityEngine.Random.Range(0, enemies.Count)]
            : null;
    }

    // 2026-09-05: 여기 있던 FindById(string id)는 2026-08-21 기준으로도 이미 호출부가 없던 미사용
    // API라 정리하며 제거했다. id 기반 조회가 필요한 기능(예: 특정 보스 강제 스폰)이 생기면
    // `enemies.Find(enemy => enemy != null && enemy.id == id)` 한 줄로 다시 만들 수 있다(git 이력
    // 참고). 리뷰에서 읽은 파일 범위 안에서는 호출부를 찾지 못했지만, 전체 코드베이스를 grep한 것은
    // 아니므로 IDE의 "모든 참조 찾기"로 한 번 더 확인해보는 걸 권장한다.
}
