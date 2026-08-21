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
    public event Action<Vector2> LeftClickRequested;
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
            // 빠른 연속 클릭도 각각 일반 좌클릭으로 전달한다. 이전 Debug 더블 클릭은
            // MP·경로·이동 확정 단계를 건너뛰고 Player를 타일로 순간이동시켜 정식 이동 규칙을 깨뜨렸다.
            LeftClickRequested?.Invoke(pointerPosition);
        }

        if (Input.GetMouseButtonDown(1))
        {
            RightClickRequested?.Invoke(pointerPosition);
        }
    }

    /// <summary>
    /// 모달이 열렸을 때는 모든 UI 레이캐스트 영역을 차단하고, 일반 HUD에서는 실제 조작
    /// UI만 차단한다. 일반 HUD의 전체 화면 장식 Image까지 항상 차단하면 맵 전체가 클릭
    /// 불가능해질 수 있으므로 두 상태를 구분한다.
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
        bool modalOpen = BattleGameManager.Instance != null &&
                         BattleGameManager.Instance.IsModalInteractionOpen;

        foreach (RaycastResult result in UiRaycastResults)
        {
            GameObject target = result.gameObject;
            if (target == null)
            {
                continue;
            }

            if (modalOpen)
            {
                UiRaycastResults.Clear();
                return true;
            }

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
