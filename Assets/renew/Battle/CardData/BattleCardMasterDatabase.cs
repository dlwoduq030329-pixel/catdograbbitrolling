using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EffectPipeline이 카드 효과 한 단계를 어떤 규칙으로 실행할지 구분한다.
/// 이 enum과 EffectData는 실제 사용 중이지만 현재 유령 MasterDatabase 파일에 함께 있어 BattleCardDatabase로 이동할 예정이다.
/// </summary>
public enum BattleCardEffectType
{
    [InspectorName("피해")] Damage,
    [InspectorName("회복")] Heal,
    [InspectorName("보호막")] Shield,
    [InspectorName("상태이상 부여")] ApplyStatus,
    [InspectorName("능력치 변경")] ModifyStat,
    [InspectorName("밀치기")] Push,
    [InspectorName("당기기")] Pull,
    [InspectorName("돌진")] Dash,
    [InspectorName("순간이동")] Teleport,
    [InspectorName("소환")] Summon,
    [InspectorName("지속 영역 생성")] CreateArea,
    [InspectorName("처형")] Execute,
    [InspectorName("상태이상 제거")] Cleanse
}

/// <summary>
/// 카드가 선택받은 대상과 별개로, 효과 단계 하나가 실제로 적용될 대상 집합을 지정한다.
/// PreviousEffectTargets는 돌진 후 피해처럼 바로 앞 효과의 성공 대상을 이어받을 때 사용한다.
/// </summary>
public enum BattleCardEffectTarget
{
    [InspectorName("선택한 대상")] SelectedTarget,
    [InspectorName("사용자 자신")] Self,
    [InspectorName("카드 범위 안의 대상")] TargetsInArea,
    [InspectorName("앞 효과가 적용된 대상")] PreviousEffectTargets,
    [InspectorName("모든 적")] AllEnemies,
    [InspectorName("지정 타일")] SelectedTile
}

/// <summary>
/// 카드 한 장이 순서대로 실행할 효과 한 단계의 정적 설정이다.
/// 예를 들어 돌진→피해→밀치기 카드는 이 데이터 세 개를 목록 순서대로 보유한다.
/// 실행 중 대상과 계산 결과는 저장하지 않는다.
/// </summary>
[System.Serializable]
public sealed class BattleCardEffectData
{
    [Tooltip("실행할 효과의 기능을 선택합니다. 피해, 회복, 밀치기, 돌진, 상태이상 등이 있습니다.")]
    [InspectorName("효과 종류")]
    public BattleCardEffectType effectType;

    [Tooltip("이 효과를 누구 또는 어느 타일에 적용할지 선택합니다. 카드 자체의 선택 대상과 다를 수 있습니다.")]
    [InspectorName("효과 대상")]
    public BattleCardEffectTarget effectTarget;

    [Tooltip("효과의 핵심 수치입니다. 피해는 피해량, 회복은 회복량, 보호막은 보호막량, 능력치 변경은 증감량으로 사용합니다. 사용하지 않는 효과는 0으로 둡니다.")]
    [InspectorName("주요 수치 (피해·회복·보호막 등)")]
    public float amount;

    [Tooltip("처형 실패 피해처럼 주 효과와 별도로 필요한 보조 수치입니다.")]
    [InspectorName("보조 수치")]
    public float secondaryAmount;

    [Tooltip("밀치기, 당기기, 돌진, 순간이동처럼 위치가 변하는 효과의 최대 이동 칸 수입니다. 이동하지 않는 효과는 0으로 둡니다.")]
    [InspectorName("이동 거리(칸)")]
    [Min(0)] public int distanceTiles;

    [Tooltip("밀치기 효과의 힘입니다. 대상의 밀기 무게 이상이어야 밀 수 있습니다.")]
    [InspectorName("밀기 힘")]
    [Min(1)] public int pushForce = 1;

    [Tooltip("상태이상, 능력치 변경, 지속 영역이 유지되는 턴 수입니다. 즉시 끝나는 효과는 0으로 둡니다.")]
    [InspectorName("지속 턴")]
    [Min(0)] public int durationTurns;

