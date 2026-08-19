using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player Transform을 타일 경로 또는 지정 위치로 이동시키는 연출 전용 컴포넌트다.
/// 경로 계산, MP 차감, 이동 가능 여부는 판단하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerMover : MonoBehaviour
{
    private GameObject player;
    private float secondsPerTile = 1f;
    private float speedMultiplier = 4f;

    /// <summary>이동 대상과 한 칸당 기준 시간, 연출 배속을 설정한다. 경로·MP 규칙은 판단하지 않는다.</summary>
    public void Configure(GameObject targetPlayer, float baseSecondsPerTile, float movementSpeedMultiplier)
    {
        player = targetPlayer;
        secondsPerTile = Mathf.Max(0.01f, baseSecondsPerTile);
        speedMultiplier = Mathf.Max(0.01f, movementSpeedMultiplier);
    }

    /// <summary>계산이 끝난 타일 경로를 순서대로 따라가며 Player Transform 이동만 연출한다.</summary>
    public IEnumerator MoveAlongPath(IReadOnlyList<MapInfo> path, MapInfo startTile)
    {
        if (player == null || path == null || startTile == null)
        {
            yield break;
        }

        float heightOffset = player.transform.position.y - startTile.transform.position.y;
        float durationPerTile = GetDurationPerTile();

        BattleCharacterAnimationBridge.PlayWalk(player);

        foreach (MapInfo pathTile in path)
        {
            if (pathTile == null)
            {
                continue;
            }

            Vector3 targetPosition = pathTile.transform.position + Vector3.up * heightOffset;
            yield return BattleTransformMovement.MoveToPosition(
                player.transform,
                targetPosition,
                durationPerTile);
        }

        BattleCharacterAnimationBridge.PlayIdle(player);
    }

    /// <summary>공격 취소 등으로 임시 이동을 되돌릴 때 이동한 칸 수에 맞는 시간으로 원위치 복귀를 연출한다.</summary>
    public IEnumerator ReturnToPosition(Vector3 targetPosition, int travelledTileCount)
    {
        float duration = Mathf.Max(0.01f, travelledTileCount * GetDurationPerTile());
        BattleCharacterAnimationBridge.PlayWalk(player);
        yield return BattleTransformMovement.MoveToPosition(
            player != null ? player.transform : null,
            targetPosition,
            duration);
        BattleCharacterAnimationBridge.PlayIdle(player);
    }

    private float GetDurationPerTile()
    {
        return Mathf.Max(0.01f, secondsPerTile / speedMultiplier);
    }
}
