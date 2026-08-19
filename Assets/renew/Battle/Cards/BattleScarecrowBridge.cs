using UnityEngine;

/// <summary>허수아비 소환 카드와 레거시 Ch_Scareclow 프리팹을 잇는 Battle 전용 브릿지.</summary>
public static class BattleScarecrowBridge
{
    private const float ScarecrowHealth = 10f;

    public sealed class Plan
    {
        public MapInfo SummonTile;
        public MapInfo RetreatTile;
    }

    public static bool TryCreatePlan(GameObject player, MapInfo summonTile, out Plan plan, out string reason)
    {
        plan = null;
        reason = string.Empty;
        if (player == null || summonTile == null || !summonTile.IsWalkable)
        { reason = "허수아비를 놓을 수 있는 빈 타일을 선택해야 합니다."; return false; }

        MapInfo playerTile = FindClosestTile(player.transform.position);
        if (playerTile == null || BattleTileRangeCalculator.GetDistance(playerTile, summonTile, 1) != 1)
        { reason = "허수아비는 플레이어와 인접한 한 칸에 소환해야 합니다."; return false; }
        if (IsOccupied(summonTile, player))
        { reason = "선택한 타일이 이미 점유되어 있습니다."; return false; }

        Vector2Int direction = summonTile.Index - playerTile.Index;
        MapInfo retreat = GetNeighbour(playerTile, -direction);
        if (retreat != null && (!retreat.IsWalkable || IsOccupied(retreat, player))) retreat = null;
        plan = new Plan { SummonTile = summonTile, RetreatTile = retreat };
        return true;
    }

    public static bool Execute(GameObject player, Plan plan)
    {
        if (player == null || plan == null || plan.SummonTile == null) return false;
        BattleScarecrowPrefabReference reference =
            Resources.Load<BattleScarecrowPrefabReference>("Battle/Scarecrow/BattleScarecrowPrefabReference");
        GameObject prefab = reference != null ? reference.Prefab : null;
        if (prefab == null)
        {
            Debug.LogError("Battle 허수아비 브릿지 프리팹을 찾지 못했습니다.");
            return false;
        }

        GameObject summon = Object.Instantiate(prefab, plan.SummonTile.transform.position, Quaternion.identity);
        BattleScarecrowSummon runtime = summon.AddComponent<BattleScarecrowSummon>();
        runtime.Initialize(plan.SummonTile, ScarecrowHealth);

        if (plan.RetreatTile != null)
        {
            BattleCardMovementService.ApplyMovement(
                player, new BattleCardMovementService.MovementPlan(plan.RetreatTile));
        }
        return true;
    }

    private static bool IsOccupied(MapInfo tile, GameObject ignored)
    {
        foreach (BattleHealth health in Object.FindObjectsByType<BattleHealth>(FindObjectsSortMode.None))
        {
            if (health == null || health.gameObject == ignored || health.IsDead) continue;
            if (FindClosestTile(health.transform.position) == tile) return true;
        }
        return false;
    }

    private static MapInfo FindClosestTile(Vector3 position)
    {
        MapInfo best = null; float distance = float.MaxValue;
        foreach (MapInfo tile in Object.FindObjectsByType<MapInfo>(FindObjectsSortMode.None))
        {
            float next = (tile.transform.position - position).sqrMagnitude;
            if (next < distance) { best = tile; distance = next; }
        }
        return best;
    }

    private static MapInfo GetNeighbour(MapInfo tile, Vector2Int direction)
    {
        if (direction == Vector2Int.up) return tile.Up;
        if (direction == Vector2Int.down) return tile.Down;
        if (direction == Vector2Int.left) return tile.Left;
        if (direction == Vector2Int.right) return tile.Right;
        return null;
    }
}
