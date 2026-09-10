using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 주사위 버튼의 누르기·떼기 입력과 게이지 표시를 담당합니다.
/// 게이지는 연출이며 실제 결과에는 영향을 주지 않습니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class BattleDiceRollButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("게이지 표시")]
    [InspectorName("게이지로 사용할 Slider")]
    [FormerlySerializedAs("rollSlider")]
    [SerializeField] private Slider rollChargeGauge;
    [InspectorName("게이지 왕복 속도(초당)")]
    [FormerlySerializedAs("gaugeTravelSpeedPerSecond")]
    [FormerlySerializedAs("gaugeSpeed")]
    [SerializeField, Min(0.01f)] private float gaugeSpeed = 1f;

    private Button rollButton;
    private float gaugeDirection = 1f;
    private bool isHeld;
    private Action rollAction;

    /// <summary>카드 사용 확인 뒤 버튼을 활성화하고, 손을 뗄 때 전달받은 굴림 요청을 한 번 실행한다.</summary>
    public void ShowForCardRoll(Action onRollRequested)
    {
        rollAction = onRollRequested;
        rollButton.interactable = true;
        ResetRollGauge();
        SetRollGaugeVisible(false);
    }

    /// <summary>현재 카드 굴림 요청과 버튼 입력 상태를 초기화한다.</summary>
    public void ClearCardRollRequest()
    {
        rollAction = null;
        isHeld = false;
        ResetRollGauge();
        SetRollGaugeVisible(false);
    }

    /// <summary>
    /// 이 입력 컴포넌트와 같은 GameObject에 있는 Button을 저장한다.
    /// 이후 포인터 입력을 시작할 때 Button.interactable 상태를 확인하는 데 사용한다.
    /// </summary>
    private void Awake()
    {
        rollButton = GetComponent<Button>();
    }

    /// <summary>
    /// 버튼이 다시 활성화될 때 이전 입력의 게이지 값과 이동 방향을 초기화하고,
    /// 사용자가 새로 누르기 전까지 게이지 오브젝트를 숨긴다.
    /// </summary>
    private void OnEnable()
    {
        ResetRollGauge();
        SetRollGaugeVisible(false);
    }

    /// <summary>
    /// 버튼이나 부모 UI가 비활성화되는 도중 포인터를 놓는 이벤트를 받지 못하더라도
    /// 누름 상태가 남지 않도록 입력 상태를 해제하고 게이지를 숨긴다.
    /// </summary>
    private void OnDisable()
    {
        isHeld = false;
        rollAction = null;
        SetRollGaugeVisible(false);
    }

    /// <summary>
    /// 버튼을 누르고 있는 동안 일시정지의 영향을 받지 않는 시간으로 게이지를 0과 1 사이에서 왕복시킨다.
    /// 양 끝에 도달하면 이동 방향을 반대로 바꾸고 해당 방향의 슬라이더 효과음을 재생한다.
    /// 이 값은 주사위 결과 계산에 사용되지 않으며 누르는 동안의 시각·청각 연출만 담당한다.
    /// </summary>
    private void Update()
    {
        if (!isHeld || rollChargeGauge == null)
        {
            return;
        }

        float nextGaugeValue = rollChargeGauge.value +
                               gaugeDirection *
                               gaugeSpeed *
                               Time.unscaledDeltaTime;
        if (nextGaugeValue >= 1f)
        {
            nextGaugeValue = 1f;
            gaugeDirection = -1f;
            SoundManager.Instance?.SliderDown();
        }
        else if (nextGaugeValue <= 0f)
        {
            nextGaugeValue = 0f;
            gaugeDirection = 1f;
            SoundManager.Instance?.sliderUp();
        }

        rollChargeGauge.value = nextGaugeValue;
    }

    /// <summary>
    /// 주사위 버튼 위에서 포인터를 누르면 현재 Player 턴에 입력 가능한지 확인한 뒤 게이지 왕복을 시작한다.
    /// 실제 주사위 굴림은 아직 실행하지 않고, 포인터를 놓는 시점까지 누름 상태만 유지한다.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanStartRollInput())
        {
            return;
        }

        isHeld = true;
        SetRollGaugeVisible(true);
        SoundManager.Instance?.sliderUp();
    }

    /// <summary>
    /// 유효하게 누르기 시작한 뒤 포인터를 놓으면 게이지 연출을 종료하고 주사위 효과음을 재생한다.
    /// 이어서 BattleDiceSystem이 등록한 카드 효과 굴림 요청을 한 번 실행하고 게이지를 초기화한다.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHeld)
        {
            return;
        }

        isHeld = false;
        SoundManager.Instance?.RollDice();

        Action rollRequest = rollAction;
        rollAction = null;
        rollRequest?.Invoke();

        ResetRollGauge();
        SetRollGaugeVisible(false);
    }

    /// <summary>
    /// 카드 굴림 요청이 등록되어 있고 주사위 Button이 현재 입력 가능한지 확인한다.
    /// </summary>
    private bool CanStartRollInput()
    {
        if (rollAction == null)
        {
            Debug.LogWarning("[BattleDice] 실행할 주사위 함수가 연결되지 않았습니다.", this);
            return false;
        }

        return rollButton.IsInteractable();
    }

    /// <summary>다음 입력이 0에서 위쪽으로 시작하도록 게이지 값과 이동 방향을 초기화한다.</summary>
    private void ResetRollGauge()
    {
        gaugeDirection = 1f;
        if (rollChargeGauge != null)
        {
            rollChargeGauge.value = 0f;
        }
    }

    /// <summary>게이지를 누르고 있는 동안만 표시합니다.</summary>
    private void SetRollGaugeVisible(bool shouldBeVisible)
    {
        if (rollChargeGauge == null)
        {
            return;
        }

        rollChargeGauge.gameObject.SetActive(shouldBeVisible);
    }
}
