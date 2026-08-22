
### BattleCardShopSystem.cs 그룹1 리뷰: Update / Awake / Configure / TryEnter / GenerateOffers (68~192행)

상태: 리뷰 완료

- **영문 주석 전량 한글화**: 이 파일 전체를 훑어 영문 주석 5곳을 찾아 전부 번역했다 — GenerateOffers 안의 "빈 슬롯 채우기" 설명 1곳(그룹1), TryBindSceneStoreView 안의 "Button이 기본 비활성이라 hover로 클릭 릴레이" 설명 1곳(그룹3), TryCreateLegacyView/EnsureEquipmentConfirmationView 안의 "런타임 리스너만 제거·persistent 콜백 유지" 설명 2곳(그룹6). 표준 지침(설명은 항상 한글로)에 맞춰 그룹 순서와 상관없이 발견 즉시 전부 처리함.
- **Update**: ESC 입력을 단계적으로 처리한다 — 장비구매 확인창(`pendingEquipmentSlot != -1`)이 열려있으면 그 선택만 취소, 카드/장비 선택 미리보기(`purchaseButtonMode != None`)만 열려있으면 그것만 취소, 아무것도 없으면 상점 전체를 닫는다. 요약 주석 추가.
- **Awake**: 장비 데이터베이스·상점설정(BattleShopConfig)을 Resources에서 한 번 로드. 요약 추가.
- **TryEnter**: 같은 상점 타일을 처음 방문하면 `GenerateOffers`로 새 진열 목록을 뽑아 `stores` 딕셔너리에 저장, 이미 방문했으면 저장된 상태를 그대로 재사용 — 같은 상점을 다시 들어가도 목록이 안 바뀌는 이유가 여기 있음. 요약 추가.
- **GenerateOffers**: 6슬롯에 카드/장비 종류를 shopConfig 비율대로 배치·셔플한 뒤 슬롯마다 후보를 하나씩 뽑아 소모(중복 방지), 후보 부족으로 빈 슬롯이 남으면 fallback 목록(원본, 소모 안 된 목록)에서 재사용해 채운다. 요약 추가.
- 검증: 브레이스 184/184, 영문 주석 잔존 0건(전체 스캔 기준).

### BattleCardShopSystem.cs 리뷰 착수 - 그룹 분할 계획

상태: 진행중

메서드 맵 기준 6개 그룹으로 나눠서 순서대로 리뷰하기로 함:
1. 생명주기/진입: Update, Awake, Configure, TryEnter, GenerateOffers (68~192행) - **완료**
2. 장비 제안 생성·구매: PickEquipmentCandidate, CreateRandomEquipment, BuildEligibleCards, Buy/BuyEquipment, ConfirmEquipmentPurchase 계열, ApplyEquipmentVisual, CancelEquipmentPurchase 등 (193~432행)
3. 리롤/닫기/모달락/뷰 갱신: Reroll, Close, ForceClose, AcquireModalLock/ReleaseModalLock, RefreshView, TryBindSceneStoreView 등 (433~699행)
4. 선택·판매 흐름: ShowOfferDetails, Select* 계열, BuySelectedOffer, SellSelectedCard/Equipment, 보호된 시작 카드 판정 등 (700~950행)
5. UI 표시 갱신: SetPreviewImage, RefreshOwnedInventory/RefreshOwnedEquipmentSlot, 라벨 헬퍼들 (960~1088행)
6. 레거시 뷰 생성 및 UI 헬퍼: TryCreateLegacyView, FindNamedTransform, EnsureEquipmentConfirmationView, CreateText/CreateButton (1089~1264행)
