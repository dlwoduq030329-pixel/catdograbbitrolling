using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Enemy의 전투 등급 분류. BattleCardEffectPipeline이 Elite·Boss만 상태이상 저항(보호 등급)으로 특별 취급한다.
/// Totem은 현재 어떤 코드에서도 분기하지 않으며 실제 데이터 에셋에도 의도적으로 배정된 사례가 없는 미사용 값이다.
/// </summary>
public enum BattleEnemyRank
{
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
/// 스폰될 유닛이 Enemy(전투 대상)인지, 싸우지 않는 NPC인지, 나중에 추가될 용병(Mercenary)인지 구분한다.
/// 2026-09-05: NPC/Mercenary 파이프라인 설계 논의에서 나온 값만 미리 준비해 둔 것이다 — 이 enum과
/// 아래 BattleEnemyData.spawnRole 필드, EnemyTurnActor.ConfigureFromData의 복사 지점까지만 여기서
/// 만들어 두고, 실제로 NPC/Mercenary가 이 값을 읽어서 다르게 동작하는 코드(예: EnemyTurnActor.Targeting.cs의
/// ResolveTarget이 NPC일 때 null을 반환하게 하는 것 등)는 아직 구현하지 않았다 — 그건 다음 작업으로 남겨둔다.
/// </summary>
public enum SpawnRole
{
    [InspectorName("적 (Enemy)")]
    Enemy,
    [InspectorName("NPC")]
    NPC,
    [InspectorName("용병 (Mercenary)")]
    Mercenary
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
    [Tooltip("적 종류를 구분하는 이름입니다. 현재 스폰 선택은 ID가 아니라 목록 인덱스 또는 무작위 추첨을 사용합니다.")]
    public string id;
    [InspectorName("표시 이름")]
    [Tooltip("적 종류의 표시용 이름입니다. 전투 능력치에는 영향을 주지 않습니다.")]
    public string displayName;
    [InspectorName("스폰 역할")]
    [Tooltip("Enemy/NPC/Mercenary 구분만 담는 값이다. 지금은 어떤 코드도 이 값을 읽어서 분기하지 않으며, " +
             "NPC·Mercenary 전용 동작은 이후 별도 작업에서 구현한다.")]
    public SpawnRole spawnRole = SpawnRole.Enemy;
    [InspectorName("적 프리팹")]
    [Tooltip("EnemySpawner가 생성할 프리팹입니다. 이 데이터의 체력·공격·감지 설정을 생성된 적에 적용합니다.")]
    public GameObject prefab;
    [InspectorName("등급")]
    [Tooltip("일반·정예·보스를 구분합니다. 현재 정예와 보스의 일부 상태이상 저항 판정에 사용하며, 스탯이나 스킬이 자동으로 추가되지는 않습니다.")]
    public BattleEnemyRank rank = BattleEnemyRank.Normal;
    [InspectorName("공격 방식")]
    [Tooltip("근거리·원거리 분류입니다. 실제 공격 거리(칸)는 아래 공격 사거리로 설정합니다.")]
    public BattleEnemyAttackType attackType = BattleEnemyAttackType.Melee;
    [InspectorName("피해 속성 (물리 / 마법)")]
    [Tooltip("기본 공격의 물리·마법 피해 판정과 위협 아이콘 구분에 사용합니다. 화상 같은 상태이상을 자동 부여하지 않습니다.")]
    public BattleDamageType attackDamageType = BattleDamageType.Physical;

    [Header("전투 능력치")]
    [InspectorName("최대 체력")]
    [Tooltip("스폰 시 적용할 최대 HP입니다. 최소 1이며 현재 HP는 생성된 유닛이 따로 관리합니다.")]
    [Min(1f)] public float maxHP = 10f;
    [InspectorName("공격력")]
    [Tooltip("기본 공격의 기준 피해량입니다. 실제 피해는 피해 처리기의 상태이상 등 보정을 거칩니다.")]
    [Min(0f)] public float attackDamage = 1f;
    [InspectorName("공격 사거리 (칸)")]
    [Tooltip("기본 공격 가능 거리를 타일 칸 수로 설정합니다. 1은 인접 공격이며 감지 거리와는 별개입니다.")]
    [Min(1)] public int attackRangeTiles = 1;
    [Tooltip("밀기 힘이 이 값 이상일 때만 Enemy가 밀립니다. 일반 Enemy의 기본 무게는 1입니다.")]
    [InspectorName("밀기 무게")]
    [Min(1)] public int pushWeight = 1;
    [InspectorName("스킬 재사용 대기시간 (현재 미사용)")]
    [Tooltip("현재 미연결인 예약 필드입니다. 초·턴 단위 및 스킬 실행 처리가 정의되지 않았으므로 지금은 값을 바꿔도 효과가 없습니다.")]
    [Min(0f)] public float skillCooldown;

