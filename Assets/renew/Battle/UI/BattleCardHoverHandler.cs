using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 카드 버튼에 마우스가 올라오고 벗어나는 시점만 View에 알린다.
/// 실제 확대·상승 연출은 BattleCardHandView가 매 프레임 목표 자세로 보간해서 처리하며,
/// 이 컴포넌트는 "지금 이 카드 위에 마우스가 있는가"만 전달하는 역할로 한정한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCardHoverHandler : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private Action onHoverEnter;
    private Action onHoverExit;

    /// <summary>이 카드 슬롯에 마우스가 들어오고 나갈 때 각각 실행할 콜백을 연결한다.</summary>
    public void ConfigureHover(Action hoverEnterCallback, Action hoverExitCallback)
    {
        onHoverEnter = hoverEnterCallback;
        onHoverExit = hoverExitCallback;
    }

    /// <summary>마우스가 카드 영역에 들어온 순간을 View에 전달한다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        onHoverEnter?.Invoke();
    }

    /// <summary>마우스가 카드 영역을 벗어난 순간을 View에 전달한다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        onHoverExit?.Invoke();
    }
}
