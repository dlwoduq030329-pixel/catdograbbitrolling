
### BattleCardShopSystem.cs 처음부터 재검토 3차(마지막): ShowOfferDetails~파일 끝

상태: 리뷰+수정 완료 — 이걸로 BattleCardShopSystem.cs 전체(1264줄 원본 → 1260줄) 1차 재검토 끝.

- `ShowOfferDetails`/`SelectOfferForPurchase`/`HideOfferDetails`: 서로 어떻게 얽혀 있는지 요약에 명시 — hover(마우스 올림)와 클릭(선택 고정) 두 경로가 같은 ShowOfferDetails를 부르고, "고정된 뒤엔 hover가 정보를 안 바꾸는" 이유(purchaseButtonMode 체크)까지 설명. `HideOfferDetails` 호출부 9곳이 왜 이렇게 많은지도 "정보를 지워야 하는 여러 상황(hover 이탈/닫기/구매·판매 확정/취소/빈 슬롯)의 공통 처리"라고 정리.
- `SetPreviewImage`: "참조 6개인데 뭐지" 하셨던 부분 — ShowOfferDetails/HideOfferDetails/SelectInventoryCardForSale/SelectEquipmentSlotForSale 등 "지금 뭘 보여줄지 바뀌는" 모든 지점이 공통으로 거치는 유일한 미리보기 온오프 지점이라는 걸 명시.
- `RefreshOwnedInventory` 안의 영문 주석(InventoryStore.OnPointerDown 관련) 한글화 — 전체 스캔 정규식이 처음에 놓쳤던 것(dotted 식별자라 패턴이 안 걸림), 이번에 잡아서 처리.
- `RefreshOwnedEquipmentSlot`: 왜 리플렉션으로 private 필드에 접근하는지(EquipStore.Init()이 DataPool.Instance를 요구해 Battle 상점에서 그대로 못 씀) 근거 명시.
- `GetEquippedIndex`: "왜 밑에 있지" 질문 답 — RefreshOwnedEquipmentSlot 하나에서만 쓰는 작은 매핑 헬퍼라 바로 아래 배치된 것, 문제 없는 배치.
- `SetTextVisible`/`GetTargetLabel`/`GetPropertyLabel`: 각각 용도 요약 추가(ShowOfferDetails 전용 헬퍼들).
- **함수명 변경**: `FindNamedComponent`→`FindComponentByName`, `FindNamedTransform`→`FindTransformByName`(각각 7곳/4곳 전체 치환, private 메서드라 다른 파일 영향 없음 확인) — "너무 어렵다, 처음 보는 구조"라고 하셔서 이름 검색 패턴이라는 의도가 드러나도록 재명명하고 설명 대폭 보강(레거시 프리팹을 이름으로 찾는 이유, 프리팹 구조 바뀌면 조용히 null 반환하는 주의점 포함).
- **영문 주석 최종 스캔**: TryBindSceneStoreView 안의 EscButton 관련 주석(4줄) 추가 발견해 한글화 — 정규식 스캔 방식을 바꿔가며(단어 3개 연속 패턴) 파일 전체를 다시 훑었고, 이제 영문 주석 0건 확정.

**최종 검증**: 브레이스 153/153. 이 세션에서 BattleCardShopSystem.cs 전체에 걸쳐 확인한 죽은 코드 총 6개(Configure/GetRarityColor/TryCreateLegacyView/EnsureEquipmentConfirmationView/SetNamedObjectActive/CreateText/CreateButton/ConfirmEquipmentPurchase·Left·Right/GetComparableEquipment/GetReplacementRefund/FormatEquipment/GetSlotRefund — 정확히는 14개 심볼, 모두 저장소 전체 재검색으로 참조 0건 확인 후 삭제) + 진짜 중복 호출 1건(RefreshOwnedInventory) 수정 + 기능 추가 1건(상점 타일 재진입 잠금).
