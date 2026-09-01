using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 전투 씬의 주사위 굴리기 버튼 입력을 처리한다.
/// 버튼을 꾹 누르고 있으면 연결된 Slider(게이지)가 0~1 사이를 왕복하고,
/// 손을 떼는 순간 BattleGameManager.RollDice()를 통해 BattleDiceSystem에 실제 굴림을 요청한다.
///
/// 기존 필드맵 전용 Assets/Game/Scripts/Board/RollDice.cs(레거시, NodeBasePlayerMov·LinkSelect 등
/// 필드맵 클래스에 의존)와는 완전히 별개이며, 전투 시스템(BattleGameManager)에만 의존한다.
/// 게이지가 오가는 연출은 순수 시각 효과이며, 결과값 자체는 BattleDiceSystem이 정한다
/// (게이지 위치가 주사위 값에 영향을 주지 않음).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDiceRollButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("게이지 표시")]
    [InspectorName("게이지로 사용할 Slider")]
    [FormerlySerializedAs("rollSlider")]
    [SerializeField] private Slider rollChargeGauge;
    [InspectorName("게이지 왕복 속도(초당)")]
    [FormerlySerializedAs("gaugeSpeed")]
    [SerializeField, Min(0.01f)] private float gaugeTravelSpeedPerSecond = 1f;

    private Button rollButton;
    private float gaugeMovementDirection = 1f;
    private bool isRollButtonHeld;

    /// <summary>
    /// BattleGameManager가 전달한 현재 턴 상태에 맞춰 주사위 버튼의 표시와 입력 가능 여부를 갱신한다.
    /// 주사위 버튼 자신의 표현 상태이므로 BattleTurnButtonController를 경유하지 않는다.
    /// </summary>
    public void ApplyRollButtonState(
        bool isPlayerTurn,
        bool hasRolledThisTurn,
        bool battleStopped,
        bool overlayOpen)
    {
        if (rollButton == null)
        {
            rollButton = GetComponent<Button>();
        }

        if (rollButton == null)
        {
            return;
        }

        bool shouldShowRollButton = isPlayerTurn && !hasRolledThisTurn && !battleStopped;
        rollButton.gameObject.SetActive(shouldShowRollButton);
        rollButton.interactable = shouldShowRollButton && !overlayOpen;
    }

    /// <summary>Player 사망처럼 전투가 중지된 경우 주사위 버튼 입력을 즉시 비활성화한다.</summary>
    public void DisableRollInput()
    {
        if (rollButton == null)
        {
            rollButton = GetComponent<Button>();
        }

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }
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
        isRollButtonHeld = false;
        SetRollGaugeVisible(false);
    }

    /// <summary>
    /// 버튼을 누르고 있는 동안 일시정지의 영향을 받지 않는 시간으로 게이지를 0과 1 사이에서 왕복시킨다.
    /// 양 끝에 도달하면 이동 방향을 반대로 바꾸고 해당 방향의 슬라이더 효과음을 재생한다.
    /// 이 값은 주사위 결과 계산에 사용되지 않으며 누르는 동안의 시각·청각 연출만 담당한다.
    /// </summary>
    private void Update()
    {
        if (!isRollButtonHeld || rollChargeGauge == null)
        {
            return;
        }

        float nextGaugeValue = rollChargeGauge.value +
                               gaugeMovementDirection *
                               gaugeTravelSpeedPerSecond *
                               Time.unscaledDeltaTime;
        if (nextGaugeValue >= 1f)
        {
            nextGaugeValue = 1f;
            gaugeMovementDirection = -1f;
            SoundManager.Instance?.SliderDown();
        }
        else if (nextGaugeValue <= 0f)
        {
            nextGaugeValue = 0f;
            gaugeMovementDirection = 1f;
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

        isRollButtonHeld = true;
        SetRollGaugeVisible(true);
        SoundManager.Instance?.sliderUp();
    }

    /// <summary>
    /// 유효하게 누르기 시작한 뒤 포인터를 놓으면 게이지 연출을 종료하고 주사위 효과음을 재생한다.
    /// 이어서 Manager를 경유해 BattleDiceSystem에 실제 턴 주사위 처리를 요청한 뒤 게이지를 초기 상태로 되돌린다.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isRollButtonHeld)
        {
            return;
        }

        isRollButtonHeld = false;
        SoundManager.Instance?.RollDice();

        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.RollDice();
        }

        ResetRollGauge();
        SetRollGaugeVisible(false);
    }

    /// <summary>
    /// BattleGameManager가 존재하고 주사위 Button이 현재 입력 가능한지 확인한다.
    /// Button 참조가 없는 예외적인 구성에서는 Manager 존재만으로 입력을 허용한다.
    /// </summary>
    private bool CanStartRollInput()
    {
        if (BattleGameManager.Instance == null)
        {
            return false;
        }

        return rollButton == null || rollButton.IsInteractable();
    }

    /// <summary>다음 입력이 0에서 위쪽으로 시작하도록 게이지 값과 이동 방향을 초기화한다.</summary>
    private void ResetRollGauge()
    {
        gaugeMovementDirection = 1f;
        if (rollChargeGauge != null)
        {
            rollChargeGauge.value = 0f;
        }
    }

    /// <summary>실린더(게이지) 오브젝트를 누르고 있을 때만 보이게 켜고 끈다.</summary>
    private void SetRollGaugeVisible(bool shouldBeVisible)
    {
        if (rollChargeGauge == null)
        {
            return;
        }

        rollChargeGauge.gameObject.SetActive(shouldBeVisible);
    }
}
