# -*- coding: utf-8 -*-
def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

p = "Assets/renew/Battle/Shop/BattleCardShopSystem.cs"
content = load(p)

def must_replace(content, old, new, label):
    count = content.count(old)
    assert count == 1, (label, count, old[:150])
    return content.replace(old, new, 1)

# 1) 죽은 필드 6개 제거 (equipmentConfirmPanel ~ equipmentCancelButton). pendingEquipmentSlot은 살아있으므로 유지.
content = must_replace(content,
'''    private GameObject equipmentConfirmPanel;
    private TMP_Text equipmentConfirmText;
    private Button equipmentConfirmButton;
    private Button equipmentLeftButton;
    private Button equipmentRightButton;
    private Button equipmentCancelButton;
    private int pendingEquipmentSlot = -1;''',
'''    private int pendingEquipmentSlot = -1;''',
    "fields")

# 2) ConfirmEquipmentPurchase / Left / Right 래퍼 3개 제거 (죽은 EnsureEquipmentConfirmationView에서만 onClick으로 연결되던 것들).
#    ConfirmEquipmentPurchaseInHand(실제 구매 로직)는 유지 - BuyEquipment가 직접 호출하는 살아있는 메서드.
content = must_replace(content,
'''    private void ConfirmEquipmentPurchase()
    {
        ConfirmEquipmentPurchaseInHand(null);
    }

    private void ConfirmEquipmentPurchaseLeft()
    {
        ConfirmEquipmentPurchaseInHand(true);
    }

    private void ConfirmEquipmentPurchaseRight()
    {
        ConfirmEquipmentPurchaseInHand(false);
    }

    private void ConfirmEquipmentPurchaseInHand(bool? equipLeft)''',
'''    /// <summary>
    /// 장비 구매를 실제로 처리한다. equipLeft는 항상 null로 호출된다(2026-08-22 정리, 사용자 확인:
    /// "왼손/오른손 직접 선택" 확인 UI(EnsureEquipmentConfirmationView + Confirm/Left/Right/Cancel)는
    /// 호출부가 0개라 한 번도 생성된 적 없는 죽은 코드였다 — BuyEquipment가 확인창 없이 곧바로
    /// 이 메서드를 null로 호출해, 항상 DataConfig.GetWeapon 경로(빈 손 자동 장착, 양손이 다
    /// 차있으면 왼손 강제 교체)로 구매+장착이 즉시 끝난다. equipLeft 매개변수와
    /// weaponKind == Hand 분기는 특정 손을 강제 지정하는 기능을 되살릴 때를 위해 남겨둔다.)
    /// </summary>
    private void ConfirmEquipmentPurchaseInHand(bool? equipLeft)''',
    "confirm-wrappers")

# 3) equipmentConfirmPanel 참조하던 가드 문 2곳 제거 (필드 삭제로 인해 무의미해짐)
content = must_replace(content,
'''        pendingEquipmentSlot = -1;
        if (equipmentConfirmPanel != null) equipmentConfirmPanel.SetActive(false);
        Debug.Log($"[Shop] Equipment purchased: {equipment.cardname} ({equipment.weapon}) / {price}G", this);''',
'''        pendingEquipmentSlot = -1;
        Debug.Log($"[Shop] Equipment purchased: {equipment.cardname} ({equipment.weapon}) / {price}G", this);''',
    "guard-1")

content = must_replace(content,
'''    private void CancelEquipmentPurchase()
    {
        pendingEquipmentSlot = -1;
        if (equipmentConfirmPanel != null) equipmentConfirmPanel.SetActive(false);
    }''',
'''    /// <summary>
    /// ESC 등으로 대기중인 장비구매를 취소한다. pendingEquipmentSlot만 되돌리면 되는 이유는
    /// 위 ConfirmEquipmentPurchaseInHand 설명대로 확인 UI 자체가 죽은 코드라 애초에 열리는 패널이
    /// 없기 때문이다 — Update()가 이 메서드를 호출하는 건 재화 부족 등으로 ConfirmEquipmentPurchaseInHand가
    /// 조기 반환해 pendingEquipmentSlot이 slot 값 그대로 남아있는 경우를 되돌리기 위해서다.
    /// </summary>
    private void CancelEquipmentPurchase()
    {
        pendingEquipmentSlot = -1;
    }''',
    "guard-2")

# 4) TryCreateLegacyView 전체 제거 (호출부 0개, 완전 죽은 코드). FindNamedComponent는 살아있으므로 유지.
start_marker = "    /// <summary>레거시 Store 프리팹의 외형과 배열만 재사용하고 구매 규칙은 Battle 상점이 담당한다.</summary>\n    private bool TryCreateLegacyView()\n"
end_marker = "    private static T FindNamedComponent<T>(Transform root, string objectName) where T : Component\n"
start_idx = content.index(start_marker)
end_idx = content.index(end_marker)
assert start_idx < end_idx
removed_note = (
    "    // (2026-08-22 정리, 사용자 확인: TryCreateLegacyView 삭제됨 - EnsureView()는 TryBindSceneStoreView()만\n"
    "    // 호출하고 이 메서드는 어디서도 호출되지 않는 완전한 죽은 코드였다. 씬에 이미 배치된 Event_Store\n"
    "    // 인스턴스를 못 찾는 경우 EnsureView()는 그냥 오류 로그만 남기고 실패한다.)\n\n"
)
content = content[:start_idx] + removed_note + content[end_idx:]

# 5) SetNamedObjectActive + EnsureEquipmentConfirmationView + CreateText + CreateButton 전체 제거
#    (전부 위에서 지운 TryCreateLegacyView/EnsureEquipmentConfirmationView에서만 쓰이던 것들).
#    FindNamedTransform은 살아있으므로 유지, 클래스 닫는 마지막 '}'는 유지.
start_marker2 = "    private static void SetNamedObjectActive(Transform root, string objectName, bool active)\n"
tail_marker = "\n}\n"
start_idx2 = content.index(start_marker2)
end_idx2 = content.rindex(tail_marker)
content = content[:start_idx2] + content[end_idx2 + 1:]

save(p, content)
print("done, new length:", len(content))
