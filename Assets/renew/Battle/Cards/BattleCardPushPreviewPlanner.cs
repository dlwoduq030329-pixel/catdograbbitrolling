using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 사용 확정 전에 표시할 밀치기 결과를 계산한다.
/// Transform·HP·상태를 실제로 변경하지 않고 BattleCardMovementService의 계산 API로 PushPlan만 만든다.
/// 만들어진 계획을 어떤 이미지로 표시할지는 BattlePushPreviewView가 담당한다.
/// </summary>
public static class BattleCardPushPreviewPlanner
{
    /// <summary>
    /// 현재 카드와 선택 대상에 맞는 밀치기 계획을 모두 반환한다.
    /// 범위 밀치기는 Player 주변의 살아 있는 Enemy마다 하나씩 만들고,
    /// 단일 밀치기는 Dash 도착 예상 위치까지 반영해 계획 하나를 만든다.
    /// Push 효과가 없거나 계산할 수 없으면 빈 목록을 반환한다.
    /// </summary>
    public static List<BattleCardMovementService.PushPlan> BuildPushPlans(
        GameObject player,
        GameObject selectedTarget,
        BattleCardData cardData,
        Func<Vector3, MapInfo> findClosestTile)
    {
        // Preview View에는 계산이 끝난 계획만 넘긴다. 여기서는 Transform 이동이나 피해를 적용하지 않는다.
        List<BattleCardMovementService.PushPlan> pushPlans =
            new List<BattleCardMovementService.PushPlan>();
        if (player == null || selectedTarget == null || cardData == null ||
            !BattleCardEffectDataQuery.TryFindFirstEffect(
                cardData,
                BattleCardEffectType.Push,
                out BattleCardEffectData pushEffect))
        {
            return pushPlans;
        }

        // 범위 밀치기는 선택 대상 하나가 아니라 Player 중심 효과 범위의 모든 적을 각각 계산해야 한다.
        if (pushEffect.effectTarget == BattleCardEffectTarget.TargetsInArea)
        {
            MapInfo playerTile = findClosestTile != null
                ? findClosestTile(player.transform.position)
                : null;
            // 적마다 벽 충돌·다른 Enemy 충돌·물 추락 결과가 다르므로 PushPlan도 개별 생성한다.
            foreach (GameObject pushTarget in FindLivingEnemiesInsidePushArea(
                         player,
                         playerTile,
                         cardData.areaSizeTiles,
                         findClosestTile))
            {
                if (BattleCardMovementService.TryCreatePushPlan(
                        player,
                        pushTarget,
                        Mathf.Max(0, pushEffect.distanceTiles),
                        Mathf.Max(1, pushEffect.pushForce),
                        out BattleCardMovementService.PushPlan areaPushPlan))
                {
                    pushPlans.Add(areaPushPlan);
                }
            }

            return pushPlans;
        }

        // 돌진 후 밀치기는 현재 Player 위치가 아니라 돌진 완료 예상 타일에서 밀기 방향을 계산한다.
        MapInfo predictedPushSourceTile = null;
        if (BattleCardEffectDataQuery.ContainsEffect(cardData, BattleCardEffectType.Dash) &&
            BattleCardMovementService.TryCreateDashPlan(
                player,
                selectedTarget,
                BattleCardEffectDataQuery.FindLongestMovementDistance(cardData, BattleCardEffectType.Dash),
                out BattleCardMovementService.MovementPlan dashPlan,
                out _))
        {
            // 실제 실행 순서는 Dash 다음 Push이므로 Preview의 밀기 방향도 Dash 도착지에서 시작해야 한다.
            predictedPushSourceTile = dashPlan.Destination;
        }

        // Dash가 없는 카드는 predictedPushSourceTile이 null이며 MovementService가 현재 Player 타일을 사용한다.
        if (BattleCardMovementService.TryCreatePushPlan(
                player,
                selectedTarget,
                predictedPushSourceTile,
                BattleCardEffectDataQuery.FindLongestMovementDistance(cardData, BattleCardEffectType.Push),
                BattleCardEffectDataQuery.FindStrongestPushForce(cardData),
                out BattleCardMovementService.PushPlan singlePushPlan))
        {
            pushPlans.Add(singlePushPlan);
        }

        return pushPlans;
    }

    /// <summary>
    /// Player 중심 효과 범위 안에 있는 살아 있는 Enemy를 범위 Push 후보로 수집한다.
    /// EnemyTurnActor를 기준으로 검색해 장식용 모델을 제외하고, 시계 방향으로 정렬해
    /// Scene 검색 순서가 달라져도 Preview 생성 순서를 일정하게 유지한다.
    /// </summary>
    private static List<GameObject> FindLivingEnemiesInsidePushArea(
        GameObject player,
        MapInfo playerTile,
        int areaSizeInTiles,
        Func<Vector3, MapInfo> findClosestTile)
    {
        List<GameObject> pushTargets = new List<GameObject>();
        if (playerTile == null || findClosestTile == null)
        {
            return pushTargets;
        }

        // 데이터가 0 이하더라도 범위 밀치기가 완전히 사라지지 않도록 최소 한 칸으로 보정한다.
        int pushAreaRadius = Mathf.Max(1, areaSizeInTiles);
        foreach (EnemyTurnActor enemy in UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(
                     FindObjectsSortMode.None))
        {
            // 검색 직후 파괴됐거나 풀링으로 비활성화된 Enemy는 현재 전투 Preview 대상이 아니다.
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            BattleHealth enemyHealth = enemy.GetComponent<BattleHealth>();
            MapInfo enemyTile = findClosestTile(enemy.transform.position);
            int distanceFromPlayer = BattleTileRangeCalculator.GetDistance(
                playerTile,
                enemyTile,
                pushAreaRadius);
            // 거리 0은 Player 타일이고 음수는 범위 밖이므로 살아 있는 범위 내 Enemy만 추가한다.
            if (enemyHealth != null && !enemyHealth.IsDead && distanceFromPlayer > 0)
            {
                pushTargets.Add(enemy.gameObject);
            }
        }

        // FindObjectsByType의 반환 순서는 보장되지 않는다. 표시 생성 순서를 고정하면
        // 동일한 상황에서 Preview 아이콘이 매번 다른 순서로 만들어지는 현상을 줄일 수 있다.
        pushTargets.Sort((left, right) =>
            GetClockwiseAngle(player.transform.position, left.transform.position).CompareTo(
                GetClockwiseAngle(player.transform.position, right.transform.position)));
        return pushTargets;
    }

    /// <summary>중심에서 대상까지의 XZ 방향을 0~360도 시계 방향 각도로 변환한다.</summary>
    private static float GetClockwiseAngle(Vector3 center, Vector3 target)
    {
        Vector3 offset = target - center;
        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        return angle < 0f ? angle + 360f : angle;
    }
}
