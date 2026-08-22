
### BattleShopConfig.cs / BattleShopConfig.asset - 장비 부위 비율(equipmentByStage) 조정 (2026-08-22, 사용자 확인)

상태: 리뷰+수정 완료

- **발견한 버그**: 이전 필드 rename(hand→handWeight 등) 작업 때 `equipmentByStage` 기본값 배열 리터럴 하나를 놓쳤다 — 클래스 필드는 이미 `handWeight`로 바뀌었는데 이 배열은 옛 이름(`hand = 55, body = 20, ...`)을 그대로 쓰고 있어 **실제로는 컴파일이 안 되는 상태**였다. 비율 조정 작업 중 발견해서 같이 고쳤다.
- **비율 변경**: 사용자가 "무기(손) 50 / 갑옷(몸통) 20 / 머리 20 / 양손 10인데 비율이 이상하다"고 지적, 목표 수치 "40 30 20 10"을 직접 지정. 기존에는 스테이지가 오를수록 손 비중이 줄고(55→45→40) 양손 비중이 느는(5→15→20) 표였는데, 이번에 **손40 / 몸통30 / 머리20 / 양손10 고정 비율로 전체 스테이지(1~3) 동일하게 통일**했다.
- `Assets/renew/Battle/Shop/BattleShopConfig.cs`의 기본값 배열과 실제 로드되는 `Assets/renew/Battle/Resources/Battle/Shop/BattleShopConfig.asset`의 직렬화 값 둘 다 갱신(ScriptableObject는 저장된 .asset 값이 코드 기본값보다 우선 적용되므로 둘 다 고쳐야 실제 게임에 반영됨).
- 검증: `BattleShopConfig.cs` 브레이스 16/16, `.cs`·`.asset` 양쪽 옛 필드명(`hand:`/`body:`/`head:`/`twoHand:`)·옛 비율(55/45/40/20/5/15/20) 잔존 0건.

### 사용자 질문 답변 - RollRarity / RollEquipmentKind 참조 현황, BattleShopOfferHover

- **RollRarity 실제 호출부는 2곳**(5곳 추정과 다름): `BattleChestRewardSystem.cs:172`(상자 장비 보상 등급), `BattleCardShopSystem.cs:226`(`CreateRandomEquipment` 안, 상점 장비 등급) — 사용자 말대로 상점과 상자 둘 다 같은 `BattleShopStageRarityWeights` 표를 공유해서 쓴다.
- **RollEquipmentKind 호출부는 1곳**: `BattleCardShopSystem.cs:197`(`PickEquipmentCandidate` 안). 이 부위(kind)로 먼저 후보를 좁힌 뒤, 그 안에서 다시 "이미 장착 중인 부위=가중치1 vs 미장착=가중치4"로 2차 가중치 추첨을 한다(장착 안 한 부위가 4배 더 잘 나오게). "손이 유독 많이 나오던" 체감은 구 비율(손 55~40)이 다른 부위(고정 20)보다 훨씬 컸기 때문이 맞고, 이번 40/30/20/10 통일로 완화될 것이다.
- **BattleShopStageRarityWeights ≠ BattleShopStageEquipmentWeights**: 중복 클래스 아님. 서로 다른 두 축의 가중치 추첨 표 — 하나는 "등급"(common/rare/epic/legendary), 하나는 "장비 부위"(hand/body/head/twoHand)를 굴린다. 이름이 비슷해서 헷갈리기 쉬웠던 것.
- **BattleShopOfferHover**: 사용자 관찰대로 직접 `new`로 생성되는 곳은 없고(MonoBehaviour라 애초에 불가능), `BattleComponentResolver.GetOrAdd<BattleShopOfferHover>(...)`로 슬롯 GameObject에 붙인 뒤 `.Bind(enter, exit, click)`으로 호버/클릭 콜백만 등록해서 쓰는 순수 이벤트 구독용 컴포넌트가 맞다(`BattleCardShopSystem.cs:671, 996, 1039` 3곳에서 GetOrAdd).
