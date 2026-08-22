
### BattleShopOfferHover.cs 주석 보강 (2026-08-22)

상태: 리뷰 완료

- 클래스 요약은 이미 있었으나 `Bind`/`OnPointerEnter`/`OnPointerExit`/`OnPointerClick`/`OnDisable`에 요약 주석이 없어서 추가했다.
- 특히 `OnDisable`에 왜 onExit을 강제로 한 번 더 호출하는지(슬롯이 OnPointerExit 기회 없이 SetActive(false)될 때 설명 텍스트가 계속 떠 있는 걸 방지) 근거를 남김.
- 필드/메서드 이름 변경 없음(이미 직관적). 브레이스 2/2 확인.
- 참고: `BattleComponentResolver.GetOrAdd<BattleShopOfferHover>(...)` 3곳(`BattleCardShopSystem.cs:671, 996, 1039`)에서 슬롯 GameObject에 붙이고 `.Bind()`로만 쓰는 순수 이벤트 구독용 컴포넌트 — 직접 `new`로 생성되는 곳은 없음(MonoBehaviour라 애초에 불가).

### BattleCardShopSystem.cs (1264줄) 리뷰 착수 - 그룹 분할 계획

상태: 진행중

메서드 맵 기준 6개 그룹으로 나눠서 순서대로 리뷰하기로 함:
1. 생명주기/진입: Update, Awake, Configure, TryEnter, GenerateOffers (68~192행)
2. 장비 제안 생성·구매: PickEquipmentCandidate, CreateRandomEquipment, BuildEligibleCards, Buy/BuyEquipment, ConfirmEquipmentPurchase 계열, ApplyEquipmentVisual, CancelEquipmentPurchase 등 (193~432행)
3. 리롤/닫기/모달락/뷰 갱신: Reroll, Close, ForceClose, AcquireModalLock/ReleaseModalLock, RefreshView, TryBindSceneStoreView 등 (433~699행)
4. 선택·판매 흐름: ShowOfferDetails, Select* 계열, BuySelectedOffer, SellSelectedCard/Equipment, 보호된 시작 카드 판정 등 (700~950행)
5. UI 표시 갱신: SetPreviewImage, RefreshOwnedInventory/RefreshOwnedEquipmentSlot, 라벨 헬퍼들 (960~1088행)
6. 레거시 뷰 생성 및 UI 헬퍼: TryCreateLegacyView, FindNamedTransform, EnsureEquipmentConfirmationView, CreateText/CreateButton (1089~1264행)
