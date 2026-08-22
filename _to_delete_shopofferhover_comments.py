# -*- coding: utf-8 -*-
def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

def apply(path, replacements):
    content = load(path)
    for i, (old, new) in enumerate(replacements, start=1):
        count = content.count(old)
        assert count == 1, (path, i, count, old[:120])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

p = "Assets/renew/Battle/Shop/BattleShopOfferHover.cs"
apply(p, [
    (
'''    private System.Action onEnter;
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

    private void OnDisable() => onExit?.Invoke();''',
'''    private System.Action onEnter;
    private System.Action onExit;
    private System.Action onClick;

    /// <summary>
    /// 이 슬롯이 호버/클릭될 때 실행할 콜백을 등록한다. click은 생략 가능(null이면 클릭 무시).
    /// 매번 새로 덮어쓰는 방식이라, 슬롯 내용이 갱신될 때마다 다시 Bind를 호출해도 안전하다.
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
    /// 기회 없이 SetActive(false)되는 경우(예: 상점 새로고침) 설명 텍스트가 계속 떠 있는 걸 방지.
    /// </summary>
    private void OnDisable() => onExit?.Invoke();'''
    ),
])