    [Tooltip("같은 효과를 연속으로 적용하는 횟수입니다. 일반적인 단일 효과는 1을 사용합니다.")]
    [InspectorName("반복 횟수")]
    [Min(1)] public int repeatCount = 1; // 평타 연속 치는거

    [Tooltip("상태이상 이름, 변경할 능력치, 소환할 개체처럼 실행기가 추가로 구분해야 하는 값을 입력합니다. 예: 독, 화상, 공격속도증가, 허수아비")]
    [InspectorName("상태이상 또는 소환 코드")]
    public string effectCode;

    [Tooltip("기획자와 개발자가 효과의 실제 동작을 바로 이해할 수 있도록 완성된 문장으로 작성합니다.")]
    [InspectorName("효과 설명")]
    [TextArea] public string description;
    [Tooltip("이 효과를 실행할 수 없을 때 카드 사용 전체를 취소할지 결정합니다.")]
    [InspectorName("실패 시 카드 사용 취소")]
    public bool cancelCardOnFailure;
}

/// <summary>
/// 카드 설계표에서 확정한 카드 한 장의 전투 정보를 보관합니다.
/// 원본 데이터와 분리된 Battle 전용 기준 데이터입니다.
/// </summary>
[System.Serializable]
public sealed class BattleCardMasterData
{
    [Header("기본 식별 정보")]
    [InspectorName("카드 번호")]
    [Min(0)] public int cardIndex;
    [InspectorName("원본 카드 코드")]
    public string cardCode;
    [InspectorName("카드 이름")]
    public string cardName;

    [Header("분류와 대상")]
    [InspectorName("카드 분류")]
    public BattleCardCategory category;
    [InspectorName("카드 유형")]
    public BattleCardType cardType;
    [InspectorName("대상 종류")]
    public BattleCardTargetType targetType;
    [InspectorName("범위 형태")]
    public BattleCardAreaType areaType;

    [Header("전투 수치")]
    [InspectorName("사거리(칸)")]
    [Min(0)] public int rangeTiles;
    [InspectorName("MP 비용")]
    [Min(0)] public int mpCost;
    [InspectorName("기본 피해")]
    [Min(0f)] public float baseDamage;

    [Header("특수 효과 설계")]
    [Tooltip("카드가 최종적으로 무엇을 하는지 한 문장으로 요약합니다.")]
    [InspectorName("효과 설명")]
    [TextArea] public string effectSummary;
    [Tooltip("카드 사용 시 위에서부터 순서대로 처리할 효과 목록입니다. 예: 돌진 → 피해 → 밀치기")]
    [InspectorName("효과 목록 (위에서부터 순서대로 실행)")]
    public List<BattleCardEffectData> effects = new List<BattleCardEffectData>();
    [InspectorName("효과 식별자 목록")]
    public List<string> effectIds = new List<string>();
    [InspectorName("사용 실패 조건")]
    [TextArea] public string failureCondition;
    [InspectorName("구현 우선순위")]
    public string implementationPriority;
    [InspectorName("구현 상태")]
    public string implementationStatus;
    [InspectorName("담당자")]
    public string owner;
    [InspectorName("메모")]
    [TextArea] public string notes;
}

/// <summary>
/// Battle 카드의 기준 데이터베이스로 작성됐지만 현재 실행 코드에서 참조되지 않는 유령 데이터다.
/// 카드 번호는 설계표와 동일하게 0부터 27까지 유지합니다.
/// 실제 효과 enum과 EffectData를 BattleCardDatabase로 옮긴 뒤 이 데이터베이스와 FindByCardIndex를 삭제할 예정이다.
/// </summary>
[CreateAssetMenu(fileName = "BattleCardMasterDatabase", menuName = "Renew/전투/카드 기준 데이터베이스")]
public sealed class BattleCardMasterDatabase : ScriptableObject
{
    [InspectorName("카드 목록")]
    [SerializeField] private List<BattleCardMasterData> cards = new List<BattleCardMasterData>();

    public IReadOnlyList<BattleCardMasterData> Cards => cards;
    public int Count => cards.Count;

    /// <summary>설계표와 동일한 카드 번호로 카드를 찾습니다.</summary>
    public BattleCardMasterData FindByCardIndex(int cardIndex)
    {
        return cards.Find(card => card != null && card.cardIndex == cardIndex);
    }
}
