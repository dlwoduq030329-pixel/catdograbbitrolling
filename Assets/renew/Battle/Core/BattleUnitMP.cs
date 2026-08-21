using System;
using UnityEngine;

/// <summary>
/// 전투 유닛의 최대 MP와 현재 MP를 보관하고 소비 결과를 알리는 런타임 컴포넌트다.
/// 최대 MP 계산은 소유하지 않으며 Player/Enemy 데이터가 계산한 최종값만 전달받는다.
/// </summary>
public sealed class BattleUnitMP : MonoBehaviour
{
    [Header("행동력")]
    [InspectorName("최대 행동력")]
    [SerializeField, Min(0)] private int maxMP = 6;
    [InspectorName("현재 행동력(실행 중 확인용)")]
    [SerializeField, Min(0)] private int currentMP;

    /// <summary>외부의 Player/Enemy 데이터가 계산해 적용한 이 유닛의 최대 행동력.</summary>
    public int MaxMP => maxMP;

    /// <summary>현재 턴에 이동, 기본 공격, 카드 사용으로 소비할 수 있는 남은 행동력.</summary>
    public int CurrentMP => currentMP;

    /// <summary>현재 MP 또는 최대 MP가 바뀔 때 (현재 MP, 최대 MP)를 전달하는 갱신 이벤트.</summary>
    public event Action<int, int> MPChanged;

    /// <summary>캐릭터 생성 시 계산된 최대 행동력으로 현재 행동력을 초기화한다.</summary>
    private void Awake()
    {
        currentMP = MaxMP;
    }

    /// <summary>
    /// Player 스탯 또는 Enemy 데이터가 계산한 최종 최대 MP를 적용하고 즉시 완전 회복한다.
    /// 이 컴포넌트는 WIS 등의 스탯 공식을 직접 계산하지 않고 완성된 값만 전달받는다.
    /// </summary>
    public void ConfigureMaxMP(int value)
    {
        maxMP = Mathf.Max(0, value);
        RestoreFull();
    }

    /// <summary>현재 MP가 비용 이상인지 상태 변경 없이 검사한다.</summary>
    public bool CanSpend(int cost)
    {
        return cost >= 0 && currentMP >= cost;
    }

    /// <summary>비용을 지불할 수 있을 때만 차감하고 UI 이벤트를 보낸다.</summary>
    public bool TrySpend(int cost)
    {
        if (!CanSpend(cost))
        {
            return false;
        }

        currentMP -= cost;
        NotifyChanged();
        return true;
    }

    /// <summary>턴 시작 등에 사용하며 현재 MP를 최대 MP까지 회복한다.</summary>
    public void RestoreFull()
    {
        currentMP = MaxMP;
        NotifyChanged();
    }

    /// <summary>
    /// 외부 시스템이 결정한 이번 턴 MP를 현재 MP로 적용한다.
    /// 현재는 <c>EnemyTurnActor</c>가 Enemy 데이터의 최소~최대 범위에서 뽑은 턴 MP를 전달할 때 사용한다.
    /// 전달값은 0~최대 MP 범위로 제한되며, 적용 직후 UI 등의 구독자에게 변경을 알린다.
    /// </summary>
    public void SetCurrentMP(int value)
    {
        currentMP = Mathf.Clamp(value, 0, MaxMP);
        NotifyChanged();
    }

    /// <summary>현재 행동력과 최대 행동력을 모든 구독자에게 통지한다.</summary>
    private void NotifyChanged()
    {
        MPChanged?.Invoke(currentMP, MaxMP);
    }
}
