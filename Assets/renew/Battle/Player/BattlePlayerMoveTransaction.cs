using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반 이동 경로 검증, 이동 연출, 이동 MP 차감을 하나의 트랜잭션으로 처리한다.
/// 목적지 입력, 타일 표시, 확인 UI는 담당하지 않는다.
/// 경로/MP 검증은 항상 실제 이동 연출(BattlePlayerMover.MoveAlongPath) 이전에 끝내서,
/// 검증 실패가 곧 "아무 일도 일어나지 않았다"를 보장한다(이동 후 실패가 발생하지 않도록 함).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerMoveTransaction : MonoBehaviour
{
    private GameObject playerObject;
    private BattlePlayerMover playerMover;

    public bool IsExecuting { get; private set; }
    public bool IsAwaitingConfirmation { get; private set; }
    public MapInfo PendingTarget { get; private set; }

    /// <summary>이동할 Player와 실제 Transform 연출을 수행할 Mover를 연결한다.</summary>
    public void AttachPlayer(GameObject targetPlayer, BattlePlayerMover mover)
    {
        playerObject = targetPlayer;
        playerMover = mover;
    }

    /// <summary>확정 전 목적 타일을 보관한다. 유효한 타일이 아니면 선택 상태를 만들지 않는다.</summary>
    public bool SelectTarget(MapInfo targetTile)
    {
        if (IsExecuting || targetTile == null)
        {
            return false;
        }

        PendingTarget = targetTile;
        IsAwaitingConfirmation = true;
        return true;
    }

    /// <summary>아직 실행하지 않은 목적지 선택만 취소한다. Player 위치와 MP는 변경하지 않는다.</summary>
    public bool CancelSelection()
    {
        bool wasAwaitingConfirmation = IsAwaitingConfirmation;
        PendingTarget = null;
        IsAwaitingConfirmation = false;
        return wasAwaitingConfirmation;
    }

    /// <summary>턴 시작·종료 시 남아 있는 이동 선택과 실행 중 상태를 초기화한다.</summary>
    public void ResetTurn()
    {
        PendingTarget = null;
        IsAwaitingConfirmation = false;
    }

    /// <summary>
    /// 보관된 목적지(PendingTarget)를 다시 검증한 뒤 이동하고, 성공한 경로의 칸 수만큼 MP를 차감한다.
    /// 선택된 목적지 경로가 허용 이동 칸 수(maxMovementTiles)를 넘지 않을 때만 이동을 실행한다.
    /// </summary>
    public IEnumerator TryExecutePendingMove(
        MapInfo startTile,
        Func<MapInfo, bool> isWalkable,
        ISet<MapInfo> occupiedTiles,
        int maxMovementTiles,
        Action<BattleMovementResult> completed)
    {
        MapInfo targetTile = PendingTarget;
        if (!IsAwaitingConfirmation || targetTile == null)
        {
            completed?.Invoke(BattleMovementResult.Failed("확정할 이동 목적지가 없습니다."));
            yield break;
        }

        yield return RunValidatedMove(
            startTile,
            targetTile,
            isWalkable,
            occupiedTiles,
            maxMovementTiles,
            completed);
    }

    /// <summary>
    /// 경로, 이동 상한, MP를 전부 실제 이동 연출 전에 검증하고 확정한 뒤에만 이동을 실행한다.
    /// MP 차감(TrySpend)이 이동 연출(MoveAlongPath)보다 먼저 일어나므로, MP 부족으로 실패하는 경우
    /// Player가 이미 이동해버린 채로 실패 콜백만 오는 상황(위치·MP 불일치)이 생기지 않는다.
    /// </summary>
    private IEnumerator RunValidatedMove(
        MapInfo startTile,
        MapInfo targetTile,
        Func<MapInfo, bool> isWalkable,
        ISet<MapInfo> occupiedTiles,
        int maxMovementTiles,
        Action<BattleMovementResult> completed)
    {
        if (IsExecuting || playerObject == null || playerMover == null ||
            startTile == null || targetTile == null)
        {
            completed?.Invoke(BattleMovementResult.Failed("이동 실행에 필요한 참조가 없습니다."));
            yield break;
        }

        if (!BattleTileRangeCalculator.TryCalculatePath(
                startTile,
                targetTile,
                isWalkable,
                occupiedTiles,
                out List<MapInfo> path))
        {
            completed?.Invoke(BattleMovementResult.Failed("선택한 타일까지 이동 경로를 계산하지 못했습니다."));
            yield break;
        }

        BattleUnitMP playerMP = playerObject.GetComponent<BattleUnitMP>();
        int movementCost = path.Count;
        BattleStatusEffects movementStatus = playerObject.GetComponent<BattleStatusEffects>();
        if (movementStatus != null && path.Count > 0)
        {
            movementCost = path.Count * movementStatus.ModifyMoveCost(1);
        }
        if (path.Count > Mathf.Max(0, maxMovementTiles))
        {
            completed?.Invoke(BattleMovementResult.Failed(
                "Enemy를 피한 경로가 이번 이동 가능 거리를 초과합니다."));
            yield break;
        }

        // 이동 연출을 시작하기 전에 MP를 먼저 확정 차감한다(검증-후-실행이 아니라 확정-후-실행).
        // 이렇게 해야 MP 부족으로 인한 실패가 항상 "이동이 전혀 일어나지 않은" 상태로 보장된다.
        if (playerMP == null || !playerMP.TrySpend(movementCost))
        {
            completed?.Invoke(BattleMovementResult.Failed("현재 MP가 부족하여 선택한 타일까지 이동할 수 없습니다."));
            yield break;
        }

        IsExecuting = true;
        yield return playerMover.MoveAlongPath(path, startTile);
        IsExecuting = false;

        PendingTarget = null;
        IsAwaitingConfirmation = false;
        completed?.Invoke(BattleMovementResult.Succeeded(path, movementCost));
    }
}

/// <summary>일반 이동 트랜잭션의 성공 여부, 경로, MP 비용과 실패 원인을 전달한다.</summary>
public sealed class BattleMovementResult
{
    public bool Success { get; }
    public IReadOnlyList<MapInfo> Path { get; }
    public int MPCost { get; }
    public string FailureReason { get; }

    private BattleMovementResult(
        bool success,
        IReadOnlyList<MapInfo> path,
        int mpCost,
        string failureReason)
    {
        Success = success;
        Path = path;
        MPCost = mpCost;
        FailureReason = failureReason;
    }

    /// <summary>완료된 경로와 실제 MP 비용을 포함한 이동 성공 결과를 생성한다.</summary>
    public static BattleMovementResult Succeeded(IReadOnlyList<MapInfo> path, int mpCost)
    {
        return new BattleMovementResult(true, path, mpCost, string.Empty);
    }

    /// <summary>위치와 MP를 확정하지 못한 이동 실패 결과를 사유와 함께 생성한다.</summary>
    public static BattleMovementResult Failed(string reason)
    {
        return new BattleMovementResult(false, Array.Empty<MapInfo>(), 0, reason);
    }
}
