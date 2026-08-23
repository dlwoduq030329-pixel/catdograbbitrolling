using UnityEngine;

/// <summary>
/// 전달받은 Player에 전투 런타임 참조를 연결한다.
/// 턴 진행과 Player 선택 시점은 결정하지 않는다.
/// </summary>
public static class BattlePlayerRegistrationService
{
    /// <summary>전투 데이터, MP UI, 카드 덱과 Player 행동 입력을 전달된 캐릭터에 연결한다.</summary>
    public static bool TryRegisterRuntime(
        GameObject player,
        PlayerMPUI playerMPUI,
        BattleCardDrawSystem cardDrawSystem,
        BattlePlayerActionController actionController,
        Object logContext,
        out BattleUnitMP playerMP,
        out PlayerCombatData combatData,
        out BattleHealth playerHealth)
    {
        playerMP = null;
        combatData = null;
        playerHealth = null;
        if (player == null)
        {
            Debug.LogError("플레이어 등록 실패: 전달된 플레이어가 null입니다.", logContext);
            return false;
        }

        if (!BattlePlayerCombatDataFactory.TryCreate(player, out playerMP, out combatData, out playerHealth))
        {
            Debug.LogError("플레이어 등록 실패: 전투 런타임 데이터를 구성하지 못했습니다.", logContext);
            return false;
        }

        playerMPUI?.BindPlayerMana(playerMP);
        playerMP.RestoreFull();
        PlayerDeck registeredDeck = player.GetComponentInParent<PlayerDeck>(true);
        cardDrawSystem?.InitializeBattleCardCycle(registeredDeck);
        actionController?.SetPlayer(player);
        return true;
    }
}
