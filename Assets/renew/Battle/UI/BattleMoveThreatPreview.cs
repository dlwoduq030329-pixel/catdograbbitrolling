using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이동 가능 타일에 마우스를 올리면 그 위치에 반응할 Enemy를 미리 계산한다.
/// 현재 위치에서 바로 공격 가능한 Enemy는 검 아이콘, 이동 후 추격 가능한 Enemy는 눈 아이콘과
/// 구분된 연결선으로 표시해 이동 확정 전에 위험을 판단할 수 있게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleMoveThreatPreview : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Color lineColor = new Color(1f, 0.15f, 0.08f, 0.95f);
    [SerializeField] private Color chaseLineColor = new Color(1f, 0.55f, 0.08f, 0.9f);
    [SerializeField, Min(0.01f)] private float startWidth = 0.11f;
    [SerializeField, Min(0.01f)] private float endWidth = 0.035f;
    [SerializeField] private float lineHeight = 0.65f;
    [Header("Enemy Intent Icons")]
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite chaseIcon;
    [SerializeField, Min(0.1f)] private float iconWorldSize = 0.8f;
    [Tooltip("HP UI를 찾지 못했을 때 사용할 Enemy 기준 기본 높이입니다.")]
    [SerializeField] private float fallbackIconHeight = 2.8f;
    [Tooltip("Enemy HP UI의 화면상 위쪽 끝과 의도 아이콘 사이의 간격입니다.")]
    [SerializeField, Min(0f)] private float iconGapAboveHealthBar = 0.06f;
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();
    private readonly List<Transform> iconTargets = new List<Transform>();
    private BattleRaycaster raycaster;
    private BattlePlayerRangeController rangeController;
    private MapInfo hoveredTile;
    private MapInfo selectedDestination;
    private Material lineMaterial;

    private enum ThreatIntent { Attack, Chase }

    private readonly struct ThreatPreview
    {
        public readonly GameObject Enemy;
        public readonly ThreatIntent Intent;

        public ThreatPreview(GameObject enemy, ThreatIntent intent)
        {
            Enemy = enemy;
            Intent = intent;
        }
    }

    private void Awake()
    {
        if (attackIcon == null)
            attackIcon = Resources.Load<Sprite>("Battle/UI/ThreatIcons/EnemyIntent_Attack");
        if (chaseIcon == null)
            chaseIcon = Resources.Load<Sprite>("Battle/UI/ThreatIcons/EnemyIntent_Chase");
    }

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
        if (selectedDestination != null)
        {
            bool blockedByModal = BattleGameManager.Instance != null &&
                                  BattleGameManager.Instance.IsModalInteractionOpen;
            if (blockedByModal || !BattlePlayerActionController.IsMoveRangeVisible)
            {
                HideLines();
                return;
            }

            if (hoveredTile != selectedDestination)
            {
                hoveredTile = selectedDestination;
                DrawThreats(selectedDestination);
            }
            UpdateIconTransforms();
            return;
        }

        if (!BattlePlayerActionController.IsMoveRangeVisible || raycaster == null || rangeController == null ||
            BattlePlayerInputReader.IsPointerOverInteractiveUI(Input.mousePosition) ||
            !raycaster.TryGetMapTile(Input.mousePosition, out MapInfo tile) ||
            !rangeController.IsReachable(tile))
        {
            hoveredTile = null;
            HideLines();
            return;
        }

        if (hoveredTile != tile)
        {
            hoveredTile = tile;
            DrawThreats(tile);
        }
        UpdateIconTransforms();
    }

    /// <summary>선택한 이동 목적지의 Enemy 의도를 Player 클릭으로 확정할 때까지 고정한다.</summary>
    public void ShowSelectedDestination(MapInfo destination)
    {
        selectedDestination = destination;
        hoveredTile = null;
        if (selectedDestination != null) DrawThreats(selectedDestination);
    }

    /// <summary>이동 취소·완료 시 고정된 의도 표시를 제거하고 다시 호버 미리보기 상태로 돌린다.</summary>
    public void ClearSelectedDestination()
    {
        selectedDestination = null;
        hoveredTile = null;
        HideLines();
    }

    private void DrawThreats(MapInfo destination)
    {
        List<ThreatPreview> threats = FindThreateningEnemies(destination);
        EnsureLineCount(threats.Count);
        EnsureIconCount(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            bool visible = i < threats.Count;
            lines[i].enabled = visible;
            icons[i].enabled = visible;
            iconTargets[i] = visible ? threats[i].Enemy.transform : null;
            if (!visible) continue;
            ThreatPreview threat = threats[i];
            Vector3 from = threat.Enemy.transform.position;
            Vector3 to = destination.transform.position;
            from.y += lineHeight;
            to.y += lineHeight;
            lines[i].SetPosition(0, from);
            lines[i].SetPosition(1, to);
            Color intentColor = threat.Intent == ThreatIntent.Attack ? lineColor : chaseLineColor;
            lines[i].startColor = lines[i].endColor = intentColor;
            icons[i].sprite = threat.Intent == ThreatIntent.Attack ? attackIcon : chaseIcon;
            icons[i].transform.position = ResolveIconPosition(threat.Enemy.transform);
            float spriteSize = icons[i].sprite != null
                ? Mathf.Max(icons[i].sprite.bounds.size.x, icons[i].sprite.bounds.size.y)
                : 1f;
            icons[i].transform.localScale = Vector3.one * (iconWorldSize / Mathf.Max(0.001f, spriteSize));
        }
    }

    private static List<ThreatPreview> FindThreateningEnemies(MapInfo destination)
    {
        List<ThreatPreview> result = new List<ThreatPreview>();
        EnemyTurnActor[] enemies = FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            BattleEnemyRuntimeData runtime = enemy.GetComponent<BattleEnemyRuntimeData>();
            if (runtime == null || runtime.Data == null) continue;
            MapInfo origin = FindClosestTile(enemy.transform.position);
            if (origin == null) continue;

            if (BattleTileRangeCalculator.GetDistance(
                    origin,
                    destination,
                    enemy.AttackRangeTiles) >= 0)
            {
                result.Add(new ThreatPreview(enemy.gameObject, ThreatIntent.Attack));
                continue;
            }

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
                    result.Add(new ThreatPreview(enemy.gameObject, ThreatIntent.Chase));
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

    private void EnsureIconCount(int count)
    {
        while (icons.Count < count)
        {
            GameObject child = new GameObject("Enemy Intent Icon");
            child.transform.SetParent(transform, false);
            SpriteRenderer icon = child.AddComponent<SpriteRenderer>();
            icon.sortingOrder = 100;
            icon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            icon.receiveShadows = false;
            icons.Add(icon);
            iconTargets.Add(null);
        }
    }

    private void UpdateIconTransforms()
    {
        Quaternion rotation = targetCamera != null
            ? targetCamera.transform.rotation
            : Quaternion.identity;
        for (int i = 0; i < icons.Count; i++)
        {
            SpriteRenderer icon = icons[i];
            if (icon == null || !icon.enabled) continue;
            icon.transform.rotation = rotation;
            if (i < iconTargets.Count && iconTargets[i] != null)
                icon.transform.position = ResolveIconPosition(iconTargets[i]);
        }
    }

    /// <summary>
    /// EnemyHPBar의 모든 RectTransform을 카메라 화면 위쪽 축으로 투영해 가장 높은 지점을 찾고,
    /// 그 위에 아이콘을 둔다. 카메라가 Side/Top View 사이를 움직여도 HP 프리팹과 겹치지 않는다.
    /// </summary>
    private Vector3 ResolveIconPosition(Transform enemy)
    {
        if (enemy == null) return Vector3.zero;
        Vector3 cameraUp = targetCamera != null ? targetCamera.transform.up : Vector3.up;
        Transform healthBar = FindChildByName(enemy, "EnemyHPBar");
        if (healthBar == null)
            return enemy.position + cameraUp * fallbackIconHeight;

        float enemyProjection = Vector3.Dot(enemy.position, cameraUp);
        float highestProjection = enemyProjection + fallbackIconHeight;
        RectTransform[] rects = healthBar.GetComponentsInChildren<RectTransform>(true);
        Vector3[] corners = new Vector3[4];
        foreach (RectTransform rect in rects)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) continue;
            rect.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++)
                highestProjection = Mathf.Max(highestProjection, Vector3.Dot(corners[i], cameraUp));
        }

        float offset = highestProjection - enemyProjection +
                       iconGapAboveHealthBar + iconWorldSize * 0.5f;
        return enemy.position + cameraUp * offset;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildByName(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    private void HideLines()
    {
        foreach (LineRenderer line in lines) if (line != null) line.enabled = false;
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] != null) icons[i].enabled = false;
            if (i < iconTargets.Count) iconTargets[i] = null;
        }
    }

    private void OnDisable()
    {
        selectedDestination = null;
        hoveredTile = null;
        HideLines();
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
