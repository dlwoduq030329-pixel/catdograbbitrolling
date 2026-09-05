using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 턴 한 번을 실행하는 행동 어댑터다.
/// BT에는 판단만 요청하고, 경로 이동과 타일별 MP 차감은 이 컴포넌트가 수행한다.
/// EnemyIdleBehavior/wanderRadiusTiles/wanderTilesPerTurn/wanderChance는 이 클래스가 새로 만들지 않고
/// BattleEnemyDatabase.cs에 이미 있던 BattleEnemyData의 같은 이름 필드(runtimeData.Data)를 그대로 읽는다
/// (2026-09-04: 배회 행동 자체가 미구현이라는 주석이 있던 그 필드들을 여기서 실제로 구현했다).
///
/// 2026-09-05: 파일 하나가 너무 길어져서(원래 580줄대) 같은 클래스를 C# partial class로 4개 파일에
/// 나눠 담았다 — 로직·필드 접근 범위는 그대로고 어느 파일에 물리적으로 있는지만 바뀐다.
/// 이 파일(EnemyTurnActor.cs)에는 핵심 턴 진행(TakeTurn)과 컴포넌트/풀 확보만 남기고, 배회(idle
/// wander) 관련 코드는 EnemyTurnActor.Wander.cs로, MP·공격 비용 계산은 EnemyTurnActor.Costs.cs로,
/// "이번 턴 쫓을 대상을 누구로 볼지"(ResolveTarget/ResolveNormalTarget)는 EnemyTurnActor.Targeting.cs로
/// 옮겼다. 타겟팅을 따로 뗀 이유는 나중에 NPC를 지원할 때 정확히 이 부분만 갈아끼우면 되기
/// 때문이다 — NPC는 이 파일을 건드리지 않고 ResolveTarget이 항상 null만 반환하게 하면, TakeTurn의
/// "target == null → 배회 분기"가 그대로 타서 자연스럽게 NPC 이동으로 이어진다(설계 논의 참고).
/// 네 파일 다 반드시 같은 GameObject의 같은 컴포넌트로 합쳐지며, 컴파일 시 한 클래스로 병합된다.
/// </summary>
public partial class EnemyTurnActor : MonoBehaviour
{
    [Header("적 이동 및 공격 설정")]
    [InspectorName("한 칸 이동 시간")]
    [SerializeField, Min(0.01f)] private float secondsPerTile = 0.2f;
    [InspectorName("공격 사거리 (칸)")]
    [SerializeField, Min(1)] private int attackRangeTiles = 1;
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

        // 2026-09-05: 기절·속박을 BattleEnemyControlState 전용 필드가 아니라 BattleStatusEffects(공용
        // 상태이상 저장소)에서 직접 읽도록 통합했다(ControlState 클래스는 삭제됨). Player와 완전히 같은
        // 저장소·같은 감소 시점(플레이어 턴 시작마다)을 쓰므로 표시값과 실제 판정이 항상 일치한다.
        BattleStatusEffects statusEffects = GetComponent<BattleStatusEffects>();
        if (statusEffects != null && statusEffects.Has(BattleStatusType.Stun))
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
                GetBasicAttackCost(),
                out plan))
        {
            return false;
        }

        // 속박은 공격은 허용하지만 이동은 막는다. 실제 TakeTurn과 같은 규칙으로 Move 예측만 제거한다.
        return statusEffects == null || !statusEffects.Has(BattleStatusType.Root) || !plan.WillChase;
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
        // 2026-09-05: 예전에는 BattleEnemyControlState가 기절·속박 지속 턴을 따로 들고 있다가 이 Enemy의
        // 공식 턴이 돌 때마다 한 번씩 스스로 깎았다(ConsumeTurn). 지금은 BattleStatusEffects(Player와
        // 공유하는 공용 상태이상 저장소) 하나만 쓰고, 감소는 BattleGameManager가 플레이어 턴 시작마다
        // 호출하는 BattleStatusEffects.ProcessAllPlayerTurnStart()에서 다른 상태이상(독·화상 등)과 함께
        // 똑같이 처리되므로, 여기서는 더 이상 턴을 깎지 않고 현재 상태만 읽는다.
        BattleStatusEffects statusEffects = GetComponent<BattleStatusEffects>();
        bool isRooted = statusEffects != null && statusEffects.Has(BattleStatusType.Root);
        if (statusEffects != null && statusEffects.Has(BattleStatusType.Stun))
        {
            Debug.Log($"{name}: 기절 상태로 이번 턴을 행동하지 않습니다.", this);
            yield break;
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
            int basicAttackCost = GetBasicAttackCost();

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
