/// <summary>
/// "지금 어떤 조작 유닛이든 이동/공격 범위를 화면에 표시하고 있는가"를 Scene 전체에서 공유하는 상태다.
/// 원래 BattlePlayerActionController의 static bool/event였으나, 용병단처럼 조작 가능한 유닛이
/// 여러 개가 될 것을 대비해 특정 컨트롤러 클래스 이름에 묶이지 않는 별도 공유 서비스로 분리했다.
/// 지금은 Player가 하나뿐이라 단순 "켜짐/꺼짐" bool + 이벤트로 충분하지만, 조작 유닛이 여러 개가
/// 되면 SetVisible(bool)을 유닛별 등록/해제 카운터 방식으로 바꿔야 할 텐데, 그 변경 지점이
/// 이 클래스 하나로 좁혀지도록 만든 것이 이번 분리의 목적이다(외부 참조부는 그대로 유지 가능).
/// </summary>
public static class BattleRangeVisibilityTracker
{
    /// <summary>지금 이 순간 화면에 이동/공격 범위가 하나라도 표시되고 있으면 true.</summary>
    public static bool IsAnyRangeVisible { get; private set; }

    /// <summary>표시 여부가 실제로 바뀔 때만(중복 호출 무시) 발생한다.</summary>
    public static event System.Action<bool> VisibilityChanged;

    /// <summary>범위 표시 여부를 갱신한다. 이전 값과 같으면 아무 일도 하지 않는다.</summary>
    public static void SetVisible(bool visible)
    {
        if (IsAnyRangeVisible == visible)
        {
            return;
        }

        IsAnyRangeVisible = visible;
        VisibilityChanged?.Invoke(visible);
    }
}
