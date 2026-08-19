using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 턴 한 번을 실행하는 행동 어댑터다.
/// BT에는 판단만 요청하고, 경로 이동과 타일별 MP 차감은 이 컴포넌트가 수행한다.
/// </summary>
public class EnemyTurnActor : MonoBehaviour
{
    [Header("적 이동 및 공격 설정")]
    [InspectorName("한 칸 이동 시간")]
    [SerializeField, Min(0.01f)] private float secondsPerTile = 0.2f;
    [InspectorName("공격 사거리 (칸)")]
    [SerializeField, Min(1)] private int attackRangeTiles = 1;
    [InspectorName("턴당 기본 공격 최대 횟수")]
    [SerializeField, Min(1)] private int maxBasicAttacksPerTurn = 1;
    [InspectorName("턴당 최대 행동 트리 평가 횟수")]
    [SerializeField, Min(1)] private int maxTreeEvaluationsPerTurn = 16;

    /// <summary>Player R 토글 등 외부에서 이 Enemy의 실제 공격 사거리(칸)를 읽을 때 사용한다.</summary>
    public int AttackRangeTiles => attackRangeTiles;

    /// <summary>이번 TakeTurn 호출에서 실제로 이동하거나 공격했는지 여부. 카메라는 이 값이
    /// true가 되는 시점(첫 실제 행동 직전)에만 포커스를 옮긴다. 대상이 없거나, 기절/속박으로
    /// 아무 것도 못 했거나, 행동 트리가 대기를 선택한 경우는 false로 남는다.</summary>
    public bool ActedThisTurn { get; private set; }

    private EnemyDetector detector;
    private EnemyAwareness awareness;
    private PathDebugView pathDebugView;
    private CharacterMP characterMP;
    private BattleEnemyRuntimeData runtimeData;
    private BehaviorNode behaviorTree;
    private BattleDataPool battleDataPool;
    private BattleEnemyActionExecutor actionExecutor;
    private BattleEnemyMapContext mapContext;

    /// <summary>Spawner가 선택한 DB 데이터 중 행동 판단에 필요한 값을 적용한다.</summary>
    public void ConfigureFromData(BattleEnemyData data)
    {
        if (data == null)
        {
            return;
        }

        attackRangeTiles = Mathf.Max(1, data.attackRangeTiles);
    }

    /// <summary>적 행동에 필요한 감지, 인식, 경로 표시 참조와 공용 행동 트리를 준비한다.</summary>
    private void Awake()
    {
        detector = GetComponentInChildren<EnemyDetector>();
        awareness = GetComponent<EnemyAwareness>();
        pathDebugView = GetComponent<PathDebugView>();
        behaviorTree = EnemyBehaviorTreeFactory.CreateAggressiveTree();
    }

