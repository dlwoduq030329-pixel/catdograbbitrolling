using System.Collections;
using UnityEngine;

/// <summary>
/// Player와 Enemy가 공유하는 Transform 위치 이동·회전 연출이다.
/// 경로, 이동 가능 여부, MP와 행동 규칙은 판단하지 않는다.
/// MoveToPosition(실제 위치 이동)과 FaceTowards(위치 변화 없이 제자리 회전만)로 역할이 나뉘며,
/// Player 4곳(BattleCardEffectPipeline/BattleBasicAttackController/BattlePlayerMover x2)과
/// Enemy 2곳(BattleEnemyActionExecutor/EnemyTurnActor)에서 총 6번 호출된다.
/// </summary>
public static class BattleUnitMotionAnimator
{
    /// <summary>회전 없이 순간적으로 방향만 바꾸는 것을 막기 위한 초당 최대 회전각(도)이다.</summary>
    private const float RotationDegreesPerSecond = 720f;

    /// <summary>
    /// 지정 시간 동안 현재 위치에서 목표 위치로 이동하고 마지막 위치를 정확히 맞춘다.
    /// 이동하는 동안 진행 방향(XZ 평면 기준)을 바라보도록 함께 회전한다.
    /// </summary>
    /// <param name="mover">실제로 옮겨지는 유닛의 Transform(적/타겟이 아니라 "이동 주체" 자신).</param>
    /// <param name="destinationPosition">mover가 최종적으로 도착해야 할 월드 좌표.</param>
    /// <param name="durationSeconds">이동에 걸리는 시간(초).</param>
    public static IEnumerator MoveToPosition(
        Transform mover,
        Vector3 destinationPosition,
        float durationSeconds)
    {
        if (mover == null)
        {
            yield break;
        }

        float clampedDurationSeconds = Mathf.Max(0.01f, durationSeconds);
        Vector3 startPosition = mover.position;

        Vector3 flatDirection = destinationPosition - startPosition;
        flatDirection.y = 0f;
        Quaternion? lookRotation = flatDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(flatDirection.normalized, Vector3.up)
            : (Quaternion?)null;

        float elapsedSeconds = 0f;
        while (elapsedSeconds < clampedDurationSeconds)
        {
            elapsedSeconds += Time.unscaledDeltaTime;
            mover.position = Vector3.Lerp(
                startPosition,
                destinationPosition,
                Mathf.Clamp01(elapsedSeconds / clampedDurationSeconds));

            if (lookRotation.HasValue)
            {
                mover.rotation = Quaternion.RotateTowards(
                    mover.rotation,
                    lookRotation.Value,
                RotationDegreesPerSecond * Time.unscaledDeltaTime);
            }

            yield return null;
        }

        mover.position = destinationPosition;
        if (lookRotation.HasValue)
        {
            mover.rotation = lookRotation.Value;
        }
    }

    /// <summary>
    /// 이동 없이 제자리에서 목표 위치 방향(XZ 평면 기준)을 즉시 바라보게 한다.
    /// 이동하지 않고 바로 공격할 때(상하좌우 인접 대상) 항상 정면만 보고 공격하는 문제를 막기 위해 사용한다.
    /// </summary>
    /// <param name="mover">제자리에서 회전만 하는 유닛의 Transform(적/타겟이 아니라 "회전 주체" 자신).</param>
    /// <param name="facePosition">mover가 바라봐야 할 대상의 월드 좌표(보통 공격 대상 위치).</param>
    public static void FaceTowards(Transform mover, Vector3 facePosition)
    {
        if (mover == null)
        {
            return;
        }

        Vector3 flatDirection = facePosition - mover.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        mover.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
    }
}
