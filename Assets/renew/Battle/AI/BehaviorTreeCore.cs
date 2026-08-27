using System.Collections.Generic;

/// <summary>
/// 행동 트리 노드 한 개를 평가한 결과다.
/// Success는 이 조건 또는 행동이 완료됐다는 뜻이고, Failure는 다음 후보 행동을 검사하라는 뜻이다.
/// Running은 여러 프레임에 걸친 행동이 아직 끝나지 않았다는 뜻이지만 현재 Enemy 트리는 즉시 판단만 하므로 사용하지 않는다.
/// </summary>
public enum BehaviorNodeState
{
    /// <summary>조건을 만족했거나 판단 기록을 완료했다.</summary>
    Success,
    /// <summary>조건을 만족하지 못했으므로 Selector가 다음 행동 후보를 검사한다.</summary>
    Failure,
    /// <summary>행동이 진행 중이다. 현재 판단 전용 트리에서는 반환하지 않는다.</summary>
    Running
}

/// <summary>모든 Behavior Tree 노드가 공유하는 추상 기반 클래스.</summary>
public abstract class BehaviorNode
{
    /// <summary>
    /// EnemyTurnPlanner가 만든 현재 판단 문맥을 평가해 성공, 실패, 진행 중 상태 중 하나를 반환한다.
    /// 노드는 이동·공격을 직접 실행하지 않으며 Context의 Decision 기록까지만 담당한다.
    /// </summary>
    public abstract BehaviorNodeState Evaluate(EnemyAIContext context);
}

/// <summary>자식 중 처음으로 실패하지 않은 결과를 반환하는 우선순위 선택 노드.</summary>
public sealed class SelectorNode : BehaviorNode
{
    private readonly IReadOnlyList<BehaviorNode> children;

    public SelectorNode(params BehaviorNode[] children)
    {
        // 배열 순서를 행동 우선순위로 그대로 보존한다. 공격 Sequence가 이동 Sequence보다 앞에 있으면 공격 우선 AI가 된다.
        this.children = children;
    }

    /// <summary>등록 순서대로 자식을 평가하므로 배열 순서가 곧 행동 우선순위다.</summary>
    public override BehaviorNodeState Evaluate(EnemyAIContext context)
    {
        foreach (BehaviorNode child in children)
        {
            // 앞 후보가 실패했을 때만 다음 후보를 검사한다. 성공/진행 중이면 그 결과가 현재 선택이다.
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
        // 조건 노드들과 마지막 Decision 기록 노드를 전달받은 순서 그대로 실행한다.
        this.children = children;
    }

    /// <summary>하나라도 실패하거나 실행 중이면 즉시 해당 결과를 반환한다.</summary>
    public override BehaviorNodeState Evaluate(EnemyAIContext context)
    {
        foreach (BehaviorNode child in children)
        {
            // 하나라도 실패하면 이 행동 후보 전체가 불가능하다. 모두 성공해야 마지막 행동 결정까지 도달한다.
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
    /// <summary>아직 행동 트리가 결정을 기록하지 않았다.</summary>
    None,
    /// <summary>대상에게 접근하도록 이동한다.</summary>
    Move,
    /// <summary>현재 위치에서 기억 중인 대상을 기본 공격한다.</summary>
    Attack,
    /// <summary>Enemy 전용 스킬을 사용한다. 현재 공용 공격형 트리에는 연결되지 않았다.</summary>
    UseSkill,
    /// <summary>공격과 이동 모두 불가능하므로 이번 행동을 종료한다.</summary>
    Wait
}