    /// <summary>
    /// MP 회복, Target 확인, 경로 계산, BT 평가, 선택된 행동 실행 순으로 Enemy 턴을 처리한다.
    /// cameraRig가 주어지면 실제로 이동/공격하는 첫 순간에만 카메라를 이 Enemy로 포커스한다
    /// (대상이 없거나 아무 행동도 하지 않는 Enemy에는 카메라가 따라가지 않는다).
    /// </summary>
    public IEnumerator TakeTurn(BattleCameraRig cameraRig)
    {
        ResolveComponents();
        ResolveBattleDataPool();
        ActedThisTurn = false;
        BattleEnemyControlState controlState = GetComponent<BattleEnemyControlState>();
        bool isRooted = false;
        if (controlState != null)
        {
            controlState.ConsumeTurn(out bool isStunned, out isRooted);
            if (isStunned)
            {
                Debug.Log($"{name}: 기절 상태로 이번 턴을 행동하지 않습니다.", this);
                yield break;
            }
        }
        int basicAttackCount = 0;

        for (int evaluation = 0; evaluation < maxTreeEvaluationsPerTurn; evaluation++)
        {
            Transform target = ResolveTarget();
            if (target == null)
            {
                pathDebugView?.Clear();
                yield break;
            }

            IReadOnlyList<MapInfo> mapTiles = mapContext.ResolveMapTiles(battleDataPool);
            MapInfo startTile = MapPathfinder.FindClosestTile(transform.position, mapTiles);
            MapInfo targetTile = MapPathfinder.FindClosestTile(target.position, mapTiles);
            HashSet<MapInfo> occupiedTiles =
                mapContext.FindOtherEnemyTiles(battleDataPool, this, mapTiles);

            if (!MapPathfinder.TryFindShortestPath(startTile, targetTile, occupiedTiles, out List<MapInfo> path) &&
                BattleScarecrowSummon.IsScarecrow(target))
            {
                // 허수아비에 접근할 수 없으면 도발 때문에 턴을 버리지 않고 원래 Player를 노린다.
                target = ResolveNormalTarget();
                targetTile = target != null ? MapPathfinder.FindClosestTile(target.position, mapTiles) : null;
            }

            if (target == null ||
                !MapPathfinder.TryFindShortestPath(startTile, targetTile, occupiedTiles, out path))
            {
                Debug.LogWarning($"{name}: 플레이어까지 이어지는 경로를 찾지 못했습니다.", this);
                pathDebugView?.Clear();
                yield break;
            }

            pathDebugView?.DrawPath(transform.position, path);

            int moveCostPerTile = GetMoveCostPerTile();
            int basicAttackCost = GetBasicAttackCost(basicAttackCount);
            EnemyAIContext context = new EnemyAIContext(
                transform,
                target,
                startTile,
                targetTile,
                path,
                attackRangeTiles,
                characterMP.CurrentMP,
                moveCostPerTile,
                basicAttackCost);

            behaviorTree.Evaluate(context);
            int mpBeforeAction = characterMP.CurrentMP;

            switch (context.Decision)
            {
                case EnemyAIDecision.Move:
                    if (isRooted)
                    {
                        Debug.Log($"{name}: 속박 상태라 이번 턴에 이동할 수 없습니다.", this);
                        yield break;
                    }
                    yield return BeginActionFocus(cameraRig);
                    yield return actionExecutor.MoveAlongPath(
                        path,
                        startTile,
                        attackRangeTiles,
                        moveCostPerTile);
                    break;

                case EnemyAIDecision.Attack:
                    if (!actionExecutor.TrySpendActionMP(basicAttackCost))
                    {
                        yield break;
                    }

                    yield return BeginActionFocus(cameraRig);
                    basicAttackCount++;
                    ApplyBasicAttackDamage(target);
                    Debug.Log(
                        $"{name}: 기본 공격 {basicAttackCount}회차 선택, " +
                        $"MP {basicAttackCost} 소모, 남은 MP {characterMP.CurrentMP}",
                        this);

                    // 일반 Enemy는 기본 공격 1회가 끝나면 남은 MP와 관계없이 턴을 마친다.
                    // 특수 기믹 Enemy만 Inspector 또는 전용 데이터 연결을 통해 제한을 늘린다.
                    // Basic attacks are globally limited to exactly one per enemy turn.
                    // MP remains random, while attack and movement costs stay fixed.
                    if (basicAttackCount >= 1)
                    {
                        yield break;
                    }

                    break;

                default:
                    yield break;
            }

            // 행동이 MP를 소비하지 않으면 같은 판단이 무한 반복될 수 있으므로 턴을 종료한다.
            if (characterMP.CurrentMP >= mpBeforeAction)
            {
                yield break;
            }
        }

        Debug.LogWarning($"{name}: 행동 트리 최대 평가 횟수에 도달하여 턴을 종료합니다.", this);
    }

    /// <summary>이번 턴 첫 실제 행동(이동 또는 공격) 직전에 한 번만 카메라를 이 Enemy로 포커스하고
    /// 기존과 동일하게 0.2초 대기한다. 같은 턴에 이동 후 공격처럼 행동이 이어져도 카메라 포커스와
    /// 대기는 한 번만 발생한다.</summary>
    private IEnumerator BeginActionFocus(BattleCameraRig cameraRig)
    {
        if (ActedThisTurn)
        {
            yield break;
        }

        ActedThisTurn = true;
        cameraRig?.SetTemporaryFocus(transform);
        yield return new WaitForSecondsRealtime(0.2f);
    }

    /// <summary>DB에 설정된 기본 공격 피해량으로 공용 피해 서비스를 호출한다. 대상이 없으면 아무 일도 하지 않는다.</summary>
    private void ApplyBasicAttackDamage(Transform target)
    {
        if (target == null)
        {
            return;
        }

        float damage = runtimeData != null && runtimeData.Data != null
            ? Mathf.Max(0f, runtimeData.Data.attackDamage)
            : 0f;
        if (damage <= 0f)
        {
            return;
        }

        BattleTransformMovement.FaceTowards(transform, target.position);
        BattleCharacterAnimationBridge.PlayAttack(gameObject);
        actionExecutor.TryApplyBasicAttackDamage(gameObject, target.gameObject, damage);
    }

