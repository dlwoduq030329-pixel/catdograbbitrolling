/// <summary>유효한 공격/추격 대상이 있는지 검사한다.</summary>
public sealed class HasTargetNode : BehaviorNode
{
    /// <summary>현재 적이 추적할 대상을 기억하고 있는지 검사한다.</summary>
    public override BehaviorNodeState Evaluate(EnemyAIContext context)
    {
        return context.Target != null ? BehaviorNodeState.Success : BehaviorNodeState.Failure;
    }
}

/// <summary>계산된 경로 길이가 공격 사거리 안인지 검사한다.</summary>
public sealed class CanAttackNode : BehaviorNode
{
    /// <summary>대상이 공격 사거리 안에 있고 공격 행동력 비용을 지불할 수 있는지 검사한다.</summary>
    public override BehaviorNodeState Evaluate(EnemyAIContext context)
    {
        if (context.Path == null)
        {
            return BehaviorNodeState.Failure;
        }

        return context.Path.Count <= context.AttackRangeTiles &&
               context.CurrentMP >= context.BasicAttackMPCost
            ? BehaviorNodeState.Success
            : BehaviorNodeState.Failure;
    }
}

/// <summary>대상이 사거리 밖에 있어 접근 이동이 필요한지 검사한다.</summary>
public sealed class CanMoveNode : BehaviorNode
{
    /// <summary>대상까지 이동할 경로와 최소 이동 행동력이 남아 있는지 검사한다.</summary>
    public override BehaviorNodeState Evaluate(EnemyAIContext context)
    {
        return context.Path != null &&
               context.Path.Count > context.AttackRangeTiles &&
               context.CurrentMP >= context.MoveMPCost
            ? BehaviorNodeState.Success
            : BehaviorNodeState.Failure;
    }
}

/// <summary>게임 행동을 직접 수행하지 않고 Context에 선택 결과만 기록한다.</summary>
public sealed class DecideActionNode : BehaviorNode
{
    private readonly EnemyAIDecision decision;

    public DecideActionNode(EnemyAIDecision decision)
    {
        this.decision = decision;
    }

    /// <summary>생성 시 받은 공격, 이동, 대기 판단을 문맥에 기록해 실행 담당자에게 전달한다.</summary>
    public override BehaviorNodeState Evaluate(EnemyAIContext context)
    {
        context.Decision = decision;
        return BehaviorNodeState.Success;
    }
}

/// <summary>공용 노드를 AI Profile별 우선순위로 조립하는 팩토리.</summary>
public static class EnemyBehaviorTreeFactory
{
    /// <summary>공격 가능 여부를 먼저 보고, 불가능하면 이동, 그마저 불가능하면 대기하는 공용 트리를 만든다.</summary>
    public static BehaviorNode CreateAggressiveTree()
    {
        // 공격 가능하면 공격, 아니면 추격, 둘 다 불가능하면 대기한다.
        return new SelectorNode(
            new SequenceNode(
                new HasTargetNode(),
                new CanAttackNode(),
                new DecideActionNode(EnemyAIDecision.Attack)),
            new SequenceNode(
                new HasTargetNode(),
                new CanMoveNode(),
                new DecideActionNode(EnemyAIDecision.Move)),
            new DecideActionNode(EnemyAIDecision.Wait));
    }
}
