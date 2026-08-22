using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

/// <summary>
/// 전투 손패 패널을 지정 키 또는 외부 UI 요청으로 화면 안팎에 슬라이드한다.
/// 패널 GameObject를 비활성화하지 않고 위치와 CanvasGroup 입력만 변경하므로,
/// 숨겨진 동안에도 손패 변경 이벤트를 계속 받아 다음 표시 상태를 준비할 수 있다.
/// 향후 항상 보이는 부채꼴 손패를 채택하면 Tab 입력은 제거하고 손패 등장·퇴장 연출 제어기로 재사용할 수 있다.
/// </summary>
public class BattleCardPanelToggle : MonoBehaviour
{
    [Header("카드 패널")]
    [InspectorName("이동할 카드 패널")]
    [FormerlySerializedAs("cardPanel")]
    [SerializeField] private RectTransform handPanelRect;
    [InspectorName("입력 차단용 캔버스 그룹")]
    [FormerlySerializedAs("canvasGroup")]
    [SerializeField] private CanvasGroup handPanelInputGroup;

    [Header("입력")]
    [InspectorName("카드 패널 열기/닫기 키")]
    [FormerlySerializedAs("toggleKey")]
    [SerializeField] private KeyCode handPanelToggleKey = KeyCode.Tab;
    [InspectorName("문자 입력 중 단축키 무시")]
    [FormerlySerializedAs("ignoreWhenEditingUI")]
    [SerializeField] private bool ignoreShortcutWhileTyping = true;

    [Header("슬라이드 설정")]
    [Tooltip("인스펙터에 배치한 위치를 열린 위치로 사용합니다.")]
    [InspectorName("닫힐 때 이동할 거리")]
    [FormerlySerializedAs("hiddenOffset")]
    [SerializeField] private Vector2 hiddenPositionOffset = new Vector2(0f, -320f);
    [InspectorName("슬라이드 시간")]
    [FormerlySerializedAs("slideDuration")]
    [SerializeField, Min(0f)] private float slideDurationSeconds = 0.2f;
    [InspectorName("시작할 때 패널 닫기")]
    [FormerlySerializedAs("startHidden")]
    [SerializeField] private bool hidePanelAtBattleStart = true;

    private Vector2 visibleAnchoredPosition;
    private Vector2 hiddenAnchoredPosition;
    private Coroutine activeSlideCoroutine;
    private BattleCardInfoPresenter cardInfoPanelPresenter;

    /// <summary>손패 패널이 열린 목표 상태인지 나타낸다. 슬라이드가 끝나기 전에도 목표 상태가 즉시 반영된다.</summary>
    public bool IsShown { get; private set; }
    /// <summary>현재 Inspector에 설정된 손패 열기·닫기 단축키를 외부 입력 안내 UI에 제공한다.</summary>
    public KeyCode ToggleKey => handPanelToggleKey;

