using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 씬의 주사위 굴리기 버튼 입력을 처리한다.
/// 버튼을 꾹 누르고 있으면 연결된 Slider(게이지)가 0~1 사이를 왕복하고,
/// 손을 떼는 순간 BattleGameManager.RollDice()를 호출해 실제 주사위를 굴린다.
///
/// 기존 필드맵 전용 Assets/Game/Scripts/Board/RollDice.cs(레거시, NodeBasePlayerMov·LinkSelect 등
/// 필드맵 클래스에 의존)와는 완전히 별개이며, 전투 시스템(BattleGameManager)에만 의존한다.
/// 게이지가 오가는 연출은 순수 시각 효과이며, 결과값 자체는 BattleGameManager.RollDice() 내부
/// 로직을 그대로 사용한다(게이지 위치가 주사위 값에 영향을 주지 않음).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDiceRollButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("게이지 표시")]
    [InspectorName("게이지로 사용할 Slider")]
    [SerializeField] private Slider rollSlider;
    [InspectorName("게이지 왕복 속도(초당)")]
    [SerializeField, Min(0.01f)] private float gaugeSpeed = 1f;

    private Button ownerButton;
    private float gaugeDirection = 1f;
    private bool isHolding;

    private void Awake()
    {
        ownerButton = GetComponent<Button>();
    }

    private void OnEnable()
    {
        ResetGauge();
        SetGaugeVisible(false);
    }

    private void OnDisable()
    {
        isHolding = false;
        SetGaugeVisible(false);
    }

    private void Update()
    {
        if (!isHolding || rollSlider == null)
        {
            return;
        }

        float nextValue = rollSlider.value + gaugeDirection * gaugeSpeed * Time.unscaledDeltaTime;
        if (nextValue >= 1f)
        {
            nextValue = 1f;
            gaugeDirection = -1f;
            SoundManager.Instance?.SliderDown();
        }
        else if (nextValue <= 0f)
        {
            nextValue = 0f;
            gaugeDirection = 1f;
            SoundManager.Instance?.sliderUp();
        }

        rollSlider.value = nextValue;
    }

    /// <summary>버튼을 누르기 시작하면 게이지 왕복을 시작한다.</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanRoll())
        {
            return;
        }

        isHolding = true;
        SetGaugeVisible(true);
        SoundManager.Instance?.sliderUp();
    }

    /// <summary>버튼에서 손을 떼면 게이지를 멈추고 실제 주사위를 굴린다.</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHolding)
        {
            return;
        }

        isHolding = false;
        SoundManager.Instance?.RollDice();

        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.RollDice();
        }

        ResetGauge();
        SetGaugeVisible(false);
    }

    /// <summary>버튼의 Interactable 상태(BattleGameManager.SyncTurnUI가 갱신)를 그대로 따른다.</summary>
    private bool CanRoll()
    {
        if (BattleGameManager.Instance == null)
        {
            return false;
        }

        return ownerButton == null || ownerButton.IsInteractable();
    }

    private void ResetGauge()
    {
        gaugeDirection = 1f;
        if (rollSlider != null)
        {
            rollSlider.value = 0f;
        }
    }

    /// <summary>실린더(게이지) 오브젝트를 누르고 있을 때만 보이게 켜고 끈다.</summary>
    private void SetGaugeVisible(bool visible)
    {
        if (rollSlider == null)
        {
            return;
        }

        rollSlider.gameObject.SetActive(visible);
    }
}
