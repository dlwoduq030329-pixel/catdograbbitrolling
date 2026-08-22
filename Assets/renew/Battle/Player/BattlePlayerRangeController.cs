using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 이동·공격 범위 집합을 생성하고 우선순위에 따라 타일 색상을 표시한다.
/// 입력 처리와 행동 확정은 담당하지 않는다.
///
/// 두 가지 독립된 표시 모드를 갖는다: (1) `BuildAndShow` — Player 자신의 이동/공격 범위를
/// 우선순위 색상(점유 차단 > 이동가능 > 공격가능 > Enemy 탐지권)으로 표시, (2) `BuildAndShowEnemyThreatRange`
/// — R 단축키 전용으로 활성 Enemy 전체가 이번 턴에 위협 가능한 타일을 한 가지 색으로 따로 표시.
/// 두 결과는 서로 다른 필드(reachableTiles/attackableTiles vs enemyThreatTiles)에 저장되고 섞이지 않는다.
/// "Enemy가 어느 타일에 서 있는가"는 두 모드 모두 occupiedEnemyTiles(RefreshOccupiedEnemyTiles가 채움) 하나만
/// 참조하도록 통일했다 — 예전에는 BuildAndShowEnemyThreatRange가 findClosestTile로 매번 따로 계산해서
/// 두 표시가 서로 다른 소스를 참조했었다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerRangeController : MonoBehaviour
{
    // BuildAndShow()가 계산하는 "Player 자신의" 이동/공격 범위 결과.
    private readonly HashSet<MapInfo> reachableTiles = new HashSet<MapInfo>();
    private readonly HashSet<MapInfo> attackableTiles = new HashSet<MapInfo>();
    private readonly Dictionary<MapInfo, int> reachableDistances = new Dictionary<MapInfo, int>();

    // RefreshOccupiedEnemyTiles()가 채우는, 현재 Enemy들이 서 있는 타일 집합(이동 계산 시 통행 차단용).
    private readonly HashSet<MapInfo> occupiedEnemyTiles = new HashSet<MapInfo>();

    // BuildAndShowEnemyThreatRange()가 계산하는, 모든 활성 Enemy의 위협 범위 합집합(R 단축키 표시 전용).
    private readonly HashSet<MapInfo> enemyThreatTiles = new HashSet<MapInfo>();

    private BattleRangeVisualizer rangeVisualizer;

    public IEnumerable<MapInfo> ReachableTiles => reachableTiles;
    public ISet<MapInfo> OccupiedEnemyTiles => occupiedEnemyTiles;
    /// <summary>현재 표시 중인 이동 범위에 지정 타일이 포함되는지 확인한다.</summary>
    public bool IsReachable(MapInfo tile) => tile != null && reachableTiles.Contains(tile);

    /// <summary>계산 결과를 화면에 칠할 전용 시각화 모듈을 연결한다.</summary>
    public void AttachVisualizer(BattleRangeVisualizer visualizer)
    {
        rangeVisualizer = visualizer;
    }

    /// <summary>
    /// 이동·공격 범위 색칠을 담당하는 메인 메서드. 호출부(BattlePlayerActionController.ShowMoveRange)에서
    /// moveRange로 "주사위 이동력과 현재 MP 중 작은 값"을 넘겨주므로, 결과적으로 화면에 보이는 이동 범위는
    /// 항상 현재 MP로 실제 갈 수 있는 만큼만 표시된다(MP가 부족하면 주사위 값보다 좁게 표시).
    /// 호출 전에 RefreshOccupiedEnemyTiles()로 occupiedEnemyTiles를 먼저 채워둬야 한다(이 메서드는 채우지 않음).
    ///
    /// 한 타일에 표시되는 색은 아래 우선순위로 정확히 하나만 선택된다(위에서부터 먼저 만족하는 조건 사용):
    /// 1. blockedColor      — Enemy가 점유해서 이동 불가능한 타일(occupiedEnemyTiles)
    /// 2. movableColor      — 이번 턴에 이동 가능한 타일(reachableTiles, BFS 결과)
    /// 3. attackableColor   — 이동은 안 되지만 기본 공격은 닿는 타일(attackableTiles)
    /// 4. enemyDetectColor  — 위 셋 다 아니지만 Enemy의 탐지 범위(EnemyDetector.DetectRange) 안에 들어오는 타일(경고용)
    /// 위 네 조건에 전부 해당 없으면 색을 칠하지 않는다(원래 타일 색 유지).
    /// </summary>
    public bool BuildAndShow(
        IEnumerable<MapInfo> mapTiles,
        MapInfo currentTile,
        int moveRange,
        int attackRange,
        Func<MapInfo, bool> isWalkable,
        IEnumerable<GameObject> enemyObjects,
        Color movableColor,
        Color blockedColor,
        Color attackableColor,
        Color enemyDetectColor)
    {
        ClearCalculatedRanges();
        if (currentTile == null || rangeVisualizer == null || isWalkable == null)
        {
            return false;
        }

        BattleTileRangeCalculator.BuildReachableTiles(
            currentTile,
            Mathf.Max(0, moveRange),
            isWalkable,
            occupiedEnemyTiles,
            reachableTiles,
            reachableDistances);
        BattleTileRangeCalculator.BuildAttackableTiles(
            currentTile,
            Mathf.Max(0, attackRange),
            reachableTiles,
            attackableTiles,
            isWalkable,
            occupiedEnemyTiles);

        List<EnemyDetector> detectors = CollectEnemyDetectors(enemyObjects);
        foreach (MapInfo tile in mapTiles)
        {
            if (tile == null)
            {
                continue;
            }

            // 원래 통행할 수 없는 지형에는 이동 Ray를 표시하지 않는다.
            if (!isWalkable(tile))
            {
                continue;
            }

            // Enemy가 점유한 통행 가능 타일만 차단 색으로 표시한다.
            if (occupiedEnemyTiles.Contains(tile))
            {
                rangeVisualizer.ShowRangeTile(tile, blockedColor);
            }
            else if (reachableTiles.Contains(tile))
            {
                rangeVisualizer.ShowRangeTile(tile, movableColor);
            }
            else if (attackableTiles.Contains(tile))
            {
                rangeVisualizer.ShowRangeTile(tile, attackableColor);
            }
            else if (IsInEnemyDetectRange(tile.transform.position, detectors))
            {
                rangeVisualizer.ShowRangeTile(tile, enemyDetectColor);
            }
        }

        return true;
    }

    /// <summary>
    /// R 단축키 전용 표시. Player 자신의 이동·공격 범위 대신, 활성 Enemy 각각이 "자기 턴이 온다면"
    /// 이번 상태 그대로 이동+기본공격으로 위협할 수 있는 타일 전체를 Enemy별로 BFS 계산해서 합집합으로 한 번에 표시한다.
    ///
    /// 처리 순서:
    /// 1. 활성(activeInHierarchy) Enemy만 골라낸다.
    /// 2. 모든 활성 Enemy가 서 있는 타일을 allEnemyTiles에 모은다 — 단, 이미 reachableTiles/attackableTiles(Player
    ///    자신의 범위)에 포함된 타일은 애초에 제외한다("R 위협 범위는 최하단 정보라 이동·공격 가능 타일 색을 덮지 않는다").
    /// 3. Enemy 한 명씩 순서대로: 그 Enemy 자신의 이동력(maxTurnMP / moveMPCostPerTile)만큼 BFS로 도달 가능한
    ///    타일을 계산한다. 이때 "다른 Enemy가 서 있는 타일"은 막되(allEnemyTiles에서 자기 타일만 제외한 집합을
    ///    점유 정보로 사용), 계산 대상 Enemy 자신이 서 있는 타일은 막지 않는다(자기 자신이 자기 위치를 막으면 안 되므로).
    /// 4. 그 도달 범위에서 다시 기본 공격 사거리(actor.AttackRangeTiles)만큼 공격 가능 타일을 뽑는다.
    /// 5. Enemy 본인 타일 + 도달 타일 + 공격 타일을 전부 enemyThreatTiles에 합쳐서, 마지막에 한 번에 threatColor로 칠한다.
    ///
    /// (2026-08-22 통일 완료) "Enemy가 서 있는 타일"은 occupiedEnemyTiles(RefreshOccupiedEnemyTiles가 채운,
    /// BuildAndShow와 동일한 Registry 우선 소스) 하나만 참조한다. 호출부(ActionController.ShowEnemyThreatRange)가
    /// 이 메서드를 부르기 전에 RefreshOccupiedEnemyTiles()를 먼저 호출해서 채워둬야 한다(BuildAndShow와 동일한 계약).
    /// </summary>
    public bool BuildAndShowEnemyThreatRange(
        Func<MapInfo, bool> isWalkable,
        IEnumerable<GameObject> enemyObjects,
        Func<Vector3, MapInfo> findClosestTile,
        Color threatColor)
    {
        enemyThreatTiles.Clear();
        if (rangeVisualizer == null || isWalkable == null || findClosestTile == null || enemyObjects == null)
        {
            return false;
        }

        List<GameObject> activeEnemies = new List<GameObject>();
        foreach (GameObject enemyObject in enemyObjects)
        {
            if (enemyObject != null && enemyObject.activeInHierarchy)
            {
                activeEnemies.Add(enemyObject);
            }
        }

        if (activeEnemies.Count == 0)
        {
            return false;
        }

        // 다른 Enemy가 서 있는 타일은 이동 계산에서 막되, 계산 대상 본인의 타일은 막지 않는다.
        // occupiedEnemyTiles(RefreshOccupiedEnemyTiles가 채운, BuildAndShow와 동일한 소스)를 그대로 써서
        // "Enemy가 어느 타일에 서 있는가"를 두 표시 모드가 서로 다르게 계산하지 않도록 했다
        // (호출부인 BattlePlayerActionController.ShowEnemyThreatRange가 이 메서드를 부르기 전에
        // RefreshOccupiedEnemyTiles를 먼저 호출해서 채워둔다).
        HashSet<MapInfo> allEnemyTiles = new HashSet<MapInfo>();
        foreach (MapInfo tile in occupiedEnemyTiles)
        {
            // R 위협 범위는 최하단 정보다. 이동·공격 가능 타일의 색을 덮지 않는다.
            if (tile != null && !reachableTiles.Contains(tile) && !attackableTiles.Contains(tile))
            {
                allEnemyTiles.Add(tile);
            }
        }

        foreach (GameObject enemyObject in activeEnemies)
        {
            EnemyTurnActor actor = enemyObject.GetComponent<EnemyTurnActor>();
            BattleEnemyRuntimeData runtimeData = enemyObject.GetComponent<BattleEnemyRuntimeData>();
            if (actor == null || runtimeData == null || runtimeData.Data == null)
            {
                continue;
            }

            MapInfo enemyTile = findClosestTile(enemyObject.transform.position);
            if (enemyTile == null)
            {
                continue;
            }

            int moveCost = Mathf.Max(1, runtimeData.Data.moveMPCostPerTile);
            int moveRange = runtimeData.Data.maxTurnMP / moveCost;

            HashSet<MapInfo> occupiedForThisEnemy = new HashSet<MapInfo>(allEnemyTiles);
            occupiedForThisEnemy.Remove(enemyTile);

            HashSet<MapInfo> reachable = new HashSet<MapInfo>();
            Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
            BattleTileRangeCalculator.BuildReachableTiles(
                enemyTile,
                moveRange,
                isWalkable,
                occupiedForThisEnemy,
                reachable,
                distances);

            HashSet<MapInfo> attackable = new HashSet<MapInfo>();
            BattleTileRangeCalculator.BuildAttackableTiles(
                enemyTile,
                actor.AttackRangeTiles,
                reachable,
                attackable,
                isWalkable,
                occupiedForThisEnemy);

            enemyThreatTiles.Add(enemyTile);
            enemyThreatTiles.UnionWith(reachable);
            enemyThreatTiles.UnionWith(attackable);
        }

        foreach (MapInfo tile in enemyThreatTiles)
        {
            if (tile != null)
            {
                rangeVisualizer.ShowRangeTile(tile, threatColor);
            }
        }

        return enemyThreatTiles.Count > 0;
    }

    /// <summary>
    /// R 단축키 토글 등으로 이전 표시를 지울 때 쓰는 초기화. 실제 타일 색 복구(RestoreAllTileColors)는
    /// 호출부(BattlePlayerActionController)가 별도로 하고, 여기서는 이 클래스가 들고 있는 3가지 계산
    /// 결과(이동/공격 범위, Enemy 점유 정보, R 위협 범위)를 전부 비우기만 한다.
    /// </summary>
    public void ClearState()
    {
        ClearCalculatedRanges();
        occupiedEnemyTiles.Clear();
        enemyThreatTiles.Clear();
    }

    /// <summary>새 범위 계산 전에 도달·공격 결과만 비우고 직전에 갱신한 점유 정보는 유지한다.</summary>
    private void ClearCalculatedRanges()
    {
        reachableTiles.Clear();
        attackableTiles.Clear();
        reachableDistances.Clear();
    }

    /// <summary>
    /// occupiedEnemyTiles(Player 자신의 이동 범위 계산에서 "막힌 타일"로 취급할 Enemy 점유 타일 집합)를
    /// 다시 채운다. BattleDataPool.Map(Registry)에 Enemy가 등록돼 있으면 Registry의 FillOccupiedTiles를
    /// 그대로 쓰고, 없을 때만(구형/Registry 미등록 Scene 대비) 씬을 훑어 활성 EnemyTurnActor 전체의
    /// 최근접 타일을 직접 계산해서 채운다. BuildAndShow() 호출 전에 반드시 먼저 호출해야 한다(그렇지 않으면
    /// occupiedEnemyTiles가 비어 있어 Enemy 점유 차단 표시가 전부 빠진다).
    /// </summary>
    public void RefreshOccupiedEnemyTiles(
        BattleDataPool battleDataPool,
        Func<Vector3, MapInfo> findClosestTile)
    {
        occupiedEnemyTiles.Clear();

        if (battleDataPool != null &&
            battleDataPool.Units != null &&
            battleDataPool.Map != null &&
            battleDataPool.Units.Enemies.Count > 0)
        {
            battleDataPool.Map.FillOccupiedTiles(
                battleDataPool.Units.Enemies,
                occupiedEnemyTiles);
            return;
        }

        if (findClosestTile == null)
        {
            return;
        }

        EnemyTurnActor[] enemies = FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            MapInfo tile = findClosestTile(enemy.transform.position);
            if (tile != null)
            {
                occupiedEnemyTiles.Add(tile);
            }
        }
    }

    /// <summary>
    /// enemyDetectColor(4번째 우선순위 경고색) 판정에 쓸 EnemyDetector 컴포넌트 목록을 모은다.
    /// enemyObjects가 주어지면 그 활성 Enemy들의 자식에서만 찾고(정상 경로), enemyObjects가 null이면
    /// 씬 전체에서 EnemyDetector를 전부 찾는다(호출부가 항상 enemyObjects를 넘겨주므로 사실상 안 쓰이는 fallback).
    /// </summary>
    private static List<EnemyDetector> CollectEnemyDetectors(IEnumerable<GameObject> enemyObjects)
    {
        List<EnemyDetector> detectors = new List<EnemyDetector>();
        if (enemyObjects != null)
        {
            foreach (GameObject enemyObject in enemyObjects)
            {
                if (enemyObject == null || !enemyObject.activeInHierarchy)
                {
                    continue;
                }

                EnemyDetector detector = enemyObject.GetComponentInChildren<EnemyDetector>();
                if (detector != null)
                {
                    detectors.Add(detector);
                }
            }

            return detectors;
        }

        detectors.AddRange(FindObjectsByType<EnemyDetector>(FindObjectsSortMode.None));
        return detectors;
    }

    /// <summary>
    /// tilePosition이 detectors 중 하나라도의 탐지 범위(EnemyDetector.DetectRange, Enemy 종류별 Inspector 값) 안에
    /// 들어오면 true. BuildAndShow()에서 "이동도 공격도 안 되지만 이 타일로 가면 Enemy에게 들킬 수 있다"는
    /// 경고 표시(enemyDetectColor)에만 쓰인다 — 실제 이동/공격 가능 여부와는 무관한 순수 시각적 경고.
    /// </summary>
    private static bool IsInEnemyDetectRange(Vector3 tilePosition, IEnumerable<EnemyDetector> detectors)
    {
        foreach (EnemyDetector detector in detectors)
        {
            if (detector != null &&
                Vector3.Distance(detector.transform.position, tilePosition) <= detector.DetectRange)
            {
                return true;
            }
        }

        return false;
    }
}
