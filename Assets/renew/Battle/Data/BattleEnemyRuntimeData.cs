using UnityEngine;

/// <summary>
/// 생성된 Enemy 인스턴스가 자신을 만든 원본 DB 데이터를 참조하도록 보관한다.
/// AI와 행동 시스템은 이 컴포넌트를 통해 이동 비용 등 종류별 설정을 읽는다.
/// </summary>
public class BattleEnemyRuntimeData : MonoBehaviour
{
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
