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
    private System.Action onEnter;
    private System.Action onExit;
    private System.Action onClick;

    public void Bind(System.Action enter, System.Action exit, System.Action click = null)
    {
        onEnter = enter;
        onExit = exit;
        onClick = click;
    }

    public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke();
    public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();

    private void OnDisable() => onExit?.Invoke();
}
