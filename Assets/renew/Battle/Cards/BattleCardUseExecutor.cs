using System;
using UnityEngine;

/// <summary>카드의 MP 비용, 손패 소비와 주사위 실행을 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleCardUseExecutor : MonoBehaviour
{
    private GameObject player;
    private BattleUnitMP playerMP;
    private BattleStatusEffects statusEffects;
    private BattleDiceSystem diceSystem;

    public void Setup(GameObject targetPlayer, BattleDiceSystem targetDiceSystem)
    {
        player = targetPlayer;
        diceSystem = targetDiceSystem;
        playerMP = player != null ? player.GetComponent<BattleUnitMP>() : null;
        statusEffects = player != null ? player.GetComponent<BattleStatusEffects>() : null;
    }

    /// <summary>상태이상까지 적용한 카드의 최종 MP 비용을 반환합니다.</summary>
    public int GetMpCost(BattleActionRequest action, BattleCardData card)
    {
        if (action == null)
            return 0;

        int cost = action.MPCost;
        if (statusEffects != null && card != null && card.category == BattleCardCategory.Attack)
            cost = statusEffects.ModifyAttackCost(cost);
        return cost;
    }

    public bool CanPay(int mpCost)
    {
        return playerMP != null && playerMP.CanSpend(mpCost);
    }

    /// <summary>MP를 차감하고 카드를 버립니다. 카드 소비 실패 시 MP를 복구합니다.</summary>
    public bool TryPay(int mpCost, BattleCardDrawSystem drawSystem, SelectedCardUseInfo card)
    {
        if (playerMP == null || drawSystem == null || card == null)
            return false;

        int mpBeforePayment = playerMP.CurrentMP;
        if (!playerMP.TrySpend(mpCost))
        {
            Debug.LogWarning($"카드 사용 확정 중 MP {mpCost} 차감에 실패했습니다.", this);
            return false;
        }

        if (drawSystem.TryMoveUsedCardToDiscardPile(card))
            return true;

        playerMP.SetCurrentMP(mpBeforePayment);
        Debug.LogError("손패의 카드가 바뀌어 사용하지 못했습니다. 차감한 MP를 복구했습니다.", this);
        return false;
    }

    /// <summary>주사위 버튼을 열고 결과를 전달합니다.</summary>
    public bool TryStartRoll(BattleCardData card, Action<CardDiceResult> onFinished)
    {
        return diceSystem != null && diceSystem.TryStartCardRoll(player, card, onFinished);
    }
}
