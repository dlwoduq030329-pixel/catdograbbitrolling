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
