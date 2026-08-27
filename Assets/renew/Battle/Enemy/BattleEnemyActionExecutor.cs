using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy가 결정한 이동과 행동 MP 차감을 실제 게임 상태에 적용한다.
/// Target 선택, 경로 계산, 행동 우선순위와 턴 종료 여부는 판단하지 않는다.
/// EnemySpawner가 스폰 시 부착하고 EnemyTurnActor가 Configure()로 초기화하며, Enemy 개체마다 각자
/// 자기 전용 인스턴스를 갖는다(공용/싱글턴 아님, BattleComponentResolver.GetOrAdd가 개별 GameObject에 부착).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyActionExecutor : MonoBehaviour
{
    private BattleUnitMP characterMP;
    private float secondsPerTile = 0.2f;

    /// <summary>행동 실행에 필요한 MP와 타일 이동 시간을 전달받는다.</summary>
    public void Configure(BattleUnitMP targetMP, float moveSecondsPerTile)
    {
        characterMP = targetMP;
        secondsPerTile = Mathf.Max(0.01f, moveSecondsPerTile);
    }

    /// <summary>
    /// 현재 MP로 가능한 만큼 공격 사거리 직전까지 이동하고 도착한 타일마다 MP를 차감한다.
    /// moveCount는 "MP로 갈 수 있는 칸 수"와 "path.Count - 공격 사거리(최소 1칸)" 중 작은 값으로 정해진다.
    /// path가 시작 타일(startTile)을 포함하는지 여부에 따라 실제 정지 위치가 한 칸 달라질 수 있으므로
    /// 이동 결과가 의심되면 실기 QA로 path 구성 규칙을 먼저 확인한다.
    /// </summary>
    public IEnumerator MoveAlongPath(
        IReadOnlyList<MapInfo> path,
        MapInfo startTile,
        int attackRangeTiles,
        int moveCostPerTile)
    {
        // Executor의 공개 경계에서는 실행에 필요한 MP·경로·시작 타일만 확인한다.
        // 어떤 경로를 선택할지는 Planner 책임이므로 여기서 다른 타일을 검색하지 않는다.
        if (characterMP == null || path == null || startTile == null)
        {
            yield break;
        }

        int safeMoveCost = Mathf.Max(1, moveCostPerTile);
        int affordableTiles = characterMP.CurrentMP / safeMoveCost;
        int moveCount = Mathf.Min(
            affordableTiles,
            Mathf.Max(0, path.Count - Mathf.Max(1, attackRangeTiles)));
        if (moveCount == 0)
        {
            yield break;
        }

        // 시작 타일 표면보다 이 유닛이 얼마나 위에 떠 있는지(피벗 오프셋)를 미리 재둔다.
        // 경로의 각 타일로 이동할 때 이 오프셋을 함께 더해 이동 중 파묻히거나 붕 뜨지 않게 한다.
        float heightOffset = transform.position.y - startTile.transform.position.y;

        BattleCharacterAnimationBridge.PlayWalk(gameObject);

        for (int i = 0; i < moveCount; i++)
        {
            // Path는 시작 타일을 제외하고 이동 순서대로 들어 있으므로 0번부터 차례로 이동한다.
            Vector3 targetPosition = path[i].transform.position + Vector3.up * heightOffset;
            yield return BattleUnitMotionAnimator.MoveToPosition(
                transform,
                targetPosition,
                secondsPerTile);

            // 한 칸 이동이 실제 완료된 직후 그 칸의 MP를 차감한다. 중간 실패 시 이후 경로는 실행하지 않는다.
            if (!characterMP.TrySpend(safeMoveCost))
            {
                Debug.LogWarning($"{name}: MP 차감에 실패하여 이동을 중단했습니다.", this);
                break;
            }
        }

        BattleCharacterAnimationBridge.PlayIdle(gameObject);
    }

    /// <summary>행동 비용을 지불할 수 있으면 MP를 차감하고 성공을 반환한다.</summary>
    public bool TrySpendActionMP(int actionCost)
    {
        return characterMP != null && characterMP.TrySpend(Mathf.Max(0, actionCost));
    }

    /// <summary>공용 피해 서비스를 통해 기본 공격 피해를 대상에게 적용한다. 대상에 BattleHealth가 없으면 아무 효과가 없다.</summary>
    public bool TryApplyBasicAttackDamage(
        GameObject attacker,
        GameObject target,
        float damage,
        BattleDamageType damageType = BattleDamageType.Physical)
    {
        return BattleDamageService.TryApplyDamage(
            attacker,
            target,
            damage,
            damageType,
            out _);
    }
}
