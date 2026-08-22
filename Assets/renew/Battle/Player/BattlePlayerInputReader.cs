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
    // RaycastAll 결과를 매 호출마다 새로 할당하지 않기 위한 재사용 버퍼(정적이라 프레임마다 GC 압박 없음).
    private static readonly List<RaycastResult> uiRaycastResultsBuffer = new List<RaycastResult>();

    [Header("디버그 입력")]
    [InspectorName("이동·공격 사거리 토글 키")]
    [SerializeField] private KeyCode rangeToggleKey = KeyCode.R;
    public event Action<Vector2> LeftClickRequested;
    public event Action<Vector2> RightClickRequested;
    public event Action CancelRequested;
    /// <summary>Player를 클릭하지 않고도 단축키로 이동·공격 사거리를 켜고 끌 때 발생한다.</summary>
    public event Action RangeToggleRequested;

    /// <summary>
    /// 매 프레임 마우스·키보드 원시 입력을 읽어 이벤트로 전달만 한다. 클릭 좌표가 실제로
    /// 이동/공격/취소 중 무엇을 의미하는지는 이 클래스가 아니라 구독자(BattlePlayerActionController 등)가 판단한다.
    /// </summary>
    private void Update()
    {
        // 모달(장비 상점 등)이 열려 있는 동안은 배틀 화면 입력을 전부 무시한다.
        // 그렇지 않으면 모달 뒤에 가려진 타일·Player를 실수로 클릭/취소/사거리 토글할 수 있다.
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
            // 클릭 1회 = 좌클릭 이벤트 1회. 더블클릭이나 연타를 별도로 묶어서 처리하지 않고
            // 매번 그대로 전달하며, 그 클릭이 이동 확정인지 취소인지는 구독자가 결정한다.
            LeftClickRequested?.Invoke(pointerPosition);
        }

        if (Input.GetMouseButtonDown(1))
        {
            RightClickRequested?.Invoke(pointerPosition);
        }
    }

    /// <summary>
    /// 모달(ui)이 열렸을 때는 모든 UI 레이캐스트 영역을 차단하고, 일반 HUD에서는 실제 조작
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

        // 이 좌표에서 EventSystem 기준으로 화면 위에 쌓인 모든 UI 요소를 위(앞)부터 순서대로 가져온다.
        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = pointerPosition
        };

        uiRaycastResultsBuffer.Clear();
        eventSystem.RaycastAll(pointerData, uiRaycastResultsBuffer);
        bool isModalOpen = BattleGameManager.Instance != null &&
                         BattleGameManager.Instance.IsModalInteractionOpen;

        foreach (RaycastResult raycastResult in uiRaycastResultsBuffer)
        {
            GameObject hitUiObject = raycastResult.gameObject;
            if (hitUiObject == null)
            {
                continue;
            }

            // 모달이 열려 있으면 종류를 따지지 않고 이 좌표에 UI가 하나라도 걸렸다는 사실만으로 차단한다.
            // (모달 위의 장식용 배경 Image까지도 뒤쪽 배틀 화면 클릭을 막아야 하기 때문)
            if (isModalOpen)
            {
                uiRaycastResultsBuffer.Clear();
                return true;
            }

            // 일반 HUD에서는 "장식만 있고 조작은 안 되는" UI(전체 화면 배경 Image 등)까지 차단하면
            // 맵 전체가 클릭 불가능해지므로, 실제로 조작 가능한 UI인지 3가지 기준으로만 판단한다.
            Selectable interactiveSelectable = hitUiObject.GetComponentInParent<Selectable>();
            ScrollRect interactiveScrollRect = hitUiObject.GetComponentInParent<ScrollRect>();
            bool hasDirectPointerHandler =
                hitUiObject.GetComponent<IPointerClickHandler>() != null ||
                hitUiObject.GetComponent<IDragHandler>() != null ||
                hitUiObject.GetComponent<IScrollHandler>() != null ||
                hitUiObject.GetComponent<ISubmitHandler>() != null;

            bool isInteractableSelectable = interactiveSelectable != null && interactiveSelectable.IsActive() && interactiveSelectable.IsInteractable();
            bool isActiveScrollRect = interactiveScrollRect != null && interactiveScrollRect.isActiveAndEnabled;

            if (isInteractableSelectable || isActiveScrollRect || hasDirectPointerHandler)
            {
                uiRaycastResultsBuffer.Clear();
                return true;
            }
        }

        uiRaycastResultsBuffer.Clear();
        return false;
    }
}
