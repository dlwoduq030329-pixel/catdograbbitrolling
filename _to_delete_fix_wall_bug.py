# -*- coding: utf-8 -*-

def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

def apply(path, replacements):
    content = load(path)
    for i, (old, new) in enumerate(replacements, start=1):
        count = content.count(old)
        assert count == 1, (path, i, count, old[:80])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

# ---------------------------------------------------------------------------
# 1) BattleTileRangeCalculator.cs
# ---------------------------------------------------------------------------
p1 = "Assets/renew/Battle/Player/BattleTileRangeCalculator.cs"

old_build_attackable = '''    /// <summary>이동 가능 지점들의 가장자리에서 공격 사거리 안에 포함되는 타일을 수집한다.</summary>
    public static void BuildAttackableTiles(
        MapInfo currentTile,
        int attackRange,
        IEnumerable<MapInfo> reachableTiles,
        ISet<MapInfo> attackableTiles)
    {
        HashSet<MapInfo> origins = new HashSet<MapInfo>(reachableTiles) { currentTile };
        foreach (MapInfo origin in origins)
        {
            Queue<MapInfo> queue = new Queue<MapInfo>();
            Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
            queue.Enqueue(origin);
            distances[origin] = 0;

            while (queue.Count > 0)
            {
                MapInfo current = queue.Dequeue();
                int distance = distances[current];
                if (distance > 0)
                {
                    attackableTiles.Add(current);
                }

                if (distance >= attackRange)
                {
                    continue;
                }

                foreach (MapInfo neighbour in GetNeighbours(current))
                {
                    if (neighbour == null || distances.ContainsKey(neighbour))
                    {
                        continue;
                    }

                    distances[neighbour] = distance + 1;
                    queue.Enqueue(neighbour);
                }
            }
        }

        attackableTiles.ExceptWith(reachableTiles);
        attackableTiles.Remove(currentTile);
    }'''

new_build_attackable = '''    /// <summary>이동 가능 지점들의 가장자리에서 공격 사거리 안에 포함되는 타일을 수집한다.
    /// isWalkable/occupiedTiles를 넘기면 벽이나 다른 유닛이 막고 있는 타일 너머로는 사거리가
    /// 확장되지 않는다(둘 다 생략하면 기존과 동일하게 장애물을 무시하고 순수 칸수로만 계산한다).</summary>
    public static void BuildAttackableTiles(
        MapInfo currentTile,
        int attackRange,
        IEnumerable<MapInfo> reachableTiles,
        ISet<MapInfo> attackableTiles,
        Func<MapInfo, bool> isWalkable = null,
        ISet<MapInfo> occupiedTiles = null)
    {
        HashSet<MapInfo> origins = new HashSet<MapInfo>(reachableTiles) { currentTile };
        foreach (MapInfo origin in origins)
        {
            Queue<MapInfo> queue = new Queue<MapInfo>();
            Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
            queue.Enqueue(origin);
            distances[origin] = 0;

            while (queue.Count > 0)
            {
                MapInfo current = queue.Dequeue();
                int distance = distances[current];
                if (distance > 0)
                {
                    attackableTiles.Add(current);
                }

                if (distance >= attackRange)
                {
                    continue;
                }

                foreach (MapInfo neighbour in GetNeighbours(current))
                {
                    if (neighbour == null || distances.ContainsKey(neighbour))
                    {
                        continue;
                    }

                    // 공격 사거리도 이동 범위와 마찬가지로 벽/점유 타일 너머로는 뻗어나가지 않는다.
                    if (isWalkable != null && !isWalkable(neighbour))
                    {
                        continue;
                    }
                    if (occupiedTiles != null && occupiedTiles.Contains(neighbour))
                    {
                        continue;
                    }

                    distances[neighbour] = distance + 1;
                    queue.Enqueue(neighbour);
                }
            }
        }

        attackableTiles.ExceptWith(reachableTiles);
        attackableTiles.Remove(currentTile);
    }'''

old_get_distance = '''    /// <summary>상하좌우 연결 기준 최단 칸 거리를 반환하며 제한 거리 안에 없으면 음수를 반환한다.</summary>
    public static int GetDistance(MapInfo startTile, MapInfo targetTile, int maxDistance)
    {
        if (startTile == null || targetTile == null)
        {
            return -1;
        }

        if (startTile == targetTile)
        {
            return 0;
        }

        Queue<MapInfo> queue = new Queue<MapInfo>();
        Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
        queue.Enqueue(startTile);
        distances[startTile] = 0;

        while (queue.Count > 0)
        {
            MapInfo current = queue.Dequeue();
            int distance = distances[current];
            if (distance >= maxDistance)
            {
                continue;
            }

            foreach (MapInfo neighbour in GetNeighbours(current))
            {
                if (neighbour == null || distances.ContainsKey(neighbour))
                {
                    continue;
                }

                int nextDistance = distance + 1;
                if (neighbour == targetTile)
                {
                    return nextDistance;
                }

                distances[neighbour] = nextDistance;
                queue.Enqueue(neighbour);
            }
        }

        return -1;
    }'''

