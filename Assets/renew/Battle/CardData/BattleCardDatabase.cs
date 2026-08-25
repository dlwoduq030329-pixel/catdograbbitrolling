using System.Collections.Generic;
using UnityEngine;

/// <summary>전투 카드의 기본 용도 분류입니다.</summary>
public enum BattleCardCategory
{
    [InspectorName("공격")]
    Attack,
    [InspectorName("방어")]
    Defense,
    [InspectorName("지원")]
    Support,
    [InspectorName("이동")]
    Movement,
    [InspectorName("기타")]
    Utility
}

/// <summary>카드가 처리하는 주요 효과 유형입니다.</summary>
public enum BattleCardType
{
    [InspectorName("물리 피해")]
    PhysicalDamage,
    [InspectorName("마법 피해")]
    MagicDamage,
    [InspectorName("지원")]
    Support
}

/// <summary>카드를 사용할 수 있는 대상 종류입니다.</summary>
public enum BattleCardTargetType
{
    [InspectorName("대상 없음")]
    None,
    [InspectorName("자신")]
    Self,
    [InspectorName("적")]
    Enemy,
    [InspectorName("아군")]
    Ally,
    [InspectorName("모든 캐릭터")]
    Character,
    [InspectorName("타일")]
    Tile,
    [InspectorName("모든 적")]
    AllEnemies
}

/// <summary>카드 대상이 정해지는 방식을 효과 종류와 분리해 지정한다.</summary>
public enum BattleCardTargetSelectionMode
{
    [InspectorName("플레이어가 직접 선택")]
    Manual,
    [InspectorName("사거리 내 최저 HP 적 자동 선택")]
    LowestHealthEnemyInRange
}

/// <summary>카드 효과가 적용되는 범위 형태입니다.</summary>
public enum BattleCardAreaType
{
    [InspectorName("단일 대상")]
    Single,
    [InspectorName("십자 범위")]
    Cross,
    [InspectorName("원형 범위")]
    Radius,
    [InspectorName("직선 범위")]
    Line
}

/// <summary>카드 VFX Prefab을 생성할 기준 위치입니다.</summary>
public enum BattleCardVfxSpawnPosition
{
    [InspectorName("플레이어 위치")]
    Player,
    [InspectorName("선택한 대상 위치")]
    SelectedTarget,
    [InspectorName("선택한 타일 위치")]
    SelectedTile
}

/// <summary>
/// 카드 한 장의 애니메이션과 VFX 연결 정보를 Inspector에서 직접 편집하는 데이터입니다.
/// 현재 BattleCardVfxRegistry를 즉시 제거하지 않고 병행하기 위한 새 원본 구조이며,
/// Presentation Bridge 전환 전까지 이 값이 비어 있어도 기존 Registry 경로는 그대로 동작합니다.
/// </summary>
[System.Serializable]
public sealed class BattleCardPresentationData
{
    [Tooltip("Animator에서 재생할 상태 이름입니다. 현재 CardCodes 문자열 배열을 대체할 값입니다.")]
    [InspectorName("애니메이션 상태 이름")]
    public string animationStateName;

    [Tooltip("카드 사용 후 생성할 VFX Prefab입니다. 연출이 없는 카드는 비워둡니다.")]
    [InspectorName("VFX 프리팹")]
    public GameObject vfxPrefab;

    [Tooltip("VFX를 플레이어, 선택 대상, 선택 타일 중 어느 위치에 생성할지 결정합니다.")]
    [InspectorName("VFX 생성 위치")]
    public BattleCardVfxSpawnPosition vfxSpawnPosition =
        BattleCardVfxSpawnPosition.SelectedTarget;

    [Tooltip("생성된 일회성 VFX를 제거하기까지의 시간입니다. 지속 영역 수명은 해당 영역 컴포넌트가 별도로 관리합니다.")]
    [InspectorName("VFX 유지 시간(초)")]
    [Min(0f)] public float vfxLifetimeSeconds = 2.5f;

