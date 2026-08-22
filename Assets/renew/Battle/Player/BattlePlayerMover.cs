using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player Transform을 타일 경로 또는 지정 위치로 이동시키는 연출 전용 컴포넌트다.
/// 경로 계산, MP 차감, 이동 가능 여부는 판단하지 않는다. 실제 이동 애니메이션 재생은
/// BattleUnitMotionAnimator에, 걷기/대기 모션 전환은 BattleCharacterAnimationBridge에 위임한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerMover : MonoBehaviour
{
    private GameObject playerObject;

    // 아래 두 값은 항상 Configure()에서 BattlePlayerActionController의 Inspector 값(기본 1f / 4f)으로
    // 덮어써진다. 여기 초기값은 Configure가 호출되기 전에도 GetDurationPerTile()이 0으로 나누지
    // 않도록 하는 안전한 기본값일 뿐, 실제 이동 속도 튜닝은 ActionController의 Inspector에서 한다.
    private float secondsPerTile = 1f;
    private float moveSpeedMultiplier = 4f;

    /// <summary>이동 대상과 한 칸당 기준 시간, 연출 배속을 설정한다. 경로·MP 규칙은 판단하지 않는다.</summary>
    public void Configure(GameObject targetPlayer, float baseSecondsPerTile, float speedMultiplier)
    {
        playerObject = targetPlayer;
        secondsPerTile = Mathf.Max(0.01f, baseSecondsPerTile);
        moveSpeedMultiplier = Mathf.Max(0.01f, speedMultiplier);
    }

    /// <summary>계산이 끝난 타일 경로를 순서대로 따라가며 Player Transform 이동만 연출한다.</summary>
    public IEnumerator MoveAlongPath(IReadOnlyList<MapInfo> path, MapInfo startTile)
    {
        if (playerObject == null || path == null || startTile == null)
        {
            yield break;
        }

        // Player가 타일 표면보다 얼마나 떠 있는지(발밑 높이 보정값)를 이동 시작 시점 기준으로 구해서,
        // 경로의 각 타일로 이동할 때도 같은 높이를 유지한 채 수평으로만 이동하게 한다.
        float heightOffset = playerObject.transform.position.y - startTile.transform.position.y;
        float durationPerTile = GetDurationPerTile();

        BattleCharacterAnimationBridge.PlayWalk(playerObject);

        foreach (MapInfo pathTile in path)
        {
            if (pathTile == null)
            {
                continue;
            }

            Vector3 targetPosition = pathTile.transform.position + Vector3.up * heightOffset;
            yield return BattleUnitMotionAnimator.MoveToPosition(
                playerObject.transform,
                targetPosition,
                durationPerTile);
        }

        BattleCharacterAnimationBridge.PlayIdle(playerObject);
    }

    /// <summary>공격 취소 등으로 임시 이동을 되돌릴 때 이동한 칸 수에 맞는 시간으로 원위치 복귀를 연출한다.</summary>
    public IEnumerator ReturnToPosition(Vector3 targetPosition, int travelledTileCount)
    {
        float duration = Mathf.Max(0.01f, travelledTileCount * GetDurationPerTile());
        BattleCharacterAnimationBridge.PlayWalk(playerObject);
        yield return BattleUnitMotionAnimator.MoveToPosition(
            playerObject != null ? playerObject.transform : null,
            targetPosition,
            duration);
        BattleCharacterAnimationBridge.PlayIdle(playerObject);
    }

    /// <summary>
    /// 타일 1칸을 이동하는 데 걸리는 실제 연출 시간(초). "기본 시간을 배속으로 나눈다"는 식이라,
    /// moveSpeedMultiplier가 클수록(배속이 높을수록) 오히려 durationPerTile은 더 짧아져서
    /// 이동이 더 빨라 보인다(기본값 기준: 1f / 4f = 0.25초/칸).
    /// </summary>
    private float GetDurationPerTile()
    {
        return Mathf.Max(0.01f, secondsPerTile / moveSpeedMultiplier);
    }
}