    /// <summary>기억 중인 Target을 우선 사용하고, 없으면 Detector의 직접 감지 결과를 확인한다.</summary>
    private Transform ResolveTarget()
    {
        Transform tauntTarget = BattleScarecrowSummon.FindNearest(transform.position);
        return tauntTarget != null ? tauntTarget : ResolveNormalTarget();
    }

    private Transform ResolveNormalTarget()
    {
        if (awareness != null && awareness.HasTarget)
        {
            return awareness.Target;
        }

        Transform detectedTarget = detector != null ? detector.PlayerTarget : null;
        if (detectedTarget != null && detector.CanDetectPlayer(detectedTarget))
        {
            awareness?.SetTarget(detectedTarget);
            return detectedTarget;
        }

        return null;
    }

    /// <summary>DB에 설정된 타일당 이동 MP 비용을 반환하며 DB가 없으면 1을 사용한다.</summary>
    private int GetMoveCostPerTile()
    {
        int cost = runtimeData != null && runtimeData.Data != null
            ? Mathf.Max(1, runtimeData.Data.moveMPCostPerTile)
            : 1;
        BattleStatusEffects status = GetComponent<BattleStatusEffects>();
        return status != null ? status.ModifyMoveCost(cost) : cost;
    }

    private void RollTurnMP()
    {
        if (characterMP == null) return;

        BattleEnemyData data = runtimeData != null ? runtimeData.Data : null;
        if (data == null)
        {
            characterMP.RestoreFull();
            return;
        }

        int minimum = Mathf.Clamp(data.minTurnMP, 0, characterMP.MaxMP);
        int maximum = Mathf.Clamp(data.maxTurnMP, minimum, characterMP.MaxMP);
        int turnMP = Random.Range(minimum, maximum + 1);
        characterMP.SetCurrentMP(turnMP);
        Debug.Log($"{name}: turn MP rolled {turnMP} ({minimum}-{maximum})", this);
    }

    /// <summary>플레이어 턴 시작 시 다음 적 턴에 사용할 MP를 한 번만 결정한다.</summary>
    public void PrepareNextTurnMP()
    {
        ResolveComponents();
        RollTurnMP();
    }

    /// <summary>이번 턴 기본 공격 순번에 따라 기본 비용을 누적 배율로 계산한다.</summary>
    private int GetBasicAttackCost(int successfulAttackCount)
    {
        int baseCost = runtimeData != null && runtimeData.Data != null
            ? Mathf.Max(0, runtimeData.Data.basicAttackMPCost)
            : 1;
        int cost = BattleAttackCostService.CalculateRepeatedAttackCost(
            baseCost,
            successfulAttackCount);
        BattleStatusEffects status = GetComponent<BattleStatusEffects>();
        return status != null ? status.ModifyAttackCost(cost) : cost;
    }

    private void ResolveBattleDataPool()
    {
        if (battleDataPool == null)
        {
            battleDataPool = FindFirstObjectByType<BattleDataPool>(FindObjectsInactive.Include);
        }
    }

    /// <summary>런타임 자동 부착 순서와 무관하게 필요한 컴포넌트 참조를 다시 확보한다.</summary>
    private void ResolveComponents()
    {
        if (detector == null)
            detector = GetComponentInChildren<EnemyDetector>();
        if (awareness == null)
            awareness = GetComponent<EnemyAwareness>();
        if (pathDebugView == null)
            pathDebugView = GetComponent<PathDebugView>();
        characterMP = BattleComponentResolver.GetOrAdd(gameObject, characterMP);
        if (runtimeData == null)
            runtimeData = GetComponent<BattleEnemyRuntimeData>();
        if (behaviorTree == null)
            behaviorTree = EnemyBehaviorTreeFactory.CreateAggressiveTree();
        actionExecutor = BattleComponentResolver.GetOrAdd(gameObject, actionExecutor);
        mapContext = BattleComponentResolver.GetOrAdd(gameObject, mapContext);

        actionExecutor.Configure(characterMP, secondsPerTile);
    }
}
