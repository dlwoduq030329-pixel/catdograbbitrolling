using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 카드 버튼의 짧은 클릭과 길게 누르기를 구분한다.
/// 지정된 시간 동안 왼쪽 버튼을 유지하면 카드 정보 표시 콜백을 한 번 실행한다.
/// Unity Button은 포인터를 놓은 뒤 일반 클릭도 발생시키므로, 길게 누른 같은 입력이
/// 카드 선택으로 이어지지 않도록 바로 다음 클릭만 무시하도록 기록한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCardLongPressHandler : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    private float requiredHoldSeconds;
    private Action onLongPressStarted;
    private bool isLeftButtonHeld;
    private bool didTriggerLongPress;
    private bool shouldIgnoreNextButtonClick;
    private float holdStartedTime;

    /// <summary>
    /// 카드 정보 표시를 시작할 최소 누르기 시간과, 그 시간이 지났을 때 한 번 실행할 함수를 연결한다.
    /// 너무 짧은 설정값으로 일반 클릭까지 길게 누르기로 오인하지 않도록 최소 시간은 0.1초로 제한한다.
    /// </summary>
    public void ConfigureLongPress(float holdDurationSeconds, Action onHoldCompleted)
    {
        requiredHoldSeconds = Mathf.Max(0.1f, holdDurationSeconds);
        onLongPressStarted = onHoldCompleted;
    }

    /// <summary>
    /// 왼쪽 버튼을 누르고 있는 동안 경과 시간을 확인한다.
    /// 필요한 시간이 지나면 정보 표시 콜백을 한 번 호출하고, 손을 뗄 때 뒤이어 발생할
    /// Button.onClick이 카드 선택으로 처리되지 않도록 다음 클릭 무시 상태를 기록한다.
    /// </summary>
    private void Update()
    {
        if (!isLeftButtonHeld || didTriggerLongPress)
        {
            return;
        }

        if (Time.unscaledTime - holdStartedTime < requiredHoldSeconds)
        {
            return;
        }

        didTriggerLongPress = true;
        shouldIgnoreNextButtonClick = true;
        onLongPressStarted?.Invoke();
    }

    /// <summary>
    /// 카드 위에서 왼쪽 버튼을 누르면 길게 누른 시간을 측정하기 시작한다.
    /// 이전 입력의 실행 여부를 초기화하고, 게임 일시정지의 영향을 받지 않는 시간을 시작점으로 저장한다.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        isLeftButtonHeld = true;
        didTriggerLongPress = false;
        holdStartedTime = Time.unscaledTime;
    }

    /// <summary>
    /// 왼쪽 버튼을 놓으면 누르기 시간 측정을 끝낸다.
    /// 길게 누르기가 이미 성립했다면 뒤이어 발생하는 Button.onClick은 별도 상태값으로 한 번 무시된다.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            isLeftButtonHeld = false;
        }
    }

    /// <summary>
    /// 누른 채 카드 영역 밖으로 포인터가 나가면 길게 누르기를 취소한다.
    /// 사용자가 다른 카드로 드래그한 시간을 이 카드의 길게 누르기로 처리하지 않기 위한 동작이다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isLeftButtonHeld = false;
    }

    /// <summary>
    /// 직전에 길게 누르기가 성립해 이번 Button.onClick을 카드 선택으로 처리하면 안 되는지 반환한다.
    /// true를 한 번 반환한 즉시 상태를 해제하므로 다음번의 정상적인 짧은 클릭은 그대로 처리된다.
    /// </summary>
    public bool ShouldIgnoreClickAfterLongPress()
    {
        if (!shouldIgnoreNextButtonClick)
        {
            return false;
        }

        shouldIgnoreNextButtonClick = false;
        return true;
    }
}
