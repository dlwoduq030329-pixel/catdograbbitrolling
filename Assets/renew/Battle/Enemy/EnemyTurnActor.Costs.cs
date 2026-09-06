using UnityEngine;

/// <summary>
/// EnemyTurnActor의 "MP·행동력 비용 계산" 부분만 따로 뗀 partial 조각이다. 이동 비용, 턴 시작 MP
/// 굴리기, 기본 공격 비용 계산이 전부 여기 모여 있다. 파일만 분리했을 뿐 로직·필드 접근 방식은
/// 원래 EnemyTurnActor.cs에 있던 것과 동일하다(C# partial class로 한 클래스를 여러 파일에 나눠 담은
/// 것 — 컴파일되면 완전히 같은 클래스로 합쳐진다).
/// </summary>
public partial class EnemyTurnActor
{
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

    /// <summary>
    /// DB(runtimeData.Data)에 설정된 기본 공격 MP 비용에 상태이상 보정만 적용해 반환한다.
    /// 2026-09-05 정리: 예전에는 같은 턴 안에서 기본 공격을 반복할수록(successfulAttackCount) 비용이
    /// (횟수+1)배로 커지는 점진적 증가 계산(BattleAttackCostService.CalculateRepeatedAttackCost)을
    /// 거쳤는데, 현재 TakeTurn은 기본 공격 1회 성공 시 바로 턴을 끝내므로(EnemyTurnActor.cs의 switch
    /// Attack 분기, basicAttackCount>=1 -> yield break) successfulAttackCount가 0보다 커지는 경우가
    /// 실제로 없어 그 배율 로직이 항상 baseCost 그대로를 돌려주는 것과 같았다(도달 불가 코드). 그래서
    /// 파라미터와 그 계산을 걷어내고 baseCost를 바로 반환하도록 정리했다. 같은 이유로 죽어 있던
    /// EnemyTurnActor.cs의 maxBasicAttacksPerTurn 필드도 함께 제거했다. 턴당 여러 번 공격을 허용하는
    /// 기믹 Enemy를 나중에 만들 때는, 반복 횟수를 실제로 세는 카운터와 함께 이 배율 계산을 다시
    /// 살리는 걸 검토할 것(git 이력에 원래 구현이 남아 있다).
    /// </summary>
    private int GetBasicAttackCost()
    {
        int baseCost = runtimeData != null && runtimeData.Data != null
            ? Mathf.Max(0, runtimeData.Data.basicAttackMPCost)
            : 1;
        BattleStatusEffects status = GetComponent<BattleStatusEffects>();
        return status != null ? status.ModifyAttackCost(baseCost) : baseCost;
    }
}
