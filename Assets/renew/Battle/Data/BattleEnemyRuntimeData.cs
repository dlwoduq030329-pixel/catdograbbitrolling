using UnityEngine;

/// <summary>
/// 생성된 Enemy 인스턴스가 자신을 만든 원본 DB 데이터(BattleEnemyData)를 참조하도록 보관하는 핵심 브릿지다.
/// EnemySpawner가 스폰 시점에 Initialize()로 한 번 채운 뒤, BattleCardEffectPipeline(rank 보호 판정),
/// BattleCardMovementService(pushWeight), EnemyTurnActor(attackDamage·moveMPCostPerTile·basicAttackMPCost),
/// BattlePlayerRangeController·BattleMoveThreatPreview(이동 범위·위협 프리뷰)
/// 등 여러 파일이 GetComponent 후 Data를 직접 읽는다. 파일 크기는 작지만 참조 범위가 넓어
/// 변경 시 회귀 확인 범위가 크다(2026-08-21 2차 검증 확인).
/// </summary>
public class BattleEnemyRuntimeData : MonoBehaviour
{
    /// <summary>스폰 시 Initialize()로 채워지는 이 Enemy의 정적 스펙. 미초기화 상태(null)일 수 있으므로 호출부는 null 검사 후 사용한다.</summary>
    public BattleEnemyData Data { get; private set; }

    /// <summary>Spawner가 선택한 데이터를 런타임 Enemy에 연결하고 표시 이름을 적용한다.</summary>
    public void Initialize(BattleEnemyData data)
    {
        Data = data;

        if (data != null && !string.IsNullOrWhiteSpace(data.displayName))
        {
            gameObject.name = data.displayName;
        }
    }
}
