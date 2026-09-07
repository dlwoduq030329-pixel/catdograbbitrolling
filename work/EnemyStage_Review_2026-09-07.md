# Enemy / Stage 변경 리뷰

적 데이터 → StageEnemyBridge(검증·추첨) → StageSpawner(타일 배치) → EnemySpawner(대여·등록) → EnemyPool 순서입니다.

## 설정과 책임

- StageObjData: Stage 번호, DB, 등급별 수량·후보 ID, 시작 안전 반경만 저장합니다.
- StageEnemyBridge: 유효하지 않은 후보만 제외하고, 종류별 풀 여유 수량 안에서 실제 목록을 만듭니다. 오류 메시지는 호출자가 Stage 준비 시 한 번 경고합니다.
- EnemyPool: 일반은 종류별 5, 정예는 종류별 2, 보스는 1개를 준비합니다. 현재 DB 구성은 총 30개입니다. 자동 확장은 없으며 Inspector 수량은 Play 시작 전에 변경합니다.
- EnemySpawner: Stage 배치와 소환의 공통 진입점입니다. SpawnEnemy(tile, data)가 null을 반환하면 해당 종류의 풀 소진 또는 입력 누락으로 생성하지 않은 것입니다.
- EnemyRuntimeFactory: 최초 생성 시 전투 컴포넌트·HP바를 조립합니다.
- EnemySpawnGeometry: 최초 생성 시 크기·발 위치·클릭 Collider를 보정합니다. 재사용 시에는 스케일을 다시 곱하지 않습니다.
- BattleEnemyDeathHandler: 사망 연출 후 반환합니다. 다음 대여 때 투명도, 사망 시 비활성화했던 컴포넌트와 Collider를 복원합니다. 원래 꺼져 있던 컴포넌트는 켜지 않습니다.

재대여 시 체력·보호막·MP, 상태이상, 어그로, 배회 시작점 및 행동 상태를 초기화합니다. 참가 등록과 해제는 기존 Registry를 사용하고, 사망 대기열은 재등록 전에 비웁니다.

## 포털 전환

Player의 이동 완료 목적지가 Exit 타일이면 페이드 아웃 → 기존 적 반환 → 새 맵 생성 → 새 타일 Registry 등록 → 동일 Player를 시작점으로 이동 → 적 배치 → 페이드 인 → Stage 안내 순으로 진행합니다.

RegisterPlayer와 StartPlayerTurn을 다시 호출하지 않습니다. 골드·카드·장비·HP/MP·현재 턴 및 그 턴에 이미 사용한 이동/행동 상태가 유지됩니다. 모델의 타일 대비 발 높이도 보존합니다. 이전 맵의 착지 강조·허수아비·성역 연출은 정리합니다.

현재는 포털 도착이 전환 조건입니다. 모든 적 처치 조건은 추가하지 않았습니다. 4Stage 출구에서는 finalStageExited 이벤트를 한 번 호출하며, 결과 화면은 아직 연결하지 않았습니다. 보스의 실제 소환 스킬 실행 코드는 이번 변경에 포함하지 않았습니다.

## 수정 파일

Stage 폴더: StageObjData, StageEnemyBridge, StageSpawner, StagePlacement, EnemyPool, EnemyRuntimeFactory, EnemySpawnGeometry, StageTransitionController.

연결 및 생명주기: EnemySpawner, BattleEnemyDeathHandler, EnemyTurnActor, BattleSceneInstaller, BattleUnitMoveFlow, BattleUIFlowController.

전환 정리: BattleGameManager(Stage 안내 잠금 해제), BattleCameraRig(새 맵 경계), BattleRangeVisualizer(이전 맵 강조 해제), BattleHealingArea(기존 타일과 함께 제거).

씬 연결 대상은 Assets/renew/moon_branch Jeon Yong.unity입니다. NewMapGenerator 코드는 변경하지 않고 기존 StartGenerator를 호출합니다. Shop/Chest 생성 책임도 그대로입니다.

## 확인 결과

- 신규 파일까지 포함한 Assembly-CSharp 빌드: 오류 0, 경고 39.
- 실제 Stage/Bridge/Pool 소스를 Unity 스텁과 함께 실행: 풀 소진 시 증설 없음, 반환한 인스턴스 재사용, 반복 Stage 배치에도 총량 유지, ID 재정렬, 잘못된 후보 제외, 수량 제한 추첨, 안전 구역·중복 타일·3×3 소환 제외 조건 통과.
- 통합 씬: 컴포넌트 소유 관계, 직접 참조, 5/2/1 설정 확인.
- Unity Play Mode 실행·시각 검증은 하지 않았습니다. 페이드, 사망 중 Stage 전환, 재소환된 적의 HP바·Collider·애니메이션, Player 정보 유지 여부는 실제 플레이 확인 대상입니다.

검증 스크립트: work/run_stage_pool_checks.ps1, work/verify_stage_pool_scene.py. 빌드 기록: work/stage_final_build.log.