new_get_distance = '''    /// <summary>상하좌우 연결 기준 최단 칸 거리를 반환하며 제한 거리 안에 없으면 음수를 반환한다.
    /// isWalkable/occupiedTiles를 넘기면 벽이나 다른 유닛이 가로막아 실제로 돌아갈 수 없는 경로는
    /// 최단 거리 계산에서 제외한다(둘 다 생략하면 기존과 동일하게 장애물을 무시한 순수 칸수 거리).
    /// 단, targetTile 자신은 점유 여부와 무관하게 항상 도달 지점으로 인정한다 — 공격 대상 Enemy가
    /// 서 있는 바로 그 타일이 "점유된 타일"이라는 이유로 거리 계산에서 제외되면 안 되기 때문이다.</summary>
    public static int GetDistance(
        MapInfo startTile,
        MapInfo targetTile,
        int maxDistance,
        Func<MapInfo, bool> isWalkable = null,
        ISet<MapInfo> occupiedTiles = null)
    {
        if (startTile == null || targetTile == null)
        {
            return -1;
        }

        if (startTile == targetTile)
        {
            return 0;
        }

        Queue<MapInfo> queue = new Queue<MapInfo>();
        Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
        queue.Enqueue(startTile);
        distances[startTile] = 0;

        while (queue.Count > 0)
        {
            MapInfo current = queue.Dequeue();
            int distance = distances[current];
            if (distance >= maxDistance)
            {
                continue;
            }

            foreach (MapInfo neighbour in GetNeighbours(current))
            {
                if (neighbour == null || distances.ContainsKey(neighbour))
                {
                    continue;
                }

                int nextDistance = distance + 1;
                if (neighbour == targetTile)
                {
                    return nextDistance;
                }

                if (isWalkable != null && !isWalkable(neighbour))
                {
                    continue;
                }
                if (occupiedTiles != null && occupiedTiles.Contains(neighbour))
                {
                    continue;
                }

                distances[neighbour] = nextDistance;
                queue.Enqueue(neighbour);
            }
        }

        return -1;
    }'''

apply(p1, [
    (old_build_attackable, new_build_attackable),
    (old_get_distance, new_get_distance),
])

# ---------------------------------------------------------------------------
# 2) BattleBasicAttackService.cs
# ---------------------------------------------------------------------------
p2 = "Assets/renew/Battle/Combat/BattleBasicAttackService.cs"

old_create_plan_call = '''            int attackDistance = BattleTileRangeCalculator.GetDistance(origin, enemyTile, attackRange);'''
new_create_plan_call = '''            int attackDistance = BattleTileRangeCalculator.GetDistance(
                origin, enemyTile, attackRange, isWalkable, occupiedTiles);'''

old_try_confirm_sig = '''    public static bool TryConfirm(
        BattleActionRequest pendingAction,
        GameObject player,
        EnemyTurnActor enemy,
        MapInfo playerTile,
        MapInfo enemyTile,
        IReadOnlyList<MapInfo> movementPath,
        int currentActionCost,
        out BattleActionResult result)
    {
        result = null;
        if (pendingAction == null || player == null || enemy == null ||
            !enemy.gameObject.activeInHierarchy || movementPath == null)
        {
            return false;
        }

        int attackDistance = BattleTileRangeCalculator.GetDistance(
            playerTile,
            enemyTile,
            pendingAction.RangeTiles);'''
new_try_confirm_sig = '''    public static bool TryConfirm(
        BattleActionRequest pendingAction,
        GameObject player,
        EnemyTurnActor enemy,
        MapInfo playerTile,
        MapInfo enemyTile,
        IReadOnlyList<MapInfo> movementPath,
        int currentActionCost,
        out BattleActionResult result,
        Func<MapInfo, bool> isWalkable = null,
        ISet<MapInfo> occupiedTiles = null)
    {
        result = null;
        if (pendingAction == null || player == null || enemy == null ||
            !enemy.gameObject.activeInHierarchy || movementPath == null)
        {
            return false;
        }

        int attackDistance = BattleTileRangeCalculator.GetDistance(
            playerTile,
            enemyTile,
            pendingAction.RangeTiles,
            isWalkable,
            occupiedTiles);'''

apply(p2, [
    (old_create_plan_call, new_create_plan_call),
    (old_try_confirm_sig, new_try_confirm_sig),
])

# ---------------------------------------------------------------------------
# 3) BattleBasicAttackController.cs
# ---------------------------------------------------------------------------
p3 = "Assets/renew/Battle/Combat/BattleBasicAttackController.cs"

