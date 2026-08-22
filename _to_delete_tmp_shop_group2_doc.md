
### BattleCardShopSystem.cs 그룹2 마무리: 장비 뽑기·구매 흐름 (193~441행)

상태: 리뷰 완료

- **추가로 발견한 죽은 코드**: `GetComparableEquipment`, `GetReplacementRefund`, `FormatEquipment`, `GetSlotRefund` 4개 메서드 — 저장소 전체 재검색해서 호출부 0개 확인, 삭제함. 방금 지운 `EnsureEquipmentConfirmationView`(제목이 "EQUIPMENT COMPARISON"였음)가 완성됐다면 "현재 장착 장비 vs 구매하려는 장비" 비교 텍스트를 만드는 데 썼을 헬퍼로 추정 — 같은 죽은 기능의 일부였던 것으로 보임. 사용자 확인 하에 삭제(직전 항목과 같은 정리 승인 범위로 처리).
- `PickEquipmentCandidate`/`IsEquipped`/`CreateRandomEquipment`/`BuildEligibleCards`/`Buy`/`BuyEquipment`에 요약 주석 추가. 특히 `PickEquipmentCandidate`는 부위 1차 추첨(RollEquipmentKind, 40/30/20/10) → 장착여부 2차 추첨(미장착 4배 우대) 흐름을 명시.
- 검증: 브레이스 156/156(184 → 168 → 156, 이번 세션에서 발견한 죽은 코드 누적 삭제 결과), 파일 1116줄 → 1083줄.
