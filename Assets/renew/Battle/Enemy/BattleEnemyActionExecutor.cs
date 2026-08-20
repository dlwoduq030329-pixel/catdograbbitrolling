using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy가 결정한 이동과 행동 MP 차감을 실제 게임 상태에 적용한다.
/// Target 선택, 경로 계산, 행동 우선순위와 턴 종료 여부는 판단하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyActionExecutor : MonoBehaviour
{
    private CharacterMP characterMP;
    private float secondsPerTile = 0.2f;

    /// <summary>행동 실행에 필요한 MP와 타일 이동 시간을 전달받는다.</summary>
    public void Configure(CharacterMP targetMP, float moveSecondsPerTile)
    {
        characterMP = targetMP;
        secondsPerTile = Mathf.Max(0.01f, moveSecondsPerTile);
    }

    /// <summary>현재 MP로 가능한 만큼 공격 사거리 직전까지 이동하고 도착한 타일마다 MP를 차감한다.</summary>
    public IEnumerator MoveAlongPath(
        IReadOnlyList<MapInfo> path,
        MapInfo startTile,
        int attackRangeTiles,
        int moveCostPerTile)
    {
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

        float heightOffset = transform.position.y - startTile.transform.position.y;

        BattleCharacterAnimationBridge.PlayWalk(gameObject);

        for (int i = 0; i < moveCount; i++)
        {
            Vector3 targetPosition = path[i].transform.position + Vector3.up * heightOffset;
            yield return BattleTransformMovement.MoveToPosition(
                transform,
                targetPosition,
                secondsPerTile);

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