    [Header("턴 MP 추첨 범위 및 행동 비용")]
    [InspectorName("턴 MP 최솟값")]
    [Tooltip("다음 적 턴 MP 추첨의 최솟값입니다. 최댓값과 같게 설정하면 고정 MP가 됩니다. MP는 매번 새 값으로 설정하며 누적되지 않습니다.")]
    [Range(0, 10)] public int minTurnMP = 3;
    [InspectorName("턴 MP 최댓값")]
    [Tooltip("다음 적 턴 MP 추첨의 최댓값(포함)이며, 스폰 시 MP 상한에도 사용합니다. 최솟값 이상이어야 합니다.")]
    [FormerlySerializedAs("maxMP")]
    [Range(0, 10)] public int maxTurnMP = 6;
    [InspectorName("이동 1칸 행동력 비용")]
    [Tooltip("타일 한 칸을 이동할 때 소비할 기본 MP입니다. 최소 1이며 상태이상으로 실제 비용이 달라질 수 있습니다.")]
    [Min(1)] public int moveMPCostPerTile = 1;
    [InspectorName("기본 공격 행동력 비용")]
    [Tooltip("기본 공격 1회에 필요한 기본 MP입니다. 상태이상 보정 후 적용하며 현재 적은 기본 공격 성공 후 턴을 종료합니다.")]
    [Min(0)] public int basicAttackMPCost = 1;

    [Header("인공지능 설정")]
    [InspectorName("인공지능 성향 (현재 미사용)")]
    [Tooltip("현재 미연결입니다. 성향을 바꿔도 행동 우선순위나 보스 패턴이 달라지지 않습니다.")]
    public EnemyAIProfileType aiProfile = EnemyAIProfileType.Aggressive;
    [InspectorName("플레이어 감지 거리")]
    [Tooltip("높이를 제외한 XZ 평면의 월드 거리입니다. 타일 칸 수가 아닙니다. EnemyDetector가 직접 감지 여부를 판정합니다.")]
    [Min(0f)] public float detectRange = 5f;
    [InspectorName("주변 경보 거리")]
    [Tooltip("주변 적에게 대상 정보를 알리는 경보 범위입니다. 직접 플레이어 감지 거리와 구분합니다.")]
    [Min(0f)] public float alertRange = 6f;
    [InspectorName("비감지 상태 행동")]
    [Tooltip("추적할 대상이 없을 때 제자리 대기 또는 주변 배회를 선택합니다. 아래 배회 설정은 주변 배회일 때 사용합니다.")]
    public EnemyIdleBehavior idleBehavior = EnemyIdleBehavior.Stationary;
    [InspectorName("배회 반경 (칸)")]
    [Tooltip("첫 배회 시도의 타일을 기준으로 허용하는 범위입니다. 플레이어 추격 거리의 제한은 아닙니다.")]
    [Min(0)] public int wanderRadiusTiles = 3;
    [InspectorName("턴당 배회 칸 수")]
    [Tooltip("비감지 상태에서 한 턴에 시도할 최대 이동 칸 수입니다. MP 부족이나 이동 가능한 타일 부재 시 먼저 멈춥니다.")]
    [Min(1)] public int wanderTilesPerTurn = 1;
    [InspectorName("배회 확률")]
    [Tooltip("비감지 상태의 턴마다 배회를 시도할 확률입니다. 0~1 범위이며 0.6은 60%, 1은 항상 시도입니다.")]
    [Range(0f, 1f)] public float wanderChance = 0.6f;
}

/// <summary>renew 전투에서 사용하는 Enemy 데이터 목록 에셋.</summary>
[CreateAssetMenu(fileName = "BattleEnemyDatabase", menuName = "Renew/전투/적 데이터베이스")]
public class BattleEnemyDatabase : ScriptableObject
{
    [InspectorName("적 데이터 목록")]
    [Tooltip("적 종류별 원본 설정입니다. 무작위 스폰은 등급에 관계없이 이 목록 전체에서 선택합니다. 현재 HP·MP·쿨타임 같은 개체별 상태는 여기에 저장하지 않습니다.")]
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
