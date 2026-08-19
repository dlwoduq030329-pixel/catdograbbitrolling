using System.Collections.Generic;
using UnityEngine;

/// <summary>이동 가능 타일에 마우스를 올리면 그 위치를 공격할 수 있는 Enemy와 연결선을 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleMoveThreatPreview : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Color lineColor = new Color(1f, 0.15f, 0.08f, 0.95f);
    [SerializeField, Min(0.01f)] private float startWidth = 0.11f;
    [SerializeField, Min(0.01f)] private float endWidth = 0.035f;
    [SerializeField] private float lineHeight = 0.65f;
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private BattleRaycaster raycaster;
    private BattlePlayerRangeController rangeController;
    private MapInfo hoveredTile;
    private Material lineMaterial;

    public void Configure(
        Camera camera,
        BattleRaycaster targetRaycaster,
        BattlePlayerRangeController targetRangeController)
    {
        targetCamera = camera;
        raycaster = targetRaycaster;
        rangeController = targetRangeController;
    }

    private void Update()
    {
        if (!BattlePlayerActionController.IsMoveRangeVisible || raycaster == null || rangeController == null ||
            BattlePlayerInputReader.IsPointerOverInteractiveUI(Input.mousePosition) ||
            !raycaster.TryGetMapTile(Input.mousePosition, out MapInfo tile) ||
            !rangeController.IsReachable(tile))
        {
            hoveredTile = null;
            HideLines();
            return;
        }

        if (hoveredTile == tile) return;
        hoveredTile = tile;
        DrawThreats(tile);
    }

    private void DrawThreats(MapInfo destination)
    {
        List<GameObject> threats = FindThreateningEnemies(destination);
        EnsureLineCount(threats.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            bool visible = i < threats.Count;
            lines[i].enabled = visible;
            if (!visible) continue;
            Vector3 from = threats[i].transform.position;
            Vector3 to = destination.transform.position;
            from.y += lineHeight;
            to.y += lineHeight;
            lines[i].SetPosition(0, from);
            lines[i].SetPosition(1, to);
        }
    }

    private static List<GameObject> FindThreateningEnemies(MapInfo destination)
    {
        List<GameObject> result = new List<GameObject>();
        EnemyTurnActor[] enemies = FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            BattleEnemyRuntimeData runtime = enemy.GetComponent<BattleEnemyRuntimeData>();
            if (runtime == null || runtime.Data == null) continue;
            MapInfo origin = FindClosestTile(enemy.transform.position);
            if (origin == null) continue;

            int remainingMP = Mathf.Max(0, runtime.Data.maxTurnMP - runtime.Data.basicAttackMPCost);
            int moveRange = remainingMP / Mathf.Max(1, runtime.Data.moveMPCostPerTile);
            HashSet<MapInfo> origins = new HashSet<MapInfo> { origin };
            Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
            BattleTileRangeCalculator.BuildReachableTiles(
                origin,
                moveRange,
                BattleMapTraversalService.IsWalkable,
                new HashSet<MapInfo>(),
                origins,
                distances);

            foreach (MapInfo attackOrigin in origins)
            {
                if (BattleTileRangeCalculator.GetDistance(
                        attackOrigin,
                        destination,
                        enemy.AttackRangeTiles) >= 0)
                {
                    result.Add(enemy.gameObject);
                    break;
                }
            }
        }
        return result;
    }

    private static MapInfo FindClosestTile(Vector3 position)
    {
        BattleMapRegistry registry = FindFirstObjectByType<BattleMapRegistry>(FindObjectsInactive.Include);
        if (registry != null && registry.Tiles.Count > 0) return registry.FindClosestTile(position);
        return BattleTileLocator.FindClosestXZ(position, FindObjectsByType<MapInfo>(FindObjectsSortMode.None));
    }

    private void EnsureLineCount(int count)
    {
        if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
        while (lines.Count < count)
        {
            GameObject child = new GameObject("Enemy Threat Line");
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.startColor = line.endColor = lineColor;
            line.material = lineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lines.Add(line);
        }
    }

    private void HideLines()
    {
        foreach (LineRenderer line in lines) if (line != null) line.enabled = false;
    }

    private void OnDisable()
    {
        hoveredTile = null;
        HideLines();
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