    [Tooltip("이전 VFX Prefab 내부의 피해·상태 스크립트가 중복 실행되지 않도록 MonoBehaviour를 끌지 결정합니다.")]
    [InspectorName("VFX 내부 실행 스크립트 비활성화")]
    public bool disableRuntimeBehaviours = true;

    [Tooltip("비활성 자식에 시각 파티클이 들어 있는 이전 VFX는 생성 직후 모든 자식을 활성화합니다.")]
    [InspectorName("VFX 자식 오브젝트 모두 활성화")]
    public bool activateAllVfxChildren;
}

/// <summary>
/// 전투에서 사용하는 카드 한 장의 정적 설정입니다.
/// 사용 중인 대상이나 실행 상태처럼 변하는 값은 저장하지 않습니다.
/// </summary>
[System.Serializable]
public class BattleCardData
{
    [Header("전투 분류")]
    [InspectorName("카드 분류")]
    public BattleCardCategory category = BattleCardCategory.Attack;
    [InspectorName("카드 유형")]
    public BattleCardType cardType = BattleCardType.PhysicalDamage;

    [Header("원본 카드 데이터 연결")]
    [Tooltip("원본 카드 데이터 목록에서 사용하는 번호입니다. 연결하지 않을 경우 -1을 사용합니다.")]
    [InspectorName("원본 카드 번호")]
    [Min(-1)] public int legacyCardIndex = -1;

    [Header("대상과 사거리")]
    [InspectorName("대상 종류")]
    public BattleCardTargetType targetType = BattleCardTargetType.Enemy;
    [Tooltip("Teleport 같은 효과 종류와 무관하게, 실제 대상을 플레이어가 고를지 시스템이 자동 선택할지 지정합니다.")]
    [InspectorName("대상 선택 방식")]
    public BattleCardTargetSelectionMode targetSelectionMode = BattleCardTargetSelectionMode.Manual;
    [InspectorName("사용 사거리(칸)")]
    [Min(0)] public int rangeTiles = 1;
    [InspectorName("범위 형태")]
    public BattleCardAreaType areaType = BattleCardAreaType.Single;
    [Tooltip("단일 대상은 0을 사용합니다. 십자·원형·직선 범위는 효과가 퍼지는 칸 수를 입력합니다.")]
    [InspectorName("효과 범위 크기(칸)")]
    [Min(0)] public int areaSizeTiles;

    [Header("추가 효과")]
    [InspectorName("보호막 수치")]
    [Min(0f)] public float shield;

    [InspectorName("효과 설명")]
    [TextArea] public string effectSummary;

    [InspectorName("효과 목록 (위에서부터 순서대로 실행)")]
    public List<BattleCardEffectData> effects = new List<BattleCardEffectData>();

    [Header("카드 연출 직접 연결")]
    [Tooltip("카드 코드 문자열과 별도 Registry 검색 없이 이 카드가 사용할 애니메이션/VFX를 직접 연결합니다.")]
    [InspectorName("카드 연출 데이터")]
    public BattleCardPresentationData presentation = new BattleCardPresentationData();
}

/// <summary>전투 카드의 정적 설정을 보관하는 데이터베이스입니다.</summary>
[CreateAssetMenu(fileName = "BattleCardDatabase", menuName = "Renew/전투/카드 데이터베이스")]
public class BattleCardDatabase : ScriptableObject
{
    [InspectorName("카드 데이터 목록")]
    [SerializeField] private List<BattleCardData> cards = new List<BattleCardData>();

    public int Count => cards.Count;
    public IReadOnlyList<BattleCardData> Cards => cards;

    /// <summary>목록 번호로 카드를 찾습니다. 범위를 벗어나면 null을 반환합니다.</summary>
    public BattleCardData GetAt(int index)
    {
        return index >= 0 && index < cards.Count ? cards[index] : null;
    }

    /// <summary>원본 카드 번호가 일치하는 첫 번째 전투 카드를 찾습니다.</summary>
    public BattleCardData FindByLegacyCardIndex(int legacyCardIndex)
    {
        return cards.Find(card => card != null && card.legacyCardIndex == legacyCardIndex);
    }
}
