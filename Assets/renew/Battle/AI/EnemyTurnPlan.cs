using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy가 한 번의 행동 판단에서 결정한 대상·경로·비용·예상 도착지를 보관한다.
/// 실제 Enemy 턴과 이동 전 위험 Preview가 같은 계획 생성 결과를 사용하게 만드는 공용 데이터다.
/// 이 객체는 판단 결과만 보관하며 Transform 이동, MP 차감, 피해 적용은 수행하지 않는다.
/// </summary>
public sealed class EnemyTurnPlan
{
    /// <summary>이 계획을 실행하거나 Preview로 표시할 Enemy 본체.</summary>
    public EnemyTurnActor Actor { get; }
    /// <summary>이번 판단에서 공격하거나 추격하기로 선택한 대상.</summary>
    public Transform Target { get; }
    /// <summary>계획을 만들 당시 Enemy가 서 있던 출발 타일.</summary>
    public MapInfo StartTile { get; }
    /// <summary>대상이 있다고 가정한 타일. Preview에서는 Player가 이동할 후보 타일이 들어간다.</summary>
    public MapInfo TargetTile { get; }
    /// <summary>
    /// 출발 타일을 제외하고 대상 타일까지 순서대로 정렬된 최단 경로.
    /// 실제 이동은 이 목록 앞부분만 사용하고, 공격 계획은 거리 검증 근거로 사용한다.
    /// </summary>
    public IReadOnlyList<MapInfo> Path { get; }
    /// <summary>Behavior Tree가 최종 선택한 Attack, Move, Wait 등의 행동 종류.</summary>
    public EnemyAIDecision Decision { get; }
    /// <summary>Enemy가 이동 없이 대상을 공격할 수 있는 최대 타일 거리.</summary>
    public int AttackRangeTiles { get; }
    /// <summary>실제 이동 한 칸마다 소비할 MP.</summary>
    public int MoveMPCostPerTile { get; }
    /// <summary>기본 공격을 실행할 때 소비할 MP.</summary>
    public int BasicAttackMPCost { get; }
    /// <summary>
    /// 현재 MP와 공격 사거리를 반영해 이번 Move 행동에서 실제로 이동할 예정인 칸 수.
    /// Path 전체 길이가 아니라 공격 사거리 직전까지 필요한 경로만 계산한 값이다.
    /// </summary>
    public int PlannedMoveTileCount { get; }
    /// <summary>
    /// Move 계획 실행 후 도착할 것으로 계산된 타일. 이동하지 않는 Attack·Wait 계획은 StartTile이다.
    /// Preview가 추후 예상 도착 마커를 표시할 때 그대로 사용할 수 있다.
    /// </summary>
    public MapInfo PredictedDestinationTile { get; }

    /// <summary>현재 위치에서 이번 행동으로 바로 공격한다고 확정된 계획인지 반환한다.</summary>
    public bool WillAttack => Decision == EnemyAIDecision.Attack;
    /// <summary>대상을 향해 이동하도록 결정된 추격 계획인지 반환한다.</summary>
    public bool WillChase => Decision == EnemyAIDecision.Move;

    /// <summary>
    /// EnemyTurnPlanner가 계산을 모두 끝낸 뒤 결과를 변경 불가능한 계획 객체로 묶는다.
    /// 호출자는 이 객체의 값을 수정하지 않고 실제 행동 실행 또는 위험 Preview에 읽기 전용으로 사용한다.
    /// </summary>
    public EnemyTurnPlan(
        EnemyTurnActor actor,
        Transform target,
        MapInfo startTile,
        MapInfo targetTile,
        IReadOnlyList<MapInfo> path,
        EnemyAIDecision decision,
        int attackRangeTiles,
        int moveMPCostPerTile,
        int basicAttackMPCost,
        int plannedMoveTileCount,
        MapInfo predictedDestinationTile)
    {
        // Actor와 Target은 "누가 누구에게 행동하는지"를 보존한다.
        Actor = actor;
        Target = target;
        // 타일과 Path는 거리 판단, 실제 이동, 예상 도착 위치 표시가 공유하는 공간 정보다.
        StartTile = startTile;
        TargetTile = targetTile;
        Path = path;
        // Decision과 비용은 실행기가 어떤 행동을 얼마에 실행해야 하는지 알려준다.
        Decision = decision;
        AttackRangeTiles = attackRangeTiles;
        MoveMPCostPerTile = moveMPCostPerTile;
        BasicAttackMPCost = basicAttackMPCost;
        // 이동 결과 예측값은 Preview와 실제 이동 정지 위치를 일치시키는 데 사용한다.
        PlannedMoveTileCount = plannedMoveTileCount;
        PredictedDestinationTile = predictedDestinationTile;
    }
}
