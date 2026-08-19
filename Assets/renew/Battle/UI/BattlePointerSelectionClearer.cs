using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스로 전투 UI 버튼을 누른 뒤 EventSystem 선택을 해제한다.
/// 키보드·게임패드로 선택한 UI에는 개입하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePointerSelectionClearer : MonoBehaviour, IPointerClickHandler
{
    /// <summary>왼쪽 마우스 클릭으로 남은 UI 선택만 해제하여 이후 Space가 같은 버튼을 재실행하지 않게 한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }

    /// <summary>대상 버튼에 마우스 선택 해제 처리를 한 번만 연결한다.</summary>
    public static void Ensure(GameObject target)
    {
        if (target != null && target.GetComponent<BattlePointerSelectionClearer>() == null)
        {
            target.AddComponent<BattlePointerSelectionClearer>();
        }
    }
}
