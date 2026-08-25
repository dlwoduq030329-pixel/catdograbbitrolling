using UnityEngine;

/// <summary>
/// 허수아비 소환 카드의 배치 가능 여부를 미리 계산하고, 카드 데이터가 직접 참조한 Prefab을 Battle 소환물로 생성한다.
/// 계산 단계에서 소환 위치·Player 후퇴 위치·허수아비 최대 HP를 확정하므로 실행 단계는 계산 결과만 적용한다.
/// </summary>
public static class BattleScarecrowBridge
{
    public sealed class ScarecrowSpawnPlan
    {
        /// <summary>카드 효과 데이터가 직접 참조한 허수아비 Prefab.</summary>
        public GameObject ScarecrowPrefab;
        /// <summary>허수아비가 생성될 Player 인접 타일.</summary>
        public MapInfo SummonTile;
        /// <summary>소환 직후 Player가 반대 방향으로 물러날 타일. 이동할 수 없으면 null.</summary>
        public MapInfo RetreatTile;
        /// <summary>카드를 확정한 시점의 Player 최대 HP를 기준으로 계산한 허수아비 최대 HP.</summary>
        public float ScarecrowMaximumHealth;
    }

    /// <summary>
    /// 선택 타일이 Player와 인접한 빈 이동 타일인지 검사하고, 가능하면 소환 타일과 후퇴 타일을 계산한다.
    /// 이 단계에서는 Player나 Prefab을 실제로 이동·생성하지 않는다.
    /// </summary>
    public static bool TryCreateScarecrowSpawnPlan(
        GameObject player,
        MapInfo summonTile,
        GameObject scarecrowPrefab,
        out ScarecrowSpawnPlan plan,
        out string reason)
    {
        plan = null;
        reason = string.Empty;
        if (player == null || summonTile == null || !summonTile.IsWalkable)
        { reason = "허수아비를 놓을 수 있는 빈 타일을 선택해야 합니다."; return false; }
        if (scarecrowPrefab == null)
        { reason = "허수아비 카드 데이터에 소환 프리팹이 연결되지 않았습니다."; return false; }

        BattleHealth playerHealth = player.GetComponent<BattleHealth>();
        if (playerHealth == null)
        { reason = "플레이어의 최대 HP를 읽을 BattleHealth가 없습니다."; return false; }

        MapInfo playerTile = FindNearestMapTileInScene(player.transform.position);
        if (playerTile == null || BattleTileRangeCalculator.GetDistance(playerTile, summonTile, 1) != 1)
        { reason = "허수아비는 플레이어와 인접한 한 칸에 소환해야 합니다."; return false; }
        if (IsTileOccupiedByLivingUnit(summonTile, player))
        { reason = "선택한 타일이 이미 점유되어 있습니다."; return false; }

        Vector2Int direction = summonTile.Index - playerTile.Index;
        MapInfo retreat = GetNeighbourInDirection(playerTile, -direction);
        if (retreat != null && (!retreat.IsWalkable || IsTileOccupiedByLivingUnit(retreat, player))) retreat = null;
        plan = new ScarecrowSpawnPlan
        {
            ScarecrowPrefab = scarecrowPrefab,
            SummonTile = summonTile,
            RetreatTile = retreat,
            ScarecrowMaximumHealth = Mathf.Max(1f, playerHealth.MaxHealth / 3f)
        };
        return true;
    }

    /// <summary>검증된 계획의 타일에 허수아비를 생성하고 가능한 경우 Player를 반대편 타일로 이동한다.</summary>
    public static bool ApplyScarecrowSpawnPlan(GameObject player, ScarecrowSpawnPlan plan)
    {
        if (player == null || plan == null || plan.SummonTile == null || plan.ScarecrowPrefab == null) return false;

        GameObject summon = Object.Instantiate(
            plan.ScarecrowPrefab,
            plan.SummonTile.transform.position,
            Quaternion.identity);
        BattleScarecrowSummon runtime = summon.GetComponent<BattleScarecrowSummon>();
        if (runtime == null)
        {
            Debug.LogError("허수아비 Prefab에 BattleScarecrowSummon 컴포넌트가 없습니다.", summon);
            Object.Destroy(summon);
            return false;
        }
        runtime.Initialize(plan.SummonTile, plan.ScarecrowMaximumHealth);

        if (plan.RetreatTile != null)
        {
            BattleCardMovementService.ApplyMovement(
                player, new BattleCardMovementService.MovementPlan(plan.RetreatTile));
        }
        return true;
    }

    /// <summary>살아 있는 BattleHealth를 Scene에서 찾아 지정 타일을 이미 사용하는 Unit이 있는지 검사한다.</summary>
    private static bool IsTileOccupiedByLivingUnit(MapInfo tile, GameObject ignoredUnit)
    {
        foreach (BattleHealth health in Object.FindObjectsByType<BattleHealth>(FindObjectsSortMode.None))
        {
            if (health == null || health.gameObject == ignoredUnit || health.IsDead) continue;
            if (FindNearestMapTileInScene(health.transform.position) == tile) return true;
        }
        return false;
    }

    /// <summary>Scene 전체 MapInfo 중 월드 위치와 가장 가까운 타일을 찾는 임시 fallback이다.</summary>
    private static MapInfo FindNearestMapTileInScene(Vector3 position)
    {
        MapInfo best = null; float distance = float.MaxValue;
        foreach (MapInfo tile in Object.FindObjectsByType<MapInfo>(FindObjectsSortMode.None))
        {
            float next = (tile.transform.position - position).sqrMagnitude;
            if (next < distance) { best = tile; distance = next; }
        }
        return best;
    }

    /// <summary>정규화된 상하좌우 인덱스 방향에 해당하는 MapInfo 연결을 반환한다.</summary>
    private static MapInfo GetNeighbourInDirection(MapInfo tile, Vector2Int direction)
    {
        if (direction == Vector2Int.up) return tile.Up;
        if (direction == Vector2Int.down) return tile.Down;
        if (direction == Vector2Int.left) return tile.Left;
        if (direction == Vector2Int.right) return tile.Right;
        return null;
    }
}
