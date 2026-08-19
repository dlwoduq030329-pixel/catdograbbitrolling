using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 화면의 좌클릭, 우클릭, ESC 입력을 감지해 화면 좌표와 함께 전달한다.
/// 입력에 따른 이동, 공격, 취소 규칙은 판단하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerInputReader : MonoBehaviour
{
    private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();

    [Header("디버그 입력")]
    [InspectorName("이동·공격 사거리 토글 키")]
    [SerializeField] private KeyCode rangeToggleKey = KeyCode.R;
    [InspectorName("디버그 더블 클릭 간격")]
    [SerializeField, Min(0.1f)] private float doubleClickInterval = 0.3f;
    [InspectorName("더블 클릭 허용 이동 거리")]
    [SerializeField, Min(0f)] private float doubleClickPixelTolerance = 24f;

    private float lastLeftClickTime = float.NegativeInfinity;
    private Vector2 lastLeftClickPosition;

    public event Action<Vector2> LeftClickRequested;
    public event Action<Vector2> DoubleLeftClickRequested;
    public event Action<Vector2> RightClickRequested;
    public event Action CancelRequested;
    /// <summary>Player를 클릭하지 않고도 단축키로 이동·공격 사거리를 켜고 끌 때 발생한다.</summary>
    public event Action RangeToggleRequested;

    private void Update()
    {
        if (BattleGameManager.Instance != null && BattleGameManager.Instance.IsModalInteractionOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelRequested?.Invoke();
        }

        if (Input.GetKeyDown(rangeToggleKey))
        {
            RangeToggleRequested?.Invoke();
        }

        Vector2 pointerPosition = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            bool isDoubleClick = Time.unscaledTime - lastLeftClickTime <= doubleClickInterval &&
                                 Vector2.Distance(pointerPosition, lastLeftClickPosition) <= doubleClickPixelTolerance;
            lastLeftClickTime = Time.unscaledTime;
            lastLeftClickPosition = pointerPosition;

            if (isDoubleClick)
                DoubleLeftClickRequested?.Invoke(pointerPosition);
            else
                LeftClickRequested?.Invoke(pointerPosition);
        }

        if (Input.GetMouseButtonDown(1))
        {
            RightClickRequested?.Invoke(pointerPosition);
        }
    }

    /// <summary>
    /// 장식용 Image는 통과시키고 실제 입력을 처리하는 UI 위에서만 전투 맵 입력을 차단한다.
    /// </summary>
    public static bool IsPointerOverInteractiveUI(Vector2 pointerPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = pointerPosition
        };

        UiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, UiRaycastResults);

        foreach (RaycastResult result in UiRaycastResults)
        {
            GameObject target = result.gameObject;
            if (target == null)
            {
                continue;
            }

            // 부모 전체에서 이벤트 핸들러를 찾으면 화면을 덮는 UI 컨테이너 하나 때문에
            // 그 아래의 장식 Image까지 모두 전투 입력을 막는 문제가 생긴다.
            // 실제 조작 UI(버튼·선택 항목·스크롤)와 해당 오브젝트가 직접 처리하는 입력만 차단한다.
            Selectable selectable = target.GetComponentInParent<Selectable>();
            ScrollRect scrollRect = target.GetComponentInParent<ScrollRect>();
            bool hasDirectInputHandler =
                target.GetComponent<IPointerClickHandler>() != null ||
                target.GetComponent<IDragHandler>() != null ||
                target.GetComponent<IScrollHandler>() != null ||
                target.GetComponent<ISubmitHandler>() != null;

            if ((selectable != null && selectable.IsActive() && selectable.IsInteractable()) ||
                (scrollRect != null && scrollRect.isActiveAndEnabled) ||
                hasDirectInputHandler)
            {
                UiRaycastResults.Clear();
                return true;
            }
        }

        UiRaycastResults.Clear();
        return false;
    }
}
