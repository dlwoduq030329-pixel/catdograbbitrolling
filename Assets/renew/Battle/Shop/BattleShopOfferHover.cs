using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 상점 슬롯의 마우스 진입·이탈로 기존 설명 텍스트 표시를 요청하고, 선택적으로 클릭도 감지한다.
/// 카드 이미지 영역의 개별 "Button" 오브젝트는 프리팹 기본값이 비활성 상태라 클릭을 받을 수 없으므로
/// 슬롯 전체(항상 활성 상태인 루트)에서 클릭을 감지해 대신 전달한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleShopOfferHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // Action은 인터페이스가 아니라 나중에 실행할 인자·반환값 없는 함수(콜백)를 저장하는 delegate다.
    // BattleCardShopSystem이 각 슬롯에 맞는 표시·숨김·선택 함수를 Bind로 전달한다.
    private System.Action onEnter;
    private System.Action onExit;
    private System.Action onClick;

    /// <summary>
    /// 이 슬롯이 호버/클릭될 때 실행할 콜백을 등록한다. click은 생략 가능(null이면 클릭 무시).
    /// 세 콜백을 모두 새 값으로 덮어쓰는 방식이라 같은 슬롯에 다시 Bind를 호출해도 중복 등록되지 않는다.
    /// </summary>
    public void Bind(System.Action enter, System.Action exit, System.Action click = null)
    {
        onEnter = enter;
        onExit = exit;
        onClick = click;
    }

    /// <summary>마우스가 이 오브젝트 영역에 들어오면 등록된 onEnter 콜백을 호출한다.</summary>
    public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
    /// <summary>마우스가 이 오브젝트 영역을 벗어나면 등록된 onExit 콜백을 호출한다.</summary>
    public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke();
    /// <summary>클릭 시 등록된 onClick 콜백을 호출한다(null이면 아무 동작 없음).</summary>
    public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();

    /// <summary>
    /// 오브젝트가 비활성화될 때(슬롯이 재사용되거나 상점이 닫힐 때 등) 마우스가 여전히 올라가 있는
    /// 상태로 취급되는 걸 막기 위해 onExit을 강제로 한 번 더 호출한다 — OnPointerExit이 호출될
    /// 기회 없이 SetActive(false)되는 경우(예: 상점 새로고침) 설명 텍스트가 계속 떠 있는 걸 방지한다.
    /// 저장된 콜백을 null로 초기화하는 함수가 아니라, 등록된 이탈 동작을 강제로 실행하는 함수다.
    /// </summary>
    private void OnDisable() => onExit?.Invoke();
}
