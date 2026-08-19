using UnityEngine;

/// <summary>
/// BattleCardDrawSystem 컴포넌트를 확보하고 전투·원본 카드 데이터베이스를 연결한다.
/// 덱 초기화, 드로우와 카드 사용은 담당하지 않는다.
/// </summary>
public static class BattleCardDrawSystemFactory
{
    /// <summary>지정한 오브젝트의 카드 드로우 시스템을 생성 또는 재사용하고 데이터베이스를 구성한다.</summary>
    public static BattleCardDrawSystem CreateOrConfigure(
        GameObject owner,
        BattleCardDatabase battleDatabase,
        CardDatabase originalDatabase)
    {
        if (owner == null)
        {
            return null;
        }

        BattleCardDrawSystem drawSystem = owner.GetComponent<BattleCardDrawSystem>();
        if (drawSystem == null)
        {
            drawSystem = owner.AddComponent<BattleCardDrawSystem>();
        }

        drawSystem.ConfigureDatabase(battleDatabase);
        drawSystem.ConfigureOriginalDatabase(originalDatabase);
        return drawSystem;
    }
}
