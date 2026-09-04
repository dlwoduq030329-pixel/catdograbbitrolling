using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 턴 한 번을 실행하는 행동 어댑터다.
/// BT에는 판단만 요청하고, 경로 이동과 타일별 MP 차감은 이 컴포넌트가 수행한다.
/// EnemyIdleBehavior/wanderRadiusTiles/wanderTilesPerTurn/wanderChance는 이 클래스가 새로 만들지 않고
/// BattleEnemyDatabase.cs에 이미 있던 BattleEnemyData의 같은 이름 필드(runtimeData.Data)를 그대로 읽는다
/// (2026-09-04: 배회 행동 자체가 미구현이라는 주석이 있던 그 필드들을 여기서 실제로 구현했다).
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
    [InspectorName("Attack Damage Type")]
    [SerializeField] private BattleDamageType attackDamageType = BattleDamageType.Physical;
    [InspectorName("Movement Type")]
    [SerializeField] private EnemyIdleBehavior idleBehavior = EnemyIdleBehavior.Stationary;

    // Player의 BattlePlayerActionController(jumpTakeoffDelaySeconds/jumpArcHeight)와 같은 역할이다.
    // 2026-09-04: Player처럼 단차 이동 시 방향 전환 후 점프하는 연출을 Enemy에도 옮겨달라는 요청으로 추가,
    // 실제 점프 판정/연출 자체는 BattleEnemyActionExecutor.MoveOneTile이 담당하고 여기서는 튜닝 값만 전달한다.
    [Header("적 점프 이동 연출")]
    [InspectorName("점프 이륙 대기시간(초)")]
    [SerializeField, Min(0f)] private float jumpTakeoffDelaySeconds = 0.1f;
    [InspectorName("점프 최고 높이")]
    [SerializeField, Min(0f)] private float jumpArcHeight = 1.5f;
     
    /// <summary>배회 중 원래 자리를 기억해 wanderRadiusTiles 밖으로 못 벗어나게 하는 기준점.
    /// 첫 배회 시도 시점의 타일로 한 번만 설정되고 이후 바뀌지 않는다(스폰 위치 근사).</summary>
    private MapInfo homeTile;
    private float focusLeadInSeconds = 0.5f;
    private float attackImpactDelaySeconds = 0.3f;
    private float afterActionSeconds = 0.4f;

    /// <summary>Player R 토글 등 외부에서 이 Enemy의 실제 공격 사거리(칸)를 읽을 때 사용한다.</summary>
    public int AttackRangeTiles => attackRangeTiles;

    /// <summary>EnemyTurnRunner의 공용 페이싱 값을 이동 실행기와 행동 전후 대기에 적용한다.</summary>
    public void ConfigurePacing(
        float moveSecondsPerTile,
        float focusSeconds,
        float impactDelaySeconds,
        float actionHoldSeconds)
    {
        secondsPerTile = Mathf.Max(0.01f, moveSecondsPerTile);
        focusLeadInSeconds = Mathf.Max(0f, focusSeconds);
        attackImpactDelaySeconds = Mathf.Max(0f, impactDelaySeconds);
        afterActionSeconds = Mathf.Max(0f, actionHoldSeconds);
    }

    /// <summary>이번 TakeTurn 호출에서 실제로 이동하거나 공격했는지 여부. 카메라는 이 값이
    /// true가 되는 시점(첫 실제 행동 직전)에만 포커스를 옮긴다. 대상이 없거나, 기절/속박으로
    /// 아무 것도 못 했거나, 행동 트리가 대기를 선택한 경우는 false로 남는다.</summary>
    public bool ActedThisTurn { get; private set; }

    // 아래 9개는 전부 이 Enemy 하나가 자기 턴을 처리하는 데 필요한 "런타임 참조"다(공용/싱글턴 아님).
    // detector: 시야 감지(EnemyDetector, 자식 오브젝트에 있을 수 있음). awareness: 감지 후 기억하는 Target.
    // pathDebugView: 이동 경로 디버그 표시. characterMP: 이 Enemy의 MP. runtimeData: BattleEnemyData(공격력 등 원본 스탯)로 가는 다리.
    // behaviorTree: 이동/공격 중 무엇을 할지 판단하는 공용 행동 트리 인스턴스. battleDataPool: 현재 전투의 타일/유닛 풀(Enemy 전체가 공유).
    // actionExecutor: 실제 이동/기본공격 실행기. mapContext: 타일 조회/다른 Enemy 점유 타일 조회.
    private EnemyDetector detector;
    private EnemyAwareness awareness;
    private PathDebugView pathDebugView;
    private BattleUnitMP characterMP;
    private BattleEnemyRuntimeData runtimeData;
    private BehaviorNode behaviorTree;
    private BattleDataPool battleDataPool;
    private BattleEnemyActionExecutor actionExecutor;
    private BattleEnemyMapLookup mapContext;

    /// <summary>Spawner가 선택한 DB 데이터 중 행동 판단에 필요한 값을 적용한다.</summary>
    public void ConfigureFromData(BattleEnemyData data)
    {
        if (data == null)
        {
            return;
        }

        attackRangeTiles = Mathf.Max(1, data.attackRangeTiles);
        attackDamageType = data.attackDamageType;
        idleBehavior = data.idleBehavior;
    }

    /// <summary>
    /// Player가 후보 타일로 이동했다고 가정했을 때 이 Enemy가 다음 턴에 선택할 행동을 계산한다.
    /// 실제 TakeTurn과 동일한 행동 트리·현재 MP·이동 비용·공격 비용·점유 타일을 사용하므로,
    /// Preview 전용 추정 공식과 실제 AI 판단이 서로 달라지는 문제를 막는다.
    /// 이 함수는 상태이상 턴을 소비하거나 이동·공격을 실행하지 않는다.
    /// </summary>
    public bool TryPredictResponseToPlayerTile(MapInfo hypotheticalPlayerTile, out EnemyTurnPlan plan)
    {
        plan = null;
        if (hypotheticalPlayerTile == null)
        {
            return false;
        }

        ResolveComponents();
        ResolveBattleDataPool();

        // 어그로를 기억하거나 감지한 Player만 예측 대상으로 사용한다.
        // 허수아비 도발은 Player 이동 위험 표시와 별도 규칙이므로 여기서는 일반 대상을 읽는다.
        Transform playerTarget = ResolveNormalTarget();
        if (playerTarget == null || characterMP == null)
        {
            return false;
        }

        BattleEnemyControlState controlState = GetComponent<BattleEnemyControlState>();
        if (controlState != null && controlState.StunTurns > 0)
        {
            // 기절 중인 Enemy는 다음 자기 턴에 행동하지 않으므로 공격·추격 표시를 만들지 않는다.
            return false;
        }

        IReadOnlyList<MapInfo> mapTiles = mapContext.GetMapTiles(battleDataPool);
        MapInfo enemyTile = MapPathfinder.FindClosestTile(transform.position, mapTiles);
        HashSet<MapInfo> occupiedTiles = mapContext.FindOtherEnemyTiles(battleDataPool, this, mapTiles);
        if (!EnemyTurnPlanner.TryCreatePlan(
                this,
                behaviorTree,
                playerTarget,
                enemyTile,
                hypotheticalPlayerTile,
                occupiedTiles,
                attackRangeTiles,
                characterMP.CurrentMP,
                GetMoveCostPerTile(),
                GetBasicAttackCost(0),
                out plan))
        {
            return false;
        }

        // 속박은 공격은 허용하지만 이동은 막는다. 실제 TakeTurn과 같은 규칙으로 Move 예측만 제거한다.
        return controlState == null || controlState.RootTurns <= 0 || !plan.WillChase;
    }

    /// <summary>적 행동에 필요한 감지, 인식, 경로 표시 참조와 공용 행동 트리를 준비한다.
    /// detector는 GetComponentInChildren로 찾는다 — EnemyDetector가 루트가 아니라 "eyePoint"를 가진
    /// 자식 오브젝트에 붙는 프리팹 구조를 전제로 하기 때문이다(EnemySpawner.cs의 EnemyDetector 확보 로직과 동일한 이유).</summary>
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
        // [1. 턴 실행 준비] 런타임 참조와 공용 Registry를 확보하고 지난 턴의 행동 여부를 초기화한다.
        ResolveComponents();
        ResolveBattleDataPool();
        ActedThisTurn = false;
        BattleEnemyControlState controlState = GetComponent<BattleEnemyControlState>();
        bool isRooted = false;
        if (controlState != null)
        {
            // 상태 지속 턴은 Enemy 자신의 공식 턴 시작 시 한 번만 소비한다. Preview는 이 함수를 호출하지 않는다.
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
            // [2. 현재 상태 재판단] 이동 후 남은 MP로 공격할 수 있으므로 한 행동이 끝날 때마다 새 계획을 만든다.
            Transform target = ResolveTarget();
            if (target == null)
            {
                pathDebugView?.Clear();
                if (GetIdleBehavior() == EnemyIdleBehavior.Wander)
                {
                    yield return TryWanderStep(cameraRig);
                }
                yield break;
            }

            IReadOnlyList<MapInfo> mapTiles = mapContext.GetMapTiles(battleDataPool);
            MapInfo startTile = MapPathfinder.FindClosestTile(transform.position, mapTiles);
            MapInfo targetTile = MapPathfinder.FindClosestTile(target.position, mapTiles);
            HashSet<MapInfo> occupiedTiles =
                mapContext.FindOtherEnemyTiles(battleDataPool, this, mapTiles);
            int moveCostPerTile = GetMoveCostPerTile();
            int basicAttackCost = GetBasicAttackCost(basicAttackCount);

            bool planCreated = EnemyTurnPlanner.TryCreatePlan(
                this,
                behaviorTree,
                target,
                startTile,
                targetTile,
                occupiedTiles,
                attackRangeTiles,
                characterMP.CurrentMP,
                moveCostPerTile,
                basicAttackCost,
                out EnemyTurnPlan currentPlan);

            if (!planCreated && BattleScarecrowSummon.IsScarecrow(target))
            {
                // 허수아비에 접근할 수 없으면 도발 때문에 턴을 버리지 않고 원래 Player를 노린다.
                target = ResolveNormalTarget();
                targetTile = target != null ? MapPathfinder.FindClosestTile(target.position, mapTiles) : null;
                planCreated = EnemyTurnPlanner.TryCreatePlan(
                    this,
                    behaviorTree,
                    target,
                    startTile,
                    targetTile,
                    occupiedTiles,
                    attackRangeTiles,
                    characterMP.CurrentMP,
                    moveCostPerTile,
                    basicAttackCost,
                    out currentPlan);
            }

            if (!planCreated)
            {
                Debug.LogWarning($"{name}: 플레이어까지 이어지는 경로를 찾지 못했습니다.", this);
                pathDebugView?.Clear();
                yield break;
            }

            // 디버그 경로, 실제 이동과 공격 분기가 모두 같은 계획 객체를 읽는다.
            pathDebugView?.DrawPath(transform.position, currentPlan.Path);
            int mpBeforeAction = characterMP.CurrentMP;

            // [3. 계획 실행] Planner는 계산만 했으므로 여기서 결정 종류에 맞는 Executor 작업을 호출한다.
            switch (currentPlan.Decision)
            {
                case EnemyAIDecision.Move:
                    if (isRooted)
                    {
                        Debug.Log($"{name}: 속박 상태라 이번 턴에 이동할 수 없습니다.", this);
                        yield break;
                    }
                    yield return BeginActionFocus(cameraRig);
                    yield return actionExecutor.MoveAlongPath(
                        currentPlan.Path,
                        currentPlan.StartTile,
                        currentPlan.AttackRangeTiles,
                        currentPlan.MoveMPCostPerTile);
                    break;

                case EnemyAIDecision.Attack:
                    if (!actionExecutor.TrySpendActionMP(basicAttackCost))
                    {
                        yield break;
                    }

                    yield return BeginActionFocus(cameraRig);
                    basicAttackCount++;
                    yield return PlayBasicAttackAndApplyDamage(target);
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
                        if (afterActionSeconds > 0f)
                            yield return new WaitForSecondsRealtime(afterActionSeconds);
                        yield break;
                    }

                    break;

                default:
                    yield break;
            }

            if (currentPlan.Decision == EnemyAIDecision.Move && afterActionSeconds > 0f)
                yield return new WaitForSecondsRealtime(afterActionSeconds);

            // 행동이 MP를 소비하지 않으면 같은 판단이 무한 반복될 수 있으므로 턴을 종료한다.
            if (characterMP.CurrentMP >= mpBeforeAction)
            {
                yield break;
            }
        }

        Debug.LogWarning($"{name}: 행동 트리 최대 평가 횟수에 도달하여 턴을 종료합니다.", this);
    }

    /// <summary>attackRangeTiles/attackDamageType과 같은 패턴으로, ConfigureFromData가 이미 복사해 둔
    /// idleBehavior 필드를 그대로 반환한다(= 원본 BattleEnemyData.idleBehavior와 같은 값이면서, Play 모드
    /// Inspector의 "Movement Type"에서 지금 이 Enemy가 정적/배회 중 뭔지 바로 확인할 수 있다).</summary>
    private EnemyIdleBehavior GetIdleBehavior()
    {
        return idleBehavior;
    }

    /// <summary>
    /// idleBehavior가 Wander이고 이번 턴 감지된 Player가 없을 때 호출된다. BattleEnemyData의
    /// wanderChance 확률로 이번 턴 배회 여부를 먼저 굴리고, 성공하면 wanderTilesPerTurn 만큼
    /// 한 칸씩 인접 타일로 이동한다. 이동 가능(IsWalkable), 다른 Enemy 비점유, homeTile(첫 배회 시점
    /// 위치, 스폰 지점 근사) 기준 wanderRadiusTiles 이내라는 세 조건을 모두 만족하는 타일만 후보로 삼는다.
    /// 매 스텝을 완전히 독립적으로 무작위 선택하면(2026-09-04 초기 구현) 방금 온 칸으로 바로 되돌아가는
    /// 왔다갔다 지그재그가 자주 나와 "멍청해 보인다"는 피드백을 받아, 직전에 있던 칸(previousTile)은
    /// 다른 후보가 있는 한 이번 스텝 후보에서 제외해 최소한 제자리 왕복은 피하게 했다.
    /// 목표를 향한 추격이 아니라 그냥 주변을 서성이는 용도라 공격 사거리 개념이 없고, 매 칸마다
    /// 후보가 없거나 MP가 부족해지면 그 자리에서 조용히 배회를 멈춘다.
    /// </summary>
    private IEnumerator TryWanderStep(BattleCameraRig cameraRig)
    {
        BattleEnemyData data = runtimeData != null ? runtimeData.Data : null;
        float wanderChance = data != null ? data.wanderChance : 0.6f;
        int wanderRadiusTiles = data != null ? data.wanderRadiusTiles : 3;
        int wanderTilesPerTurn = Mathf.Max(1, data != null ? data.wanderTilesPerTurn : 1);

        if (UnityEngine.Random.value > wanderChance)
        {
            // 배회 확률 실패 — 이번 턴은 그냥 가만히 있는다.
            yield break;
        }

        ResolveBattleDataPool();
        IReadOnlyList<MapInfo> mapTiles = mapContext.GetMapTiles(battleDataPool);
        MapInfo currentTile = MapPathfinder.FindClosestTile(transform.position, mapTiles);
        if (currentTile == null)
        {
            yield break;
        }

        // homeTile은 처음 배회를 시도한 그 위치로 한 번만 고정한다(스폰 지점 근사). 이후 계속 이 기준으로
        // wanderRadiusTiles를 재는데, 정확한 스폰 타일을 별도로 기억하지 않는 현재 구조에서 가장 단순하고
        // 안전한 근사치다(스폰 직후 첫 배회 시도 위치 = 스폰 위치와 사실상 같다).
        if (homeTile == null)
        {
            homeTile = currentTile;
        }

        int movedTiles = 0;
        MapInfo previousTile = null;
        for (int step = 0; step < wanderTilesPerTurn; step++)
        {
            HashSet<MapInfo> occupiedTiles = mapContext.FindOtherEnemyTiles(battleDataPool, this, mapTiles);

            List<MapInfo> wanderCandidates = new List<MapInfo>(4);
            List<MapInfo> wanderCandidatesExcludingBacktrack = new List<MapInfo>(4);
            MapInfo[] neighbours = { currentTile.Up, currentTile.Down, currentTile.Left, currentTile.Right };
            foreach (MapInfo neighbour in neighbours)
            {
                if (neighbour == null || !neighbour.IsWalkable || occupiedTiles.Contains(neighbour))
                {
                    continue;
                }

                int distanceFromHome =
                    Mathf.Abs(neighbour.Index.x - homeTile.Index.x) +
                    Mathf.Abs(neighbour.Index.y - homeTile.Index.y);
                if (distanceFromHome > wanderRadiusTiles)
                {
                    continue;
                }

                wanderCandidates.Add(neighbour);
                if (neighbour != previousTile)
                {
                    wanderCandidatesExcludingBacktrack.Add(neighbour);
                }
            }

            // 방금 있던 칸으로 되돌아가는 선택지는, 그것 말고 갈 곳이 아예 없을 때(막다른 길)만 허용한다.
            List<MapInfo> effectiveCandidates =
                wanderCandidatesExcludingBacktrack.Count > 0 ? wanderCandidatesExcludingBacktrack : wanderCandidates;

            if (effectiveCandidates.Count == 0)
            {
                break;
            }

            int moveCostPerTile = GetMoveCostPerTile();
            if (characterMP == null || characterMP.CurrentMP < Mathf.Max(1, moveCostPerTile))
            {
                break;
            }

            MapInfo destinationTile = effectiveCandidates[UnityEngine.Random.Range(0, effectiveCandidates.Count)];
            yield return BeginActionFocus(cameraRig);
            yield return actionExecutor.MoveToSingleTile(destinationTile, currentTile, moveCostPerTile);
            movedTiles++;
            previousTile = currentTile;
            currentTile = destinationTile;
        }

        if (movedTiles > 0 && afterActionSeconds > 0f)
            yield return new WaitForSecondsRealtime(afterActionSeconds);
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
        if (focusLeadInSeconds > 0f)
            yield return new WaitForSecondsRealtime(focusLeadInSeconds);
    }

    /// <summary>대상을 바라보고 공격 애니메이션을 먼저 재생한 뒤 타격 시점에 실제 피해를 적용한다.</summary>
    private IEnumerator PlayBasicAttackAndApplyDamage(Transform target)
    {
        if (target == null)
        {
            yield break;
        }

        float damage = runtimeData != null && runtimeData.Data != null
            ? Mathf.Max(0f, runtimeData.Data.attackDamage)
            : 0f;
        if (damage <= 0f)
        {
            yield break;
        }

        BattleUnitMotionAnimator.FaceTowards(transform, target.position);
        BattleCharacterAnimationBridge.PlayAttack(gameObject);
        if (attackImpactDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(attackImpactDelaySeconds);

        if (target == null || !target.gameObject.activeInHierarchy) yield break;
        actionExecutor.TryApplyBasicAttackDamage(gameObject, target.gameObject, damage, attackDamageType);
    }

    /// <summary>도발(허수아비) 대상이 있으면 최우선으로 반환하고, 없으면 평소 타겟팅(ResolveNormalTarget)으로
    /// 넘어간다. null 대비용 보험 코드가 아니라 "도발 우선순위"를 구현하는 실제 분기다.</summary>
    private Transform ResolveTarget()
    {
        Transform tauntTarget = BattleScarecrowSummon.FindNearest(transform.position);
        return tauntTarget != null ? tauntTarget : ResolveNormalTarget();
    }

    /// <summary>도발이 없을 때의 일반 타겟팅. awareness가 이미 기억 중인 Target이 있으면 그대로 쓰고,
    /// 없으면 detector가 직접 감지한 Player를 확인해 처음으로 발견됐다면 awareness에 기억시킨다.
    /// "해제"가 아니라 "결정/획득" 의미의 Resolve다.</summary>
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

    /// <summary>DB(runtimeData.Data)에 설정된 타일당 이동 MP 비용을 반환한다. runtimeData/Data가
    /// 비어 있는 것은 정상 상황이 아니므로, 기본값 1로 대체하되 경고 로그를 남겨 데이터 연결 누락을
    /// 바로 알아챌 수 있게 한다(사용자 요청: 보험 코드가 조용히 넘어가지 않도록).</summary>
    private int GetMoveCostPerTile()
    {
        int cost;
        if (runtimeData != null && runtimeData.Data != null)
        {
            cost = Mathf.Max(1, runtimeData.Data.moveMPCostPerTile);
        }
        else
        {
            cost = 1;
            Debug.LogWarning($"{name}: runtimeData/Data가 없어 이동 MP 비용을 기본값 1로 사용합니다.", this);
        }

        BattleStatusEffects status = GetComponent<BattleStatusEffects>();
        return status != null ? status.ModifyMoveCost(cost) : cost;
    }

    /// <summary>이번 적 턴에 사용할 MP를 data.minTurnMP~maxTurnMP 범위에서 매번 새로 무작위로 뽑는다
    /// (누적/회복이 아니라 매 턴 새 값으로 덮어씀). data가 없으면 MaxMP까지 전부 회복시킨다.</summary>
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

    /// <summary>같은 턴 안에서 기본 공격을 반복할수록(successfulAttackCount) 비용이 (횟수+1)배로 커지는
    /// 점진적 증가 계산이다. 이동 후 공격 비용을 다시 계산하는 코드가 아니다.
    /// 단, 현재 TakeTurn은 기본 공격 1회 성공 시 바로 턴을 끝내므로(위 switch의 Attack 분기, basicAttackCount>=1 -> yield break)
    /// successfulAttackCount가 0보다 커지는 경우가 실제로 없어 이 배율 로직은 현재 도달 불가 상태다.
    /// 같은 이유로 위쪽 maxBasicAttacksPerTurn 필드도 선언만 되어 있고 어디서도 읽히지 않는 죽은 설정이다.
    /// 턴당 여러 번 공격을 허용하는 기믹 Enemy를 만들 때 함께 정리해야 한다.</summary>
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

    /// <summary>Player가 아니라 씬에 하나 있는 공용 BattleDataPool(타일/유닛 데이터 풀)을 찾아 캐시한다.
    /// BattleEnemyTurnRunner.RunAll이 받는 battleDataPool을 TakeTurn까지 전달하지 않는 구조라서
    /// 이 Enemy가 각자 독립적으로 Find하는 것이며, Scene에 BattleDataPool이 하나뿐이라 결과적으로 같은
    /// 인스턴스로 귀결될 뿐 RunAll의 시그니처만으로는 이 사실이 보이지 않는다(BattleEnemyTurnRunner.cs 리뷰에서
    /// 이미 지적한 것과 같은 문제).</summary>
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

        actionExecutor.Configure(characterMP, secondsPerTile, jumpTakeoffDelaySeconds, jumpArcHeight);
    }
}
