using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 카드 버튼의 짧은 클릭과 길게 누르기를 구분한다.
/// 길게 누르기가 성립한 경우 같은 입력에서 발생하는 버튼 클릭을 한 번 차단한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCardLongPressHandler : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    private float holdSeconds;
    private Action longPressed;
    private bool pointerDown;
    private bool longPressTriggered;
    private bool suppressNextClick;
    private float pressedAt;

    /// <summary>길게 누르기 시간과 완료 콜백을 설정한다.</summary>
    public void Configure(float seconds, Action callback)
    {
        holdSeconds = Mathf.Max(0.1f, seconds);
        longPressed = callback;
    }

    private void Update()
    {
        if (!pointerDown || longPressTriggered)
        {
            return;
        }

        if (Time.unscaledTime - pressedAt < holdSeconds)
        {
            return;
        }

        longPressTriggered = true;
        suppressNextClick = true;
        longPressed?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        pointerDown = true;
        longPressTriggered = false;
        pressedAt = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerDown = false;
    }

    /// <summary>길게 누르기 직후 발생한 버튼 클릭을 한 번 소비한다.</summary>
    public bool ConsumeSuppressedClick()
    {
        if (!suppressNextClick)
        {
            return false;
        }

        suppressNextClick = false;
        return true;
    }
}
