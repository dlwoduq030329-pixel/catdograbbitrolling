using UnityEngine;

/// <summary>
/// EnemyTurnActor의 "이번 턴에 누구를 쫓을 대상으로 볼지" 결정 부분만 따로 뗀 partial 조각이다.
/// TakeTurn(본체는 EnemyTurnActor.cs)이 매 평가마다 ResolveTarget()을 호출해서, null이면 배회
/// 분기로 빠지고 값이 있으면 그 대상으로 EnemyTurnPlanner를 돌린다.
///
/// 2026-09-05: 이 파일을 따로 뗀 이유는 순수 파일 길이 때문이 아니라, 나중에 Enemy/NPC 파이프라인을
/// 분리할 때 정확히 여기가 갈라지는 지점이기 때문이다 — Enemy는 도발(허수아비) → EnemyAwareness가
/// 기억한 대상 → EnemyDetector가 감지한 대상 순으로 실제 전투 대상을 찾지만, NPC는 애초에 싸우지
/// 않으므로 ResolveTarget이 항상 null만 반환하면 된다. TakeTurn 쪽 switch/분기 구조를 전혀 건드리지
/// 않고 이 파일의 구현만 바꿔치기(또는 override)하면 NPC가 자연스럽게 배회 전용 유닛이 되는 구조를
/// 노린 것이다(인수인계 설계 메모 참고).
/// </summary>
public partial class EnemyTurnActor
{
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
}
