using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 카드 손패 패널을 지정 키로 화면 안팎에 슬라이드한다.
/// 오브젝트를 비활성화하지 않으므로 키 입력과 손패 갱신 이벤트는 계속 받을 수 있다.
/// </summary>
public class BattleCardPanelToggle : MonoBehaviour
{
    [Header("카드 패널")]
    [InspectorName("이동할 카드 패널")]
    [SerializeField] private RectTransform cardPanel;
    [InspectorName("입력 차단용 캔버스 그룹")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("입력")]
    [InspectorName("카드 패널 열기/닫기 키")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [InspectorName("문자 입력 중 단축키 무시")]
    [SerializeField] private bool ignoreWhenEditingUI = true;

    [Header("슬라이드 설정")]
    [Tooltip("인스펙터에 배치한 위치를 열린 위치로 사용합니다.")]
    [InspectorName("닫힐 때 이동할 거리")]
    [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -320f);
    [InspectorName("슬라이드 시간")]
    [SerializeField, Min(0f)] private float slideDuration = 0.2f;
    [InspectorName("시작할 때 패널 닫기")]
    [SerializeField] private bool startHidden = true;

    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private Coroutine slideRoutine;
    private BattleCardInfoPresenter cardInfoPresenter;
    private BattleCardActionController cardActionController;

    public bool IsShown { get; private set; }
    public KeyCode ToggleKey => toggleKey;

    /// <summary>이동 패널과 입력 차단용 CanvasGroup을 보완하고 열림·닫힘 위치를 계산한다.</summary>
    private void Awake()
    {
        if (cardPanel == null)
        {
            cardPanel = transform as RectTransform;
        }

        cardInfoPresenter = cardPanel != null
            ? cardPanel.GetComponent<BattleCardInfoPresenter>()
            : null;

        if (!IsValidBattleCardPanel())
        {
            enabled = false;
            return;
        }

        if (canvasGroup == null && cardPanel != null)
        {
            canvasGroup = cardPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = cardPanel.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (cardPanel == null)
        {
            Debug.LogError("카드 패널 전환 실패: RectTransform을 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        shownPosition = cardPanel.anchoredPosition;
        hiddenPosition = shownPosition + hiddenOffset;
        ApplyImmediate(startHidden ? hiddenPosition : shownPosition, !startHidden);
    }

    /// <summary>카드 패널이 전투 Canvas 내부의 하위 오브젝트인지 검사한다.</summary>
    private bool IsValidBattleCardPanel()
    {
        if (cardPanel == null)
        {
            Debug.LogError("카드 패널 전환 실패: RectTransform을 찾을 수 없습니다.", this);
            return false;
        }

        Canvas[] parentCanvases = cardPanel.GetComponentsInParent<Canvas>(true);
        if (parentCanvases.Length == 0)
        {
            Debug.LogError("카드 패널 전환 실패: 상위 Canvas를 찾을 수 없습니다.", this);
            return false;
        }

        Canvas rootCanvas = parentCanvases[parentCanvases.Length - 1];
        if (rootCanvas.name.Trim() != "Canvas - Battle")
        {
            Debug.LogError(
                $"카드 패널 전환 실패: '{cardPanel.name}'은 Canvas - Battle 내부에 있어야 합니다.",
                this);
            return false;
        }

        if (cardPanel.gameObject == rootCanvas.gameObject)
        {
            Debug.LogError(
                "카드 패널 전환 실패: Canvas - Battle 자체가 아닌 하위 손패 패널에 추가해야 합니다.",
                this);
            return false;
        }

        return true;
    }

    /// <summary>문자 입력 중이 아닐 때 설정된 단축키로 카드 패널 상태를 전환한다.</summary>
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) &&
            !ShouldIgnoreShortcut() &&
            !IsCardActionLocked())
        {
            if (cardInfoPresenter != null)
            {
                cardInfoPresenter.Hide();
            }

            Toggle();
        }
    }

    /// <summary>카드 대상 선택이나 사용 확인이 끝나기 전에는 Tab 패널 전환을 차단한다.</summary>
    private bool IsCardActionLocked()
    {
        if (cardActionController == null)
        {
            cardActionController = FindFirstObjectByType<BattleCardActionController>(FindObjectsInactive.Include);
        }

        return cardActionController != null && cardActionController.IsActionActive;
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
        StartSlide(shownPosition, true);
    }

    /// <summary>카드 패널을 화면 밖으로 내린다.</summary>
    public void Hide()
    {
        if (cardInfoPresenter != null)
        {
            cardInfoPresenter.Hide();
        }

        StartSlide(hiddenPosition, false);
    }

    /// <summary>진행 중인 이동을 정리하고 지정 위치로 향하는 새 슬라이드 애니메이션을 시작한다.</summary>
    private void StartSlide(Vector2 targetPosition, bool show)
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
        }

        IsShown = show;
        SetInteraction(false);
        slideRoutine = StartCoroutine(SlideTo(targetPosition, show));
    }

    /// <summary>시간 배율과 무관한 시간으로 카드 패널을 부드럽게 목표 위치까지 이동한다.</summary>
    private IEnumerator SlideTo(Vector2 targetPosition, bool show)
    {
        Vector2 startPosition = cardPanel.anchoredPosition;
        if (slideDuration <= 0f)
        {
            ApplyImmediate(targetPosition, show);
            slideRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / slideDuration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            cardPanel.anchoredPosition = Vector2.LerpUnclamped(
                startPosition,
                targetPosition,
                easedProgress);
            yield return null;
        }

        ApplyImmediate(targetPosition, show);
        slideRoutine = null;
    }

    /// <summary>애니메이션 없이 위치와 표시 상태, 입력 가능 여부를 즉시 일치시킨다.</summary>
    private void ApplyImmediate(Vector2 position, bool show)
    {
        cardPanel.anchoredPosition = position;
        IsShown = show;
        SetInteraction(show);
    }

    /// <summary>닫힌 카드가 보이지 않는 위치에서 클릭되는 것을 CanvasGroup으로 차단한다.</summary>
    private void SetInteraction(bool enabledInteraction)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = enabledInteraction;
        canvasGroup.blocksRaycasts = enabledInteraction;
    }

    /// <summary>현재 선택된 화면 요소가 문자 입력창이면 카드 패널 단축키를 무시할지 판단한다.</summary>
    private bool ShouldIgnoreShortcut()
    {
        if (!ignoreWhenEditingUI || EventSystem.current == null)
        {
            return false;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        return selectedObject != null &&
               (selectedObject.GetComponent<UnityEngine.UI.InputField>() != null ||
                selectedObject.GetComponent<TMPro.TMP_InputField>() != null);
    }
}
