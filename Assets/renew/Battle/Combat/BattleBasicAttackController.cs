using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기본 공격의 계획, 임시 이동, 확인 대기, 확정과 취소 복귀 흐름을 소유한다.
/// 입력과 확인 UI 표시는 Coordinator에 이벤트로 요청한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleBasicAttackController : MonoBehaviour
{
    private GameObject player;
    private PlayerCombatData combatData;
    private BattlePlayerMover playerMover;
    private Func<Vector3, MapInfo> findClosestTile;
    private Func<MapInfo, bool> isWalkable;

    private BattleActionRequest pendingAction;
    private EnemyTurnActor pendingEnemy;
    private List<MapInfo> pendingMovementPath = new List<MapInfo>();
    // Begin() 시점의 Enemy 점유 타일 집합을 Confirm()까지 들고 있는다 — 확정 직전 재검증에서도
    // "점유 타일 너머로는 공격 사거리가 닿지 않는다"는 같은 규칙을 적용하기 위함(GetDistance 벽/점유 버그 수정분).
    private ISet<MapInfo> pendingOccupiedTiles;
    private MapInfo originalTile;
    private Vector3 originalPosition;
    private int attackCountThisTurn;

    public bool IsExecuting { get; private set; }
    public bool IsAwaitingConfirmation { get; private set; }

    public event Action<string> ConfirmationRequested;
    public event Action<BattleActionResult> Confirmed;
    public event Action Cancelled;

    /// <summary>공격 계획에 필요한 Player, 이동, MP와 확인 완료 이벤트를 연결한다.</summary>
    public void Configure(
        GameObject targetPlayer,
        PlayerCombatData playerCombatData,
        BattlePlayerMover mover,
        Func<Vector3, MapInfo> tileFinder,
        Func<MapInfo, bool> walkableCheck)
    {
        player = targetPlayer;
        combatData = playerCombatData;
        playerMover = mover;
        findClosestTile = tileFinder;
        isWalkable = walkableCheck;
    }

    /// <summary>선택한 Enemy까지의 접근 경로와 총 MP 비용을 계산하고 공격 확인 대기 상태를 시작한다.</summary>
    public bool Begin(
        EnemyTurnActor enemy,
        IEnumerable<MapInfo> reachableTiles,
        ISet<MapInfo> occupiedTiles)
    {
        if (IsExecuting || IsAwaitingConfirmation || enemy == null || player == null ||
            combatData == null || playerMover == null || findClosestTile == null || isWalkable == null)
        {
            return false;
        }

        MapInfo playerTile = findClosestTile(player.transform.position);
        MapInfo enemyTile = findClosestTile(enemy.transform.position);
        int attackRange = combatData.BasicAttackRangeTiles;
        int actionCost = GetCurrentAttackCost();
        BattleUnitMP playerMP = player.GetComponent<BattleUnitMP>();

        if (!BattleBasicAttackService.TryCreatePlan(
                playerTile,
                enemyTile,
                reachableTiles,
                occupiedTiles,
                isWalkable,
                attackRange,
                actionCost,
                playerMP,
                out List<MapInfo> movementPath,
                out int totalCost))
        {
            Debug.Log(totalCost > 0
                ? $"기본 공격에 필요한 MP가 부족합니다. 필요 MP: {totalCost}"
                : "현재 이동 및 공격 범위에서 해당 적을 공격할 수 없습니다.", this);
            return false;
        }

        pendingOccupiedTiles = occupiedTiles;
        pendingAction = new BattleActionRequest(
            "기본 공격",
            BattleActionType.BasicAttack,
            attackRange,
            actionCost,
            combatData.BasicAttackPower + GetBasicAttackBonus());
        pendingEnemy = enemy;
        pendingMovementPath = new List<MapInfo>(movementPath);
        originalTile = playerTile;
        originalPosition = player.transform.position;
        StartCoroutine(MoveToConfirmationPosition());
        return true;
    }

    /// <summary>공격 비용을 다시 검사한 뒤 필요하면 접근 이동하고 공격을 턴당 한 번 확정한다.</summary>
    public void Confirm()
    {
        if (!IsAwaitingConfirmation || pendingAction == null || pendingEnemy == null || player == null)
        {
            return;
        }

        MapInfo playerTile = findClosestTile(player.transform.position);
        MapInfo enemyTile = findClosestTile(pendingEnemy.transform.position);
        int actionCost = GetCurrentAttackCost();
        BattleHealth targetHealth = pendingEnemy.GetComponent<BattleHealth>();

        // MP 차감 전에 피해 대상 상태를 확인해 공격 비용만 사라지는 부분 성공을 방지한다.
        if (targetHealth == null || targetHealth.IsDead || pendingAction.Power <= 0f)
        {
            Debug.LogWarning("공격 대상의 체력 또는 공격 위력이 유효하지 않아 기본 공격을 취소합니다.", this);
            Cancel();
            return;
        }

        if (!BattleBasicAttackService.TryConfirm(
                pendingAction,
                player,
                pendingEnemy,
                playerTile,
                enemyTile,
                pendingMovementPath,
                actionCost,
                out BattleActionResult result,
                isWalkable,
                pendingOccupiedTiles))
        {
            Debug.Log("공격 확정 직전 조건이 변경되어 기본 공격을 취소합니다.", this);
            Cancel();
            return;
        }

        if (!BattleDamageService.TryApplyDamage(
                player,
                pendingEnemy.gameObject,
                pendingAction.Power,
                BattleDamageType.Physical,
                out BattleDamageResult damageResult))
        {
            Debug.LogWarning("기본 공격 피해를 적용하지 못해 공격 확정을 취소합니다.", this);
            Cancel();
            return;
        }

        BattleUnitMotionAnimator.FaceTowards(player.transform, pendingEnemy.transform.position);
        BattleCharacterAnimationBridge.PlayAttack(player);
        attackCountThisTurn++;
        IsAwaitingConfirmation = false;
        ClearPending();
        Confirmed?.Invoke(result);

        Debug.Log(
            $"{damageResult.Target.name}: {damageResult.AppliedDamage:0.##} 피해, " +
            $"남은 HP {damageResult.RemainingHealth:0.##}/{damageResult.Target.GetComponent<BattleHealth>().MaxHealth:0.##}",
            this);
    }

    /// <summary>확정 전 임시 이동과 공격 계획을 취소하고 Player 위치·MP를 원래 상태로 복구한다.</summary>
    public void Cancel()
    {
        if (!IsAwaitingConfirmation || IsExecuting)
        {
            return;
        }

        StartCoroutine(ReturnToOriginalPosition());
    }

    /// <summary>턴 전환 시 남아 있는 공격 계획과 임시 상태를 제거한다.</summary>
    public void ResetTurn()
    {
        attackCountThisTurn = 0;
        player?.GetComponent<BattleBasicAttackBuff>()?.Clear();
        IsAwaitingConfirmation = false;
        ClearPending();
    }

    private float GetBasicAttackBonus()
    {
        BattleBasicAttackBuff buff = player != null ? player.GetComponent<BattleBasicAttackBuff>() : null;
        return buff != null ? buff.BonusDamage : 0f;
    }

    private IEnumerator MoveToConfirmationPosition()
    {
        IsExecuting = true;
        yield return playerMover.MoveAlongPath(pendingMovementPath, originalTile);
        IsExecuting = false;

        if (pendingEnemy == null || !pendingEnemy.gameObject.activeInHierarchy)
        {
            yield return ReturnToOriginalPosition();
            yield break;
        }

        IsAwaitingConfirmation = true;
        ConfirmationRequested?.Invoke(
            $"{pendingEnemy.name}에게 {pendingAction.DisplayName}을 사용하시겠습니까? " +
            $"(필요 MP {pendingMovementPath.Count + pendingAction.MPCost})");
    }

    private IEnumerator ReturnToOriginalPosition()
    {
        IsAwaitingConfirmation = false;
        IsExecuting = true;

        if (player != null)
        {
            yield return playerMover.ReturnToPosition(originalPosition, pendingMovementPath.Count);
        }

        IsExecuting = false;
        ClearPending();
        Cancelled?.Invoke();
    }

    private int GetCurrentAttackCost()
    {
        int baseCost = combatData != null ? combatData.BasicAttackMPCost : 1;
        int cost = BattleAttackCostService.CalculateRepeatedAttackCost(
            baseCost,
            attackCountThisTurn);
        BattleStatusEffects status = player != null ? player.GetComponent<BattleStatusEffects>() : null;
        return status != null ? status.ModifyAttackCost(cost) : cost;
    }

    private void ClearPending()
    {
        pendingAction = null;
        pendingEnemy = null;
        pendingMovementPath.Clear();
        pendingOccupiedTiles = null;
        originalTile = null;
        originalPosition = Vector3.zero;
    }
}
