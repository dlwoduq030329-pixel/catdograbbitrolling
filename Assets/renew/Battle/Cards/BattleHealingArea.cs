using System;
using UnityEngine;

/// <summary>
/// 선택한 타일에 남아 플레이어 턴 시작마다 범위 안의 플레이어를 회복하는 Battle 전용 지속 영역이다.
/// </summary>
public sealed class BattleHealingArea : MonoBehaviour
{
    private GameObject player;
    private MapInfo centerTile;
    private Func<Vector3, MapInfo> findClosestTile;
    private int radiusTiles;
    private float healingAmount;
    private int remainingTurns;
    private BattleGameManager manager;

    public static void Create(
        GameObject player,
        MapInfo centerTile,
        Func<Vector3, MapInfo> findClosestTile,
        int radiusTiles,
        float healingAmount,
        int durationTurns,
        BattleRangeVisualizer rangeVisualizer,
        Color areaColor)
    {
        if (player == null || centerTile == null || findClosestTile == null) return;
        GameObject root = new GameObject("Healing Area");
        root.transform.position = centerTile.transform.position;
        BattleHealingArea area = root.AddComponent<BattleHealingArea>();
        area.player = player;
        area.centerTile = centerTile;
        area.findClosestTile = findClosestTile;
        area.radiusTiles = Mathf.Max(0, radiusTiles);
        area.healingAmount = Mathf.Max(0f, healingAmount);
        area.remainingTurns = Mathf.Max(1, durationTurns);
        area.BindTurnManager();
        area.CreatePersistentVisual();
        area.CreateRangeOutlines(areaColor);
    }

    private void BindTurnManager()
    {
        manager = BattleGameManager.Instance;
        if (manager != null)
        {
            manager.PlayerTurnStarted -= HandlePlayerTurnStarted;
            manager.PlayerTurnStarted += HandlePlayerTurnStarted;
        }
    }

    private void HandlePlayerTurnStarted()
    {
        if (player != null && player.activeInHierarchy)
        {
            MapInfo playerTile = findClosestTile(player.transform.position);
            if (IsInsideSquare(playerTile))
                player.GetComponent<BattleHealth>()?.Heal(healingAmount);
        }

        remainingTurns--;
        if (remainingTurns <= 0) Destroy(gameObject);
    }

    private void CreatePersistentVisual()
    {
        BattleCardVfxRegistry registry = BattleCardVfxRegistry.Load();
        GameObject prefab = registry != null ? registry.Find("CONSECRATION") : null;
        if (prefab == null) return;

        GameObject visual = Instantiate(prefab, transform.position, prefab.transform.rotation, transform);
        foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (ParticleSystem particles in visual.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            particles.Play(true);
        }
    }

    private void CreateRangeOutlines(Color color)
    {
        foreach (MapInfo tile in FindObjectsByType<MapInfo>(FindObjectsSortMode.None))
        {
            if (IsInsideSquare(tile)) CreateTileOutline(tile, color);
        }
    }

    /// <summary>성역화는 중심 기준 X/Z 인덱스가 각각 1 이내인 정확한 3×3 정사각형을 사용한다.</summary>
    private bool IsInsideSquare(MapInfo tile)
    {
        if (tile == null || centerTile == null) return false;
        Vector2Int offset = tile.Index - centerTile.Index;
        return Mathf.Abs(offset.x) <= radiusTiles && Mathf.Abs(offset.y) <= radiusTiles;
    }

    private void CreateTileOutline(MapInfo tile, Color color)
    {
        Renderer tileRenderer = tile != null ? tile.GetComponentInChildren<Renderer>() : null;
        if (tileRenderer == null) return;
        Bounds bounds = tileRenderer.bounds;
        GameObject outline = new GameObject($"Sanctuary Range {tile.Index}");
        outline.transform.SetParent(transform, true);
        LineRenderer line = outline.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.widthMultiplier = 0.055f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;
        float y = bounds.max.y + 0.04f;
        line.SetPosition(0, new Vector3(bounds.min.x, y, bounds.min.z));
        line.SetPosition(1, new Vector3(bounds.max.x, y, bounds.min.z));
        line.SetPosition(2, new Vector3(bounds.max.x, y, bounds.max.z));
        line.SetPosition(3, new Vector3(bounds.min.x, y, bounds.max.z));
    }

    private void OnDestroy()
    {
        if (manager != null) manager.PlayerTurnStarted -= HandlePlayerTurnStarted;
    }
}
