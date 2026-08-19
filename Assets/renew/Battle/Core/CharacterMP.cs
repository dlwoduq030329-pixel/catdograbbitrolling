using System;
using UnityEngine;

/// <summary>
/// Player와 Enemy가 함께 사용하는 MP 런타임 컴포넌트다.
/// Player는 INT 계산식을, Enemy는 DB에서 전달받은 고정 최대 MP를 사용한다.
/// </summary>
public class CharacterMP : MonoBehaviour
{
    [Header("최대 행동력 계산 방식")]
    [InspectorName("고정 최대 행동력 사용")]
    [SerializeField] private bool useFixedMaxMP;
    [InspectorName("고정 최대 행동력")]
    [SerializeField, Range(0, 10)] private int fixedMaxMP = 6;

    [Header("플레이어 행동력 계산 설정")]
    [InspectorName("기본 행동력")]
    [SerializeField, Min(0)] private int baseMP = 6;
    [InspectorName("지능(INT)")]
    [SerializeField, Min(0)] private int intelligence;
    [InspectorName("행동력 1 증가에 필요한 지능")]
    [SerializeField, Min(1)] private int intelligencePerMP = 6;
    [InspectorName("최대 행동력 상한")]
    [SerializeField, Min(1)] private int maxMPCap = 10;
    [InspectorName("현재 행동력(실행 중 확인용)")]
    [SerializeField, Min(0)] private int currentMP;

    public int Intelligence => intelligence;
    public int MaxMP => useFixedMaxMP
        ? Mathf.Clamp(fixedMaxMP, 0, maxMPCap)
        : Mathf.Clamp(baseMP + intelligence / intelligencePerMP, 0, maxMPCap);
    public int CurrentMP => currentMP;

    public event Action<int, int> MPChanged;

    /// <summary>캐릭터 생성 시 계산된 최대 행동력으로 현재 행동력을 초기화한다.</summary>
    private void Awake()
    {
        currentMP = MaxMP;
    }

    /// <summary>Player의 최종 INT를 갱신하고 최대 MP를 다시 계산한다.</summary>
    public void SetIntelligence(int value)
    {
        intelligence = Mathf.Max(0, value);
        currentMP = Mathf.Min(currentMP, MaxMP);
        NotifyChanged();
    }

    /// <summary>Enemy DB의 고정 최대 MP 모드로 전환하고 즉시 완전 회복한다.</summary>
    public void ConfigureFixedMaxMP(int value)
    {
        useFixedMaxMP = true;
        fixedMaxMP = Mathf.Clamp(value, 0, maxMPCap);
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

    public void SetCurrentMP(int value)
    {
        currentMP = Mathf.Clamp(value, 0, MaxMP);
        NotifyChanged();
    }

    /// <summary>값을 변경하지 않고 현재 행동력 정보를 화면 구독자에게 다시 전달한다.</summary>
    public void NotifyCurrentValue()
    {
        NotifyChanged();
    }

    /// <summary>현재 행동력과 최대 행동력을 모든 구독자에게 통지한다.</summary>
    private void NotifyChanged()
    {
        MPChanged?.Invoke(currentMP, MaxMP);
    }
}
