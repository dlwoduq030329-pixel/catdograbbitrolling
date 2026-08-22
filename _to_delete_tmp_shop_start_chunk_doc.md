
### BattleCardShopSystem.cs 처음부터 재검토 1차 (필드~GenerateOffers, 사용자 전체 피드백 반영)

상태: 리뷰+수정 완료(1차 구간). 사용자가 파일 전체를 훑고 한 번에 준 피드백을 처음부터 순서대로 처리 중.

**기능 추가**:
- `TryEnter`에 `enteredStores`(HashSet<MapInfo>) 추가 — 한 번 성공적으로 들어간 상점 타일은 재진입 자체를 막는다(사용자 확인: "타일 재진입 자체를 막기"). Chest의 `openedTiles` 패턴과 동일. `BattleUnitMoveFlow.cs:210`에서 이동이 끝날 때 한 번만 TryEnter를 호출하는 걸 확인해서, 같은 방문 중에 자기 자신을 잠그는 문제는 없음을 검증함.

**추가로 발견한 죽은 코드 2개(호출부 0개, 삭제)**:
- `Configure(BattleCardDatabase, CardDatabase)` — `battleCardDatabase`/`originalCardDatabase`는 이미 `[SerializeField]`라 인스펙터에서 직접 설정되고, 이 메서드를 부르는 곳은 저장소 전체에 없었음.
- `GetRarityColor(weaponSt)` — 등급별 색상을 표시하려던 흔적으로 보이나 UI에 연결된 적이 없음.

**확인된 실제 중복 호출(수정)**: `TryBindSceneStoreView` 끝의 `RefreshOwnedInventory()` — 이 메서드가 끝나자마자 `TryEnter`가 `RefreshView()`를 부르고 그 안에서 다시 `RefreshOwnedInventory()`가 호출돼 매번 인벤토리를 두 번 그리고 있었음(사용자가 "이중 로더" 아니냐고 지적한 부분, 확인 결과 진짜 중복이었음 — 제거).

**사용자가 죽은 코드로 의심했지만 실제로는 살아있는 것들(정정)**:
- `EnsureView()`: "debug용, 삭제해도 무방"이라고 했지만 실제로는 `TryEnter`가 매번 호출하는 핵심 진입점(1회만 실제로 뷰를 생성, `viewRoot != null`이면 조기 반환). 삭제 불가.
- `IsProtectedStartingCard`: "의미없는 코드"로 봤지만 실제로는 2곳에서 각각 다른 목적으로 쓰임 — `SelectInventoryCardForSale`에서 UI 단계 차단(LOCKED 표시), `SellSelectedCard`에서 실행 직전 재확인(선택 이후 상태가 바뀌었을 가능성 방어). 이중 방어 패턴이라 중복이 아님.
- `ResolveCurrentPlayerDeck`: "죽은 코드 아니냐"고 했지만 `IsProtectedStartingCard`/`SynchronizePlayerCardData` 2곳에서 실제로 씀. "구형 Scene 비활성 객체 검색으로 보완"은 `ApplyEquipmentVisual`의 CharacterListUIStatusController 검색과 같은, 이 코드베이스 전반에 있는 방어적 폴백 패턴.

**주석/설명 보강**:
- `StoreState` 클래스 및 필드(Kinds/OfferedCards/OfferedEquipment/Sold/RerollPrice), `stores` 딕셔너리, `shopConfig`, `viewRoot`, `hoverPreviewImage`, `ownedCardSlots`/`ownedEquipmentSlots`, `PurchaseButtonMode`(None이 왜 있는지 포함) 등 필드 전반에 요약 주석 추가.
- `GenerateOffers`: `cardCandidates`/`equipmentCandidates`(소모되는 실제 후보) vs `cardFallbacks`/`equipmentFallbacks`(소모 전 스냅샷, 최후 수단이라 중복 허용) 구분을 명확히 설명. 사용자가 기억하는 "장비가 아예 안 나오는 버그"에 대한 근거 분석도 추가: fallback 분기는 카드/장비 후보가 동시에 바닥났을 때만 실행되는데, 장비 후보는 DB 전체 수만큼 있어 거의 안 바닥나고 카드 후보(보유 한도)가 원인인 경우가 대부분 — 그런데 fallback이 카드를 항상 먼저 쓰게 되어 있어 그 순간 장비 확률이 낮아지는 구조. 발생 자체는 드묾(카드+장비 동시 고갈 필요).
- `PickEquipmentCandidate` 변수명 정리: `preferred`→`preferredKind`, `pool`→`kindFilteredPool`, `totalWeight`→`totalEquipWeight`, `roll`→`weightedRoll`, 1차/2차 추첨 단계 주석 추가.

검증: 브레이스 153/153, 파일 1083줄 → 1155줄(주석 추가로 증가). 삭제한 두 심볼 저장소 전체 재검색, 잔존 참조 0건.
