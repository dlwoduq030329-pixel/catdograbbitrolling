using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 번의 Behavior Tree 평가에 필요한 읽기 전용 상황 데이터다.
/// 노드는 게임 오브젝트를 직접 탐색하지 않고 이 Context만 보고 판단한다.
/// </summary>
public sealed class EnemyAIContext
{
    /// <summary>이번 판단을 수행하는 Enemy Transform.</summary>
    public Transform Self { get; }
    /// <summary>EnemyAwareness가 기억하고 있는 현재 공격·추격 대상.</summary>
    public Transform Target { get; }
    /// <summary>판단 시작 시점 Enemy의 현재 타일.</summary>
    public MapInfo CurrentTile { get; }
    /// <summary>판단 시작 시점 Target이 점유한 타일.</summary>
    public MapInfo TargetTile { get; }
    /// <summary>CurrentTile을 제외하고 TargetTile까지 이어지는 최단 경로.</summary>
    public IReadOnlyList<MapInfo> Path { get; }
    /// <summary>이 거리 이내면 이동하지 않고 기본 공격할 수 있는 타일 수.</summary>
    public int AttackRangeTiles { get; }
    /// <summary>이번 판단 시점에 Enemy가 사용할 수 있는 MP.</summary>
    public int CurrentMP { get; }
    /// <summary>한 칸 이동에 필요한 MP.</summary>
    public int MoveMPCost { get; }
    /// <summary>기본 공격 한 번에 필요한 MP.</summary>
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
        // EnemyTurnPlanner가 경로 탐색과 비용 입력 준비를 먼저 끝낸 뒤 그 결과를 한 묶음으로 전달한다.
        // 행동 트리 노드는 Scene 검색이나 Component 조회 없이 이 스냅샷만 읽어 결정을 기록해야 한다.
        // Self/Target은 행동 주체와 대상, CurrentTile/TargetTile/Path는 공간 관계를 설명한다.
        Self = self;
        Target = target;
        CurrentTile = currentTile;
        TargetTile = targetTile;
        Path = path;
        // 비용과 사거리는 행동 트리가 비교할 때 음수·0 예외가 섞이지 않도록 Context 경계에서 한 번 정규화한다.
        AttackRangeTiles = Mathf.Max(1, attackRangeTiles);
        CurrentMP = Mathf.Max(0, currentMP);
        MoveMPCost = Mathf.Max(1, moveMPCost);
        BasicAttackMPCost = Mathf.Max(0, basicAttackMPCost);
        // Tree 평가 전에는 아직 어떤 행동도 선택되지 않았다. DecideActionNode만 이 값을 변경한다.
        Decision = EnemyAIDecision.None;
    }
}
