using UnityEngine;

/// <summary>
/// 전투에 등록되는 Player의 필수 런타임 데이터 컴포넌트를 구성한다.
/// Player 등록, UI 연결과 턴 초기화는 담당하지 않는다.
/// (2026-08-22 개명: BattlePlayerRuntimeDataFactory -> BattlePlayerCombatDataFactory — 이름이 같은
/// BattlePlayerRegistrationService/BattlePlayerRuntimeBinder와 헷갈린다는 리뷰 지적으로, "전투 데이터
/// 컴포넌트(BattleUnitMP/PlayerCombatData/BattleHealth)를 만든다"는 역할이 이름에서 바로 드러나도록 바꿨다.)
/// </summary>
public static class BattlePlayerCombatDataFactory
{
    /// <summary>디버그 단계에서 레거시 HP 값을 찾지 못했을 때 사용하는 기본 최대 체력이다.</summary>
    private const float DefaultMaxHealth = 15f;

    /// <summary>BattleUnitMP, PlayerCombatData, BattleHealth를 보장하고 현재 Player 데이터로 초기화한다.</summary>
    public static bool TryCreate(
        GameObject player,
        out BattleUnitMP characterMP,
        out PlayerCombatData combatData,
        out BattleHealth battleHealth)
    {
        characterMP = null;
        combatData = null;
        battleHealth = null;
        if (player == null)
        {
            return false;
        }

        characterMP = player.GetComponent<BattleUnitMP>();
        if (characterMP == null)
        {
            characterMP = player.AddComponent<BattleUnitMP>();
        }

        combatData = player.GetComponent<PlayerCombatData>();
        if (combatData == null)
        {
            combatData = player.AddComponent<PlayerCombatData>();
        }

        // 디버그 단계: BattleHealth를 보장하고 기본 최대 체력으로 초기화한다.
        // 실제 스폰 프리팹에는 레거시 BattlePlayer 컴포넌트가 없어(Ch_*_Battle류 별도 프리팹에만 존재)
        // 캐릭터별 값을 가져올 수 없으므로 항상 기본값을 사용한다.
        battleHealth = player.GetComponent<BattleHealth>();
        if (battleHealth == null)
        {
            battleHealth = player.AddComponent<BattleHealth>();
        }

        battleHealth.Initialize(DefaultMaxHealth);

        return true;
    }
}
