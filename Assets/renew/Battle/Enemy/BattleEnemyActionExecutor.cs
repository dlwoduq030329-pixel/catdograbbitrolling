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
    // Player의 BattlePlayerMover와 같은 State 이름을 그대로 쓴다. PlayState는 해당 State가 없는
    // Animator에서는 조용히 false만 반환하므로, 점프 모션이 없는 Enemy 모델에도 안전하게 호출할 수 있다.
    private const string JumpStartAnimationState = "Jump_Idle";
    private const string JumpLandingAnimationState = "Jump_Land";

    private BattleUnitMP characterMP;
    private float secondsPerTile = 0.2f;

    // 아래 세 값은 Player의 BattlePlayerMover와 동일한 역할이다(단차 이동을 점프로 연출).
    // 2026-09-04: Enemy는 지금까지 단차와 무관하게 항상 미끄러지듯 이동했는데, Player처럼
    // 방향 전환 후 점프하는 연출을 옮겨달라는 요청으로 추가했다.
    [Header("단차 이동 연출")]
    [Tooltip("앞 타일과 다음 타일의 높이 차이가 이 값 이상일 때 점프 이동을 사용한다.")]
    [SerializeField, Min(0f)] private float minimumHeightDifferenceForJump = 0.1f;
    private float jumpTakeoffDelaySeconds = 0.1f;
    private float jumpArcHeight = 1.5f;

    /// <summary>
    /// 행동 실행에 필요한 MP와 타일 이동 시간을 전달받는다. 
    /// takeoffDelaySeconds/baseJumpArcHeight를 생략하면 Player 기본값(0.1초 / 1.5)과 같은 값을 쓴다.
    /// </summary>
    public void Configure(
        BattleUnitMP targetMP,
        float moveSecondsPerTile,
        float takeoffDelaySeconds = 0.1f,
        float baseJumpArcHeight = 1.5f)
    {
        characterMP = targetMP;
        secondsPerTile = Mathf.Max(0.01f, moveSecondsPerTile);
        jumpTakeoffDelaySeconds = Mathf.Max(0f, takeoffDelaySeconds);
        jumpArcHeight = Mathf.Max(0f, baseJumpArcHeight);
    }

    /// <summary>
    /// 현재 위치에서 targetPosition까지, 높이 차이가 minimumHeightDifferenceForJump 이상이면 Player와
    /// 같은 방식(먼저 회전 → 점프 State 재생 → 이륙 대기 → 포물선 이동 → 착지 State)으로, 그렇지 않으면
    /// 기존처럼 수평 이동만으로 한 칸을 옮긴다. MoveAlongPath/MoveToSingleTile이 공통으로 사용한다.
    /// </summary>
    private IEnumerator MoveOneTile(Vector3 targetPosition)
    {
        float heightDifference = Mathf.Abs(targetPosition.y - transform.position.y);
        bool crossesHeightStep = heightDifference >= minimumHeightDifferenceForJump;

        if (!crossesHeightStep)
        {
            yield return BattleUnitMotionAnimator.MoveToPosition(transform, targetPosition, secondsPerTile);
            yield break;
        }

        // 점프 이동과 회전을 동시에 시작하지 않고, 목표 타일 방향을 먼저 바라본 뒤 준비 동작에 들어간다.
        yield return BattleUnitMotionAnimator.RotateTowards(transform, targetPosition);

        // 모델 Animator에 점프 State가 있으면 함께 재생한다. 없는 모델도 위치 포물선은 정상 동작한다.
        BattleCharacterAnimationBridge.PlayState(gameObject, JumpStartAnimationState);

        // 이 yield가 실제로 "점프 준비 시간(windup)"을 만드는 지점이다: WaitForSeconds가 끝날 때까지
        // 코루틴이 여기서 멈춰 있으므로, 바로 위에서 재생한 점프 시작 State(Jump_Idle)만 먼저 화면에
        // 보이고, 실제 위치 이동(MoveToPositionWithJumpArc)은 jumpTakeoffDelaySeconds가 다 지난 뒤에야
        // 시작된다. 이 대기가 없으면 State 재생과 포물선 이동이 같은 프레임에 겹쳐서 준비 동작 없이
        // 순간이동하듯 보인다.
        if (jumpTakeoffDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(jumpTakeoffDelaySeconds);
        }

        yield return BattleUnitMotionAnimator.MoveToPositionWithJumpArc(
            transform,
            targetPosition,
            secondsPerTile,
            jumpArcHeight);
        BattleCharacterAnimationBridge.PlayState(gameObject, JumpLandingAnimationState);
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
            yield return MoveOneTile(targetPosition);

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

    /// <summary>
    /// 목표를 향해 공격 사거리 직전까지만 접근하는 MoveAlongPath와 달리, 배회(idleBehavior=Wander)처럼
    /// "그냥 인접한 한 칸으로 이동"하는 전용 메서드다. 공격 사거리 개념이 없어 destinationTile에
    /// 실제로 도착할 때까지 이동하며, 이동 성공 시에만 MP를 차감한다.
    /// </summary>
    public IEnumerator MoveToSingleTile(MapInfo destinationTile, MapInfo startTile, int moveCostPerTile)
    {
        if (characterMP == null || destinationTile == null || startTile == null)
        {
            yield break;
        }

        int safeMoveCost = Mathf.Max(1, moveCostPerTile);
        if (!characterMP.TrySpend(safeMoveCost))
        {
            yield break;
        }

        // MoveAlongPath와 같은 피벗 오프셋 보정: 시작 타일 표면보다 이 유닛이 얼마나 위에 떠 있는지를
        // 유지한 채로 목적 타일로 이동해, 배회 중에도 발이 파묻히거나 뜨지 않게 한다.
        float heightOffset = transform.position.y - startTile.transform.position.y;
        Vector3 targetPosition = destinationTile.transform.position + Vector3.up * heightOffset;

        BattleCharacterAnimationBridge.PlayWalk(gameObject);
        yield return MoveOneTile(targetPosition);
        BattleCharacterAnimationBridge.PlayIdle(gameObject);
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
