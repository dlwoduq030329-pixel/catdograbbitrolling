
### BattleCardShopSystem.cs 처음부터 재검토 2차: Buy~EnsureView

상태: 리뷰+수정 완료

- `Buy`: 변수명 `index`→`cardIndex`, `card`→`originalCard`, `owned`→`ownedCount`(+ shadow `count`→`currentOwnedCount`)로 정리, 보유 한도 재확인 이유(BuildEligibleCards가 걸러내지만 fallback 재사용 등으로 뚫릴 가능성 방어) 명시.
- `Reroll`: 리롤이 슬롯 일부가 아니라 6슬롯 전체를 GenerateOffers로 완전히 다시 뽑는다는 점, 가격 상한(shopConfig.maximumRerollPrice)을 명시. **사용자 오해 정정**: "Close 이때도 리롤 실행되네"라고 봤는데, 실제로 `Close()`는 `Reroll()`을 호출하지 않는다(둘은 완전히 별개) — 요약 주석에 명시적으로 적어둠.
- `Close()` vs `ForceClose()` "왜 이중이지?" 질문 답: `Close()`는 ESC/닫기버튼이 쓰는 내부 정상 종료 경로, `ForceClose()`는 `BattleGameManager.cs:477-478`에서 `ChestRewardSystem.ForceClose()`와 나란히 호출하는 외부 강제 종료 API(전투 종료/사망 시 열려있는 오버레이 일괄 정리용) — 실제 호출부 확인 완료, 지금은 동작이 같지만 나중에 분기될 여지를 위해 분리된 구조로 보임.
- `AcquireModalLock`/`ReleaseModalLock`: 상점 열려있는 동안 전투 입력 잠그는 용도, `holdsModalLock`으로 중복 잠금/해제 방지. 요약 추가.
- `RefreshView`: 상태(currentState) → 화면 그리기 담당, 구매/판매/리롤/진입 전부 마지막에 이걸 호출하는 구조라고 명시.
- `FillExistingEmptySlots` "구매했을 때 호출되는 거냐" 질문 답: 구매 전용이 아니라 RefreshView가 호출될 때마다(=거의 모든 액션 후) 매번 도는 안전망이다. GenerateOffers의 fallback과 달리 스냅샷이 아니라 호출 시점 최신 보유 현황으로 다시 계산한다는 차이를 명시. 변수명 `cards`/`equipment` → `eligibleCards`/`eligibleEquipment`로 정리(같은 이름의 필드/다른 메서드 변수와 혼동 방지).
- `EnsureView` 요약 추가: viewRoot가 없을 때만 1회 TryBindSceneStoreView 실행.

검증: 브레이스 153/153 유지.
