using UnityEngine;

/// <summary>
/// Player Body에서 생성된 캐릭터를 찾고 전투 런타임 참조를 연결한다.
/// 턴 진행과 Player 선택 시점은 결정하지 않는다.
/// </summary>
public static class BattlePlayerRegistrationService
{
    /// <summary>Player Body의 마지막 자식으로 생성된 선택 캐릭터를 반환한다.</summary>
    public static bool TryFindSpawnedPlayer(
        GameObject playerBody,
        Object logContext,
        out GameObject player)
    {
        player = null;
        if (playerBody == null)
        {
            Debug.LogError("플레이어 등록 실패: 플레이어 바디 참조가 없습니다.", logContext);
            return false;
        }

        Transform bodyTransform = playerBody.transform;
        if (bodyTransform.childCount == 0)
        {
            Debug.LogError("플레이어 등록 실패: 플레이어 바디 아래에 생성된 캐릭터가 없습니다.", playerBody);
            return false;
        }

        player = bodyTransform.GetChild(bodyTransform.childCount - 1).gameObject;
        return true;
    }

    /// <summary>전투 데이터, MP UI, 카드 덱과 Player 행동 입력을 전달된 캐릭터에 연결한다.</summary>
    public static bool TryRegisterRuntime(
        GameObject player,
        PlayerMPUI playerMPUI,
        BattleCardDrawSystem cardDrawSystem,
        BattlePlayerActionController actionController,
        Object logContext,
        out CharacterMP playerMP,
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

        if (!BattlePlayerRuntimeDataFactory.TryCreate(player, out playerMP, out combatData, out playerHealth))
        {
            Debug.LogError("플레이어 등록 실패: 전투 런타임 데이터를 구성하지 못했습니다.", logContext);
            return false;
        }

        playerMPUI?.Bind(playerMP);
        playerMP.RestoreFull();
        PlayerDeck registeredDeck = player.GetComponentInParent<PlayerDeck>(true);
        cardDrawSystem?.InitializeDeck(registeredDeck);
        actionController?.SetPlayer(player);
        return true;
    }
}
