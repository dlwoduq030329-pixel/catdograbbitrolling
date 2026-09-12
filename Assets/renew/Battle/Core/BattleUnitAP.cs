using System;
using UnityEngine;

/// <summary>
/// 전투 유닛의 최대 AP(행동력)와 현재 AP를 보관하고 소비 결과를 알리는 런타임 컴포넌트다.
/// 2026-09-10: 예전에는 "주사위 굴림 결과"를 BattlePlayerActionController의
/// minMoveRange/maxMoveRange/currentMoveRange로 따로 들고 있었고, 이동 시 그 값과
/// BattleUnitMP(카드 사용·기본 공격용 자원)를 min()으로 같이 캡으로 씌우는 이상한 구조였다.
/// 이 컴포넌트는 그 "이동 전용 자원"만 분리해서 담당한다 — BattleUnitMP는 손대지 않고
/// 카드 사용·기본 공격 비용으로만 계속 쓰인다(그쪽이 더 중요해질 예정이라 그대로 유지하기로
/// 확정됨). 최대 AP 계산은 소유하지 않으며 DEX 등 스탯 계산 결과만 전달받는다.
/// </summary>
public sealed class BattleUnitAP : MonoBehaviour
{
    [Header("이동 행동력(AP)")]
    [InspectorName("기본 AP")]
    [UnityEngine.Serialization.FormerlySerializedAs("maxAP")]
    [SerializeField, Min(0)] private int baseAP = 6;
    [InspectorName("현재 AP(실행 중 확인용)")]
    [SerializeField, Min(0)] private int currentAP;

    private int maxAP;

    public int BaseAP => baseAP;

    /// <summary>기본 AP와 DEX 보너스를 적용한 최대 AP입니다.</summary>
    public int MaxAP => maxAP;

    /// <summary>이번 턴 이동에 쓸 수 있는 남은 AP.</summary>
    public int CurrentAP => currentAP;

    /// <summary>현재 AP 또는 최대 AP가 바뀔 때 (현재 AP, 최대 AP)를 전달하는 갱신 이벤트.</summary>
    public event Action<int, int> APChanged;

    /// <summary>캐릭터 생성 시 계산된 최대 AP로 현재 AP를 초기화한다.</summary>
    private void Awake()
    {
        maxAP = baseAP;
        currentAP = MaxAP;
    }

    /// <summary>
    /// DEX 등 스탯이 계산한 최종 최대 AP를 적용하고 즉시 완전 회복한다.
    /// 이 컴포넌트는 스탯 공식을 직접 계산하지 않고 완성된 값만 전달받는다.
    /// </summary>
    public void ConfigureMaxAP(int value)
    {
        maxAP = Mathf.Max(0, value);
        RestoreFull();
    }

    /// <summary>현재 AP가 비용 이상인지 상태 변경 없이 검사한다.</summary>
    public bool CanSpend(int cost)
    {
        return cost >= 0 && currentAP >= cost;
    }

    /// <summary>비용을 지불할 수 있을 때만 차감하고 UI 이벤트를 보낸다.</summary>
    public bool TrySpend(int cost)
    {
        if (!CanSpend(cost))
        {
            return false;
        }

        currentAP -= cost;
        NotifyChanged();
        return true;
    }

    /// <summary>턴 시작 시 사용하며 현재 AP를 최대 AP까지 회복한다.</summary>
    public void RestoreFull()
    {
        currentAP = MaxAP;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        APChanged?.Invoke(currentAP, MaxAP);
    }
}
