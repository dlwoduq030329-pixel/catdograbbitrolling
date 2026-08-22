
### BattleCardShopSystem.cs - 장비구매 확인 UI 죽은 코드 대량 제거 (2026-08-22, 사용자 확인)

상태: 리뷰+수정 완료 (그룹2/3/6에 걸친 발견이라 그룹 순서와 무관하게 즉시 처리)

**발견 경위**: `BattleShopOfferHover`가 정말 안 쓰는 컴포넌트인지 확인하다가(3곳에서 실제 사용 중, 죽은 코드 아님) 옆에서 훨씬 큰 문제를 발견함 — Chest에서 지웠던 "왼손/오른손 직접 선택" 확인 UI(OpenEquipmentChoice)와 완전히 같은 패턴이 Shop에도 그대로 남아 있었음.

**호출부 0개로 확인된 죽은 코드(reachability tracing으로 실제 추적, 구조적 스캔 아님)**:
- `TryCreateLegacyView()` (~100줄): `EnsureView()`는 `TryBindSceneStoreView()`만 호출하고 이 메서드는 어디서도 호출 안 됨.
- `EnsureEquipmentConfirmationView()`: 호출부 0개(TryCreateLegacyView에서조차 호출 안 됨, 완전히 고립됨). 이게 만들려던 "왼손/오른손/취소" 확인 패널이 실제로는 한 번도 생성된 적이 없었다는 뜻.
- `SetNamedObjectActive()`: TryCreateLegacyView 안에서만 쓰였음 → 같이 죽음.
- `CreateText()`, `CreateButton()`: TryCreateLegacyView + EnsureEquipmentConfirmationView 안에서만 쓰였음 → 같이 죽음.
- `ConfirmEquipmentPurchase()`, `ConfirmEquipmentPurchaseLeft()`, `ConfirmEquipmentPurchaseRight()`: EnsureEquipmentConfirmationView의 onClick 리스너로만 연결되던 3개 래퍼 → 같이 죽음.
- 필드 6개: `equipmentConfirmPanel`, `equipmentConfirmText`, `equipmentConfirmButton`, `equipmentLeftButton`, `equipmentRightButton`, `equipmentCancelButton` — 전부 죽은 초기화 메서드에서만 대입되던 필드라 항상 null이었음.

**실제 동작(수정 전부터 이미 이랬음, 이번에 코드로 확정한 것뿐)**: `BuyEquipment(slot)`는 `pendingEquipmentSlot`만 세팅하고 확인창 없이 곧바로 `ConfirmEquipmentPurchaseInHand(null)`을 호출 — 항상 `DataConfig.GetWeapon` 경로(빈 손 자동 장착, 양손 다 차있으면 왼손 강제 교체)로 즉시 구매+장착됨. `equipLeft` 매개변수와 `weaponKind == Hand` 분기는 특정 손 강제 지정 기능을 되살릴 때를 위해 그대로 남겨둠(주석에 근거 명시).

**부수 발견 - 잔존 버그 노트(이번엔 수정 안 함, 관찰만)**: `ConfirmEquipmentPurchaseInHand`가 골드 부족으로 조기 반환하면 `pendingEquipmentSlot`이 slot 값 그대로 남는다. 그래서 `Update()`의 ESC 처리가 `CancelEquipmentPurchase()`를 먼저 타면서 상점을 안 닫고 대기 슬롯만 리셋 — 골드 부족으로 구매 실패한 직후 ESC를 한 번 더 눌러야 상점이 닫힌다는 뜻. 원래도 있던 동작이라 이번 정리 범위 밖으로 두고 관찰만 기록.

**검증**: 브레이스 168/168(기존 184/184에서 감소, 삭제된 메서드/필드만큼), 파일 1282줄 → 1116줄. 삭제한 심볼(`TryCreateLegacyView`/`EnsureEquipmentConfirmationView`/`equipmentConfirmPanel`/`ConfirmEquipmentPurchaseLeft`/`ConfirmEquipmentPurchaseRight` 등) 저장소 전체에서 재검색 — 이 파일의 설명 주석 속 언급 2곳 외 실제 코드 참조 0건(다른 파일도 0건). `FindNamedComponent`/`FindNamedTransform`은 TryBindSceneStoreView가 실제로 쓰고 있어 그대로 유지.
