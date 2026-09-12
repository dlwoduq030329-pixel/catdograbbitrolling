using UnityEngine;

/// <summary>
/// 전투에 등록되는 Player의 필수 런타임 데이터 컴포넌트를 구성한다.
/// Player 등록, UI 연결과 턴 초기화는 담당하지 않는다.
/// (2026-08-22 개명: BattlePlayerRuntimeDataFactory -> BattlePlayerCombatDataFactory — 이름이 같은
/// BattlePlayerRegistrationService/BattlePlayerRuntimeBinder와 헷갈린다는 리뷰 지적으로, "전투 데이터
/// 컴포넌트(BattleUnitMP/PlayerCombatData/BattleHealth)를 만든다"는 역할이 이름에서 바로 드러나도록 바꿨다.)
/// (2026-09-10: BattleUnitAP 추가 — 이동 전용 자원을 BattleUnitMP(카드·기본공격용)와 분리하면서,
/// Player 프리팹을 직접 열어 컴포넌트를 붙이지 않아도 되도록 여기서 같이 보장한다.)
/// </summary>
public static class BattlePlayerCombatDataFactory
{
    /// <summary>디버그 단계에서 레거시 HP 값을 찾지 못했을 때 사용하는 기본 최대 체력이다.</summary>
    private const float DefaultMaxHealth = 15f;

    /// <summary>BattleUnitMP, BattleUnitAP, PlayerCombatData, BattleHealth를 보장하고 현재 Player 데이터로 초기화한다.</summary>
    public static bool TryCreate(
        GameObject player,
        out BattleUnitMP characterMP,
        out BattleUnitAP characterAP,
        out PlayerCombatData combatData,
        out BattleHealth battleHealth)
    {
        characterMP = null;
        characterAP = null;
        combatData = null;
        battleHealth = null;
        if (player == null)
        {
            return false;
        }

        // 4개 전부 Player Body(player) 오브젝트에 미리 붙어 있어야 한다. 누락돼도 조용히 새로 만들지
        // 않고 LogError로 알리고 등록을 중단한다(전에는 여기서 AddComponent로 숨겼었다).
        characterMP = player.GetComponent<BattleUnitMP>();
        if (characterMP == null)
        {
            Debug.LogError($"[{nameof(BattlePlayerCombatDataFactory)}] {player.name}에 BattleUnitMP가 없습니다. Scene에 미리 추가해야 합니다.", player);
            return false;
        }

        characterAP = player.GetComponent<BattleUnitAP>();
        if (characterAP == null)
        {
            Debug.LogError($"[{nameof(BattlePlayerCombatDataFactory)}] {player.name}에 BattleUnitAP가 없습니다. Scene에 미리 추가해야 합니다.", player);
            return false;
        }

        combatData = player.GetComponent<PlayerCombatData>();
        if (combatData == null)
        {
            Debug.LogError($"[{nameof(BattlePlayerCombatDataFactory)}] {player.name}에 PlayerCombatData가 없습니다. Scene에 미리 추가해야 합니다.", player);
            return false;
        }

        // 디버그 단계: 기본 최대 체력으로 초기화한다.
        // 실제 스폰 프리팹에는 레거시 BattlePlayer 컴포넌트가 없어(Ch_*_Battle류 별도 프리팹에만 존재)
        // 캐릭터별 값을 가져올 수 없으므로 항상 기본값을 사용한다.
        battleHealth = player.GetComponent<BattleHealth>();
        if (battleHealth == null)
        {
            Debug.LogError($"[{nameof(BattlePlayerCombatDataFactory)}] {player.name}에 BattleHealth가 없습니다. Scene에 미리 추가해야 합니다.", player);
            return false;
        }

        battleHealth.Initialize(DefaultMaxHealth);

        return true;
    }
}
