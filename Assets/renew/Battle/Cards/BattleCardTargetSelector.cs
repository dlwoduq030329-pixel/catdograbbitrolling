using System;
using UnityEngine;

/// <summary>
/// 카드 데이터가 자동 대상 선택을 요구할 때 실제 전투 유닛 후보를 검색하고 우선순위를 결정한다.
/// 카드 사용 상태·MP·효과 실행·화면 표시는 변경하지 않으며, 찾은 대상과 대상 타일만 호출자에게 반환한다.
/// </summary>
public static class BattleCardTargetSelector
{
    /// <summary>
    /// Player 기준 카드 사거리 안의 살아 있는 Enemy 중 현재 HP가 가장 낮은 대상을 찾는다.
    /// HP가 같으면 Player와 더 가까운 Enemy를 선택해 검색 순서가 달라도 결과가 일정하게 유지된다.
    /// 성공하면 Enemy GameObject와 그 Enemy가 서 있는 MapInfo를 함께 반환한다.
    /// </summary>
    public static bool TryFindLowestHealthEnemyInRange(
        GameObject player,
        int maximumCardRange,
        Func<Vector3, MapInfo> findClosestTile,
        out GameObject selectedEnemy,
        out MapInfo selectedEnemyTile)
    {
        // 실패했을 때 이전 호출의 대상이 남지 않도록 out 값은 항상 비운 상태에서 시작한다.
        selectedEnemy = null;
        selectedEnemyTile = null;

        MapInfo playerTile = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        if (playerTile == null)
        {
            return false;
        }

        float lowestHealthFound = float.MaxValue;
        int closestDistanceAmongLowestHealth = int.MaxValue;
        // 현재 Enemy 생성·사망 목록을 한 곳에서 안정적으로 전달하는 Registry 연결이 아직 완성되지 않았다.
        // 따라서 카드 선택이 시작되는 이 순간에만 Scene의 EnemyTurnActor를 검색한다.
        // Update처럼 매 Frame 실행되는 검색은 아니지만 Enemy 수에 비례한 비용이 발생하므로,
        // BattleUnitRegistry가 전투 Enemy 목록의 유일한 원본이 되면 그 목록을 인자로 받도록 교체해야 한다.
        // Enemy 모델 자식이나 장식 Object가 아니라 실제 턴을 소유한 EnemyTurnActor를 검색 기준으로 삼는다.
        EnemyTurnActor[] enemies = UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(
            FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            // 같은 프레임에 파괴됐거나 풀링으로 비활성화된 Enemy는 현재 선택 후보가 아니다.
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            BattleHealth enemyHealth = enemy.GetComponent<BattleHealth>();
            MapInfo enemyTile = findClosestTile(enemy.transform.position);
            int distanceFromPlayer = BattleTileRangeCalculator.GetDistance(
                playerTile,
                enemyTile,
                maximumCardRange);
            if (enemyHealth == null || enemyHealth.IsDead || distanceFromPlayer < 0)
            {
                continue;
            }

            bool hasLowerHealth = enemyHealth.CurrentHealth < lowestHealthFound;
            bool hasSameHealthButIsCloser =
                Mathf.Approximately(enemyHealth.CurrentHealth, lowestHealthFound) &&
                distanceFromPlayer < closestDistanceAmongLowestHealth;
            if (!hasLowerHealth && !hasSameHealthButIsCloser)
            {
                continue;
            }

            lowestHealthFound = enemyHealth.CurrentHealth;
            closestDistanceAmongLowestHealth = distanceFromPlayer;
            selectedEnemy = enemy.gameObject;
            selectedEnemyTile = enemyTile;
        }

        return selectedEnemy != null && selectedEnemyTile != null;
    }
}