    /// <summary>
    /// 손패 RectTransform과 입력 차단용 CanvasGroup을 준비하고, Inspector에 배치된 위치를 기준으로
    /// 열린 위치와 숨겨진 위치를 한 번 계산한다. 필수 계층 검증에 실패하면 이후 입력 처리를 중단한다.
    /// </summary>
    private void Awake()
    {
        if (handPanelRect == null)
        {
            handPanelRect = transform as RectTransform;
        }

        cardInfoPanelPresenter = handPanelRect != null
            ? handPanelRect.GetComponent<BattleCardInfoPresenter>()
            : null;

        if (!IsValidBattleCardPanel())
        {
            enabled = false;
            return;
        }

        if (handPanelInputGroup == null && handPanelRect != null)
        {
            handPanelInputGroup = handPanelRect.GetComponent<CanvasGroup>();
            if (handPanelInputGroup == null)
            {
                handPanelInputGroup = handPanelRect.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (handPanelRect == null)
        {
            Debug.LogError("카드 패널 전환 실패: RectTransform을 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        visibleAnchoredPosition = handPanelRect.anchoredPosition;
        hiddenAnchoredPosition = visibleAnchoredPosition + hiddenPositionOffset;
        ApplyPanelStateImmediately(
            hidePanelAtBattleStart ? hiddenAnchoredPosition : visibleAnchoredPosition,
            !hidePanelAtBattleStart);
    }

    /// <summary>카드 패널이 전투 Canvas 내부의 하위 오브젝트인지 검사한다.</summary>
    private bool IsValidBattleCardPanel()
    {
        if (handPanelRect == null)
        {
            Debug.LogError("카드 패널 전환 실패: RectTransform을 찾을 수 없습니다.", this);
            return false;
        }

        Canvas[] parentCanvases = handPanelRect.GetComponentsInParent<Canvas>(true);
        if (parentCanvases.Length == 0)
        {
            Debug.LogError("카드 패널 전환 실패: 상위 Canvas를 찾을 수 없습니다.", this);
            return false;
        }

        Canvas rootCanvas = parentCanvases[parentCanvases.Length - 1];
        if (rootCanvas.name.Trim() != "Canvas - Battle")
        {
            Debug.LogError(
                $"카드 패널 전환 실패: '{handPanelRect.name}'은 Canvas - Battle 내부에 있어야 합니다.",
                this);
            return false;
        }

        if (handPanelRect.gameObject == rootCanvas.gameObject)
        {
            Debug.LogError(
                "카드 패널 전환 실패: Canvas - Battle 자체가 아닌 하위 손패 패널에 추가해야 합니다.",
                this);
            return false;
        }

        return true;
    }

    /// <summary>문자 입력 중이 아닐 때 설정된 단축키로 카드 패널 상태를 전환한다.
    /// 카드 사거리 표시·대상 선택 중에도 Tab으로 패널을 여닫을 수 있다(더 이상 차단하지 않음).</summary>
    private void Update()
    {
        if (Input.GetKeyDown(handPanelToggleKey) &&
            !ShouldIgnoreToggleShortcut())
        {
            if (cardInfoPanelPresenter != null)
            {
                cardInfoPanelPresenter.Hide();
            }

            Toggle();
        }
    }

    /// <summary>현재 상태의 반대로 카드 패널을 슬라이드한다.</summary>
    public void Toggle()
    {
        if (IsShown)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>카드 패널을 화면 안으로 올린다.</summary>
    public void Show()
    {
        StartPanelSlide(visibleAnchoredPosition, true);
    }

    /// <summary>카드 패널을 화면 밖으로 내린다.</summary>
    public void Hide()
    {
        if (cardInfoPanelPresenter != null)
        {
            cardInfoPanelPresenter.Hide();
        }

        StartPanelSlide(hiddenAnchoredPosition, false);
    }

    /// <summary>
    /// 이전 손패 이동이 진행 중이면 중단하고 새 목표 위치로 이동을 시작한다.
    /// 이동 중 잘못된 카드 클릭이 발생하지 않도록 먼저 입력을 잠그고 목표 열림 상태를 기록한다.
    /// </summary>
    private void StartPanelSlide(Vector2 targetAnchoredPosition, bool shouldBeVisible)
    {
        if (activeSlideCoroutine != null)
        {
            StopCoroutine(activeSlideCoroutine);
        }

        IsShown = shouldBeVisible;
        SetHandPanelInteraction(false);
        activeSlideCoroutine = StartCoroutine(
            AnimatePanelToPosition(targetAnchoredPosition, shouldBeVisible));
    }

    /// <summary>
    /// 게임 일시정지와 무관한 시간으로 손패를 현재 위치에서 목표 위치까지 부드럽게 이동한다.
    /// 이동이 끝난 뒤 최종 위치를 정확히 맞추고, 열린 상태일 때만 카드 입력을 다시 허용한다.
    /// </summary>
    private IEnumerator AnimatePanelToPosition(
        Vector2 targetAnchoredPosition,
        bool shouldBeVisible)
    {
        Vector2 startingAnchoredPosition = handPanelRect.anchoredPosition;
        if (slideDurationSeconds <= 0f)
        {
            ApplyPanelStateImmediately(targetAnchoredPosition, shouldBeVisible);
            activeSlideCoroutine = null;
            yield break;
        }

        float elapsedSeconds = 0f;
        while (elapsedSeconds < slideDurationSeconds)
        {
            elapsedSeconds += Time.unscaledDeltaTime;
            float normalizedProgress = Mathf.Clamp01(elapsedSeconds / slideDurationSeconds);
            float easedProgress = 1f - Mathf.Pow(1f - normalizedProgress, 3f);
            handPanelRect.anchoredPosition = Vector2.LerpUnclamped(
                startingAnchoredPosition,
                targetAnchoredPosition,
                easedProgress);
            yield return null;
        }

        ApplyPanelStateImmediately(targetAnchoredPosition, shouldBeVisible);
        activeSlideCoroutine = null;
    }

    /// <summary>애니메이션 없이 손패 위치·열림 상태·입력 가능 여부를 같은 상태로 즉시 맞춘다.</summary>
    private void ApplyPanelStateImmediately(Vector2 anchoredPosition, bool shouldBeVisible)
    {
        handPanelRect.anchoredPosition = anchoredPosition;
        IsShown = shouldBeVisible;
        SetHandPanelInteraction(shouldBeVisible);
    }

    /// <summary>닫힌 카드가 보이지 않는 위치에서 클릭되는 것을 CanvasGroup으로 차단한다.</summary>
    private void SetHandPanelInteraction(bool allowCardInput)
    {
        if (handPanelInputGroup == null)
        {
            return;
        }

        handPanelInputGroup.interactable = allowCardInput;
        handPanelInputGroup.blocksRaycasts = allowCardInput;
    }

    /// <summary>
    /// 사용자가 InputField에 문자를 입력하는 동안 Tab이 손패 전환까지 실행되지 않도록 검사한다.
    /// 입력창 무시 옵션이 꺼져 있거나 EventSystem이 없으면 단축키를 정상 처리한다.
    /// </summary>
    private bool ShouldIgnoreToggleShortcut()
    {
        if (!ignoreShortcutWhileTyping || EventSystem.current == null)
        {
            return false;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        return selectedObject != null &&
               (selectedObject.GetComponent<UnityEngine.UI.InputField>() != null ||
                selectedObject.GetComponent<TMPro.TMP_InputField>() != null);
    }
}
