using System.Collections.Generic;

/// <summary>행동 트리 노드의 성공, 실패, 실행 중 평가 결과.</summary>
public enum BehaviorNodeState
{
    Success,
    Failure,
    Running
}

/// <summary>모든 Behavior Tree 노드가 공유하는 추상 기반 클래스.</summary>
public abstract class BehaviorNode
{
    /// <summary>현재 적 행동 문맥을 평가해 성공, 실패, 진행 중 상태 중 하나를 반환한다.</summary>
    public abstract BehaviorNodeState Evaluate(EnemyAIContext context);
}

/// <summary>자식 중 처음으로 실패하지 않은 결과를 반환하는 우선순위 선택 노드.</summary>
public sealed class SelectorNode : BehaviorNode
{
    private readonly IReadOnlyList<BehaviorNode> children;

    public SelectorNode(params BehaviorNode[] children)
    {
        this.children = children;
    }

    /// <summary>등록 순서대로 자식을 평가하므로 배열 순서가 곧 행동 우선순위다.</summary>
    public override BehaviorNodeState Evaluate(EnemyAIContext context)
    {
        foreach (BehaviorNode child in children)
        {
            BehaviorNodeState state = child.Evaluate(context);
            if (state != BehaviorNodeState.Failure)
            {
                return state;
            }
        }

        return BehaviorNodeState.Failure;
    }
}

/// <summary>모든 자식이 성공해야 성공하는 조건 묶음 노드.</summary>
public sealed class SequenceNode : BehaviorNode
{
    private readonly IReadOnlyList<BehaviorNode> children;

    public SequenceNode(params BehaviorNode[] children)
    {
        this.children = children;
    }

    /// <summary>하나라도 실패하거나 실행 중이면 즉시 해당 결과를 반환한다.</summary>
    public override BehaviorNodeState Evaluate(EnemyAIContext context)
    {
        foreach (BehaviorNode child in children)
        {
            BehaviorNodeState state = child.Evaluate(context);
            if (state != BehaviorNodeState.Success)
            {
                return state;
            }
        }

        return BehaviorNodeState.Success;
    }
}

/// <summary>BT가 판단한 행동 종류. 실제 행동 실행은 EnemyTurnActor가 담당한다.</summary>
public enum EnemyAIDecision
{
    None,
    Move,
    Attack,
    UseSkill,
    Wait
}
