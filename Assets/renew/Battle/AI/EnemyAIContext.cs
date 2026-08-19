using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 번의 Behavior Tree 평가에 필요한 읽기 전용 상황 데이터다.
/// 노드는 게임 오브젝트를 직접 탐색하지 않고 이 Context만 보고 판단한다.
/// </summary>
public sealed class EnemyAIContext
{
    public Transform Self { get; }
    public Transform Target { get; }
    public MapInfo CurrentTile { get; }
    public MapInfo TargetTile { get; }
    public IReadOnlyList<MapInfo> Path { get; }
    public int AttackRangeTiles { get; }
    public int CurrentMP { get; }
    public int MoveMPCost { get; }
    public int BasicAttackMPCost { get; }
    /// <summary>Action 노드가 기록하고 EnemyTurnActor가 실행하는 최종 판단.</summary>
    public EnemyAIDecision Decision { get; set; }

    public EnemyAIContext(
        Transform self,
        Transform target,
        MapInfo currentTile,
        MapInfo targetTile,
        IReadOnlyList<MapInfo> path,
        int attackRangeTiles,
        int currentMP,
        int moveMPCost,
        int basicAttackMPCost)
    {
        Self = self;
        Target = target;
        CurrentTile = currentTile;
        TargetTile = targetTile;
        Path = path;
        AttackRangeTiles = Mathf.Max(1, attackRangeTiles);
        CurrentMP = Mathf.Max(0, currentMP);
        MoveMPCost = Mathf.Max(1, moveMPCost);
        BasicAttackMPCost = Mathf.Max(0, basicAttackMPCost);
        Decision = EnemyAIDecision.None;
    }
}