old_fields = '''    private BattleActionRequest pendingAction;
    private EnemyTurnActor pendingEnemy;
    private List<MapInfo> pendingMovementPath = new List<MapInfo>();
    private MapInfo originalTile;
    private Vector3 originalPosition;'''
new_fields = '''    private BattleActionRequest pendingAction;
    private EnemyTurnActor pendingEnemy;
    private List<MapInfo> pendingMovementPath = new List<MapInfo>();
    // Begin() 시점의 Enemy 점유 타일 집합을 Confirm()까지 들고 있는다 — 확정 직전 재검증에서도
    // "점유 타일 너머로는 공격 사거리가 닿지 않는다"는 같은 규칙을 적용하기 위함(GetDistance 벽/점유 버그 수정분).
    private ISet<MapInfo> pendingOccupiedTiles;
    private MapInfo originalTile;
    private Vector3 originalPosition;'''

old_begin_call = '''        if (!BattleBasicAttackService.TryCreatePlan(
                playerTile,
                enemyTile,
                reachableTiles,
                occupiedTiles,
                isWalkable,
                attackRange,
                actionCost,
                playerMP,
                out List<MapInfo> movementPath,
                out int totalCost))
        {
            Debug.Log(totalCost > 0
                ? $"기본 공격에 필요한 MP가 부족합니다. 필요 MP: {totalCost}"
                : "현재 이동 및 공격 범위에서 해당 적을 공격할 수 없습니다.", this);
            return false;
        }

        pendingAction = new BattleActionRequest('''
new_begin_call = '''        if (!BattleBasicAttackService.TryCreatePlan(
                playerTile,
                enemyTile,
                reachableTiles,
                occupiedTiles,
                isWalkable,
                attackRange,
                actionCost,
                playerMP,
                out List<MapInfo> movementPath,
                out int totalCost))
        {
            Debug.Log(totalCost > 0
                ? $"기본 공격에 필요한 MP가 부족합니다. 필요 MP: {totalCost}"
                : "현재 이동 및 공격 범위에서 해당 적을 공격할 수 없습니다.", this);
            return false;
        }

        pendingOccupiedTiles = occupiedTiles;
        pendingAction = new BattleActionRequest('''

old_confirm_call = '''        if (!BattleBasicAttackService.TryConfirm(
                pendingAction,
                player,
                pendingEnemy,
                playerTile,
                enemyTile,
                pendingMovementPath,
                actionCost,
                out BattleActionResult result))'''
new_confirm_call = '''        if (!BattleBasicAttackService.TryConfirm(
                pendingAction,
                player,
                pendingEnemy,
                playerTile,
                enemyTile,
                pendingMovementPath,
                actionCost,
                out BattleActionResult result,
                isWalkable,
                pendingOccupiedTiles))'''

old_clear_pending = '''    private void ClearPending()
    {
        pendingAction = null;
        pendingEnemy = null;
        pendingMovementPath.Clear();
        originalTile = null;
        originalPosition = Vector3.zero;
    }'''
new_clear_pending = '''    private void ClearPending()
    {
        pendingAction = null;
        pendingEnemy = null;
        pendingMovementPath.Clear();
        pendingOccupiedTiles = null;
        originalTile = null;
        originalPosition = Vector3.zero;
    }'''

apply(p3, [
    (old_fields, new_fields),
    (old_begin_call, new_begin_call),
    (old_confirm_call, new_confirm_call),
    (old_clear_pending, new_clear_pending),
])

# ---------------------------------------------------------------------------
# 4) BattlePlayerRangeController.cs
# ---------------------------------------------------------------------------
p4 = "Assets/renew/Battle/Player/BattlePlayerRangeController.cs"

old_build1 = '''        BattleTileRangeCalculator.BuildAttackableTiles(
            currentTile,
            Mathf.Max(0, attackRange),
            reachableTiles,
            attackableTiles);'''
new_build1 = '''        BattleTileRangeCalculator.BuildAttackableTiles(
            currentTile,
            Mathf.Max(0, attackRange),
            reachableTiles,
            attackableTiles,
            isWalkable,
            occupiedEnemyTiles);'''

old_build2 = '''            HashSet<MapInfo> attackable = new HashSet<MapInfo>();
            BattleTileRangeCalculator.BuildAttackableTiles(
                enemyTile,
                actor.AttackRangeTiles,
                reachable,
                attackable);'''
new_build2 = '''            HashSet<MapInfo> attackable = new HashSet<MapInfo>();
            BattleTileRangeCalculator.BuildAttackableTiles(
                enemyTile,
                actor.AttackRangeTiles,
                reachable,
                attackable,
                isWalkable,
                occupiedForThisEnemy);'''

apply(p4, [
    (old_build1, new_build1),
    (old_build2, new_build2),
])

print("ALL DONE")
