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
    private readonly List<LineRenderer> threatLinePool = new List<LineRenderer>();
    private readonly List<SpriteRenderer> threatIconPool = new List<SpriteRenderer>();
    private readonly List<Transform> threatIconEnemyTargets = new List<Transform>();
    private BattleRaycaster mapPointerRaycaster;
    private BattlePlayerRangeController playerMoveRange;
    private MapInfo lastPreviewedHoverTile;
    private MapInfo lockedMoveDestination;
    private Material sharedThreatLineMaterial;

    private enum EnemyThreatIntent { Attack, Chase }

    private readonly struct EnemyThreatResult
    {
        public readonly GameObject Enemy;
        public readonly EnemyThreatIntent Intent;

        public EnemyThreatResult(GameObject enemy, EnemyThreatIntent intent)
        {
            Enemy = enemy;
            Intent = intent;
        }
    }

    /// <summary>
    /// 현재 컴포넌트가 BattleUnitMoveFlow에서 런타임 생성되므로 Inspector 아이콘 참조가 비어 있을 때만
    /// Resources의 기본 검·눈 아이콘을 임시로 연결한다. 컴포넌트를 직접 배치한 뒤에는 이 폴백을 제거한다.
    /// </summary>
    private void Awake()
    {
        if (attackIcon == null)
            attackIcon = Resources.Load<Sprite>("Battle/UI/ThreatIcons/EnemyIntent_Attack");
        if (chaseIcon == null)
            chaseIcon = Resources.Load<Sprite>("Battle/UI/ThreatIcons/EnemyIntent_Chase");
    }

    /// <summary>
    /// 이동 미리보기에 필요한 전투 카메라, 마우스 타일 판정기와 Player 이동 가능 범위 제공자를 연결한다.
    /// 이 함수는 데이터를 계산하지 않고 BattleUnitMoveFlow가 가진 의존성만 전달한다.
    /// </summary>
    public void ConfigureDependencies(
        Camera battleCamera,
        BattleRaycaster pointerRaycaster,
        BattlePlayerRangeController moveRangeController)
    {
        targetCamera = battleCamera;
        mapPointerRaycaster = pointerRaycaster;
        playerMoveRange = moveRangeController;
    }

    /// <summary>
    /// 목적지가 확정 대기 중이면 해당 타일의 위협 표시를 고정하고, 아니면 현재 마우스 아래의 이동 가능 타일을
    /// 찾아 미리보기를 갱신한다. 같은 타일을 계속 가리키는 동안 Enemy 위협 계산과 선 재구성은 반복하지 않고,
    /// 카메라 변화에 따른 아이콘 위치·회전만 매 프레임 보정한다.
    /// </summary>
    private void Update()
    {
        if (lockedMoveDestination != null)
        {
            bool blockedByModal = BattleGameManager.Instance != null &&
                                  BattleGameManager.Instance.IsModalInteractionOpen;
            if (blockedByModal || !BattleRangeVisibilityTracker.IsAnyRangeVisible)
            {
                HideAllThreatVisuals();
                return;
            }

            if (lastPreviewedHoverTile != lockedMoveDestination)
            {
                lastPreviewedHoverTile = lockedMoveDestination;
                RebuildThreatVisuals(lockedMoveDestination);
            }
            UpdateThreatIconTransforms();
            return;
        }

        if (!BattleRangeVisibilityTracker.IsAnyRangeVisible || mapPointerRaycaster == null || playerMoveRange == null ||
            BattlePlayerInputReader.IsPointerOverInteractiveUI(Input.mousePosition) ||
            !mapPointerRaycaster.TryGetMapTile(Input.mousePosition, out MapInfo hoveredMapTile) ||
            !playerMoveRange.IsReachable(hoveredMapTile))
        {
            lastPreviewedHoverTile = null;
            HideAllThreatVisuals();
            return;
        }

        if (lastPreviewedHoverTile != hoveredMapTile)
        {
            lastPreviewedHoverTile = hoveredMapTile;
            RebuildThreatVisuals(hoveredMapTile);
        }
        UpdateThreatIconTransforms();
    }

    /// <summary>선택한 이동 목적지의 Enemy 의도를 Player 클릭으로 확정할 때까지 고정한다.</summary>
    public void ShowSelectedDestination(MapInfo destination)
    {
        lockedMoveDestination = destination;
        lastPreviewedHoverTile = null;
        if (lockedMoveDestination != null)
        {
            RebuildThreatVisuals(lockedMoveDestination);
        }
    }

    /// <summary>이동 취소·완료 시 고정된 의도 표시를 제거하고 다시 호버 미리보기 상태로 돌린다.</summary>
    public void ClearSelectedDestination()
    {
        lockedMoveDestination = null;
        lastPreviewedHoverTile = null;
        HideAllThreatVisuals();
    }

    /// <summary>
    /// 이동 후보 타일을 기준으로 공격 또는 추격할 Enemy 목록을 계산하고, 재사용 중인 LineRenderer와
    /// SpriteRenderer에 위치·색상·아이콘을 적용한다. 필요한 개수보다 많은 풀 항목은 비활성화한다.
    /// </summary>
    private void RebuildThreatVisuals(MapInfo moveDestination)
    {
        List<EnemyThreatResult> enemyThreats = CalculateEnemyThreats(moveDestination);
        EnsureThreatLinePoolSize(enemyThreats.Count);
        EnsureThreatIconPoolSize(threatLinePool.Count);
        for (int previewIndex = 0; previewIndex < threatLinePool.Count; previewIndex++)
        {
            bool visible = previewIndex < enemyThreats.Count;
            threatLinePool[previewIndex].enabled = visible;
            threatIconPool[previewIndex].enabled = visible;
            threatIconEnemyTargets[previewIndex] = visible
                ? enemyThreats[previewIndex].Enemy.transform
                : null;
            if (!visible) continue;
            EnemyThreatResult threat = enemyThreats[previewIndex];
            Vector3 from = threat.Enemy.transform.position;
            Vector3 to = moveDestination.transform.position;
            from.y += lineHeight;
            to.y += lineHeight;
            threatLinePool[previewIndex].SetPosition(0, from);
            threatLinePool[previewIndex].SetPosition(1, to);
            Color intentColor = threat.Intent == EnemyThreatIntent.Attack ? lineColor : chaseLineColor;
            threatLinePool[previewIndex].startColor = threatLinePool[previewIndex].endColor = intentColor;
            threatIconPool[previewIndex].sprite = threat.Intent == EnemyThreatIntent.Attack ? attackIcon : chaseIcon;
            threatIconPool[previewIndex].transform.position = ResolveThreatIconPosition(threat.Enemy.transform);
            float spriteSize = threatIconPool[previewIndex].sprite != null
                ? Mathf.Max(threatIconPool[previewIndex].sprite.bounds.size.x, threatIconPool[previewIndex].sprite.bounds.size.y)
                : 1f;
            threatIconPool[previewIndex].transform.localScale =
                Vector3.one * (iconWorldSize / Mathf.Max(0.001f, spriteSize));
        }
    }

    /// <summary>
    /// 후보 목적지에 이미 공격 사거리가 닿는 Enemy는 Attack으로 분류한다. 바로 공격할 수 없는 Enemy는
    /// 기본 공격 MP를 남긴 이동 가능 범위를 계산하고, 이동 후 사거리가 닿는 위치가 있으면 Chase로 분류한다.
    /// </summary>
    private static List<EnemyThreatResult> CalculateEnemyThreats(MapInfo moveDestination)
    {
        List<EnemyThreatResult> threatResults = new List<EnemyThreatResult>();
        EnemyTurnActor[] activeEnemyActors = FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in activeEnemyActors)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            BattleEnemyRuntimeData runtime = enemy.GetComponent<BattleEnemyRuntimeData>();
            if (runtime == null || runtime.Data == null) continue;
            MapInfo origin = FindMapTileClosestToPosition(enemy.transform.position);
            if (origin == null) continue;

            if (BattleTileRangeCalculator.GetDistance(
                    origin,
                    moveDestination,
                    enemy.AttackRangeTiles) >= 0)
            {
                threatResults.Add(new EnemyThreatResult(enemy.gameObject, EnemyThreatIntent.Attack));
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
                        moveDestination,
                        enemy.AttackRangeTiles) >= 0)
                {
                    threatResults.Add(new EnemyThreatResult(enemy.gameObject, EnemyThreatIntent.Chase));
                    break;
                }
            }
        }
        return threatResults;
    }

    /// <summary>Enemy 월드 위치와 가장 가까운 MapInfo를 Registry에서 찾고, Registry가 비었을 때만 Scene 검색으로 보완한다.</summary>
    private static MapInfo FindMapTileClosestToPosition(Vector3 worldPosition)
    {
        BattleMapRegistry registry = FindFirstObjectByType<BattleMapRegistry>(FindObjectsInactive.Include);
        if (registry != null && registry.Tiles.Count > 0) return registry.FindClosestTile(worldPosition);
        return BattleTileLocator.FindClosestXZ(worldPosition, FindObjectsByType<MapInfo>(FindObjectsSortMode.None));
    }

    /// <summary>필요한 Enemy 수만큼 연결선 풀을 늘린다. 기존 LineRenderer는 삭제하지 않고 다음 미리보기에 재사용한다.</summary>
    private void EnsureThreatLinePoolSize(int requiredCount)
    {
        if (sharedThreatLineMaterial == null) sharedThreatLineMaterial = new Material(Shader.Find("Sprites/Default"));
        while (threatLinePool.Count < requiredCount)
        {
            GameObject child = new GameObject("Enemy Threat Line");
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.startColor = line.endColor = lineColor;
            line.material = sharedThreatLineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            threatLinePool.Add(line);
        }
    }

    /// <summary>연결선 풀과 같은 개수까지 공격·추격 아이콘 풀을 늘리고 각 아이콘의 Enemy 추적 슬롯을 준비한다.</summary>
    private void EnsureThreatIconPoolSize(int requiredCount)
    {
        while (threatIconPool.Count < requiredCount)
        {
            GameObject child = new GameObject("Enemy Intent Icon");
            child.transform.SetParent(transform, false);
            SpriteRenderer icon = child.AddComponent<SpriteRenderer>();
            icon.sortingOrder = 100;
            icon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            icon.receiveShadows = false;
            threatIconPool.Add(icon);
            threatIconEnemyTargets.Add(null);
        }
    }

    /// <summary>활성 아이콘이 카메라를 향하고 담당 Enemy HP 바 위를 계속 따라가도록 회전과 위치를 갱신한다.</summary>
    private void UpdateThreatIconTransforms()
    {
        Quaternion rotation = targetCamera != null
            ? targetCamera.transform.rotation
            : Quaternion.identity;
        for (int i = 0; i < threatIconPool.Count; i++)
        {
            SpriteRenderer icon = threatIconPool[i];
            if (icon == null || !icon.enabled) continue;
            icon.transform.rotation = rotation;
            if (i < threatIconEnemyTargets.Count && threatIconEnemyTargets[i] != null)
                icon.transform.position = ResolveThreatIconPosition(threatIconEnemyTargets[i]);
        }
    }

    /// <summary>
    /// EnemyHPBar의 모든 RectTransform을 카메라 화면 위쪽 축으로 투영해 가장 높은 지점을 찾고,
    /// 그 위에 아이콘을 둔다. 카메라가 Side/Top View 사이를 움직여도 HP 프리팹과 겹치지 않는다.
    /// </summary>
    private Vector3 ResolveThreatIconPosition(Transform enemy)
    {
        if (enemy == null) return Vector3.zero;
        Vector3 cameraUp = targetCamera != null ? targetCamera.transform.up : Vector3.up;
        Transform healthBar = FindDescendantByName(enemy, "EnemyHPBar");
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

    /// <summary>Enemy 자식 계층에서 EnemyHPBar 기준점을 찾기 위해 이름이 같은 하위 Transform을 재귀 탐색한다.</summary>
    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDescendantByName(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 이동 범위가 닫힘, 포인터가 유효 타일을 벗어남, UI 위로 이동, 모달 UI 열림, 이동 취소·완료 또는
    /// 컴포넌트 비활성화 시 모든 연결선과 아이콘을 숨기고 Enemy 추적 참조를 비운다.
    /// </summary>
    private void HideAllThreatVisuals()
    {
        foreach (LineRenderer line in threatLinePool) if (line != null) line.enabled = false;
        for (int i = 0; i < threatIconPool.Count; i++)
        {
            if (threatIconPool[i] != null) threatIconPool[i].enabled = false;
            if (i < threatIconEnemyTargets.Count) threatIconEnemyTargets[i] = null;
        }
    }

    private void OnDisable()
    {
        lockedMoveDestination = null;
        lastPreviewedHoverTile = null;
        HideAllThreatVisuals();
    }

    private void OnDestroy()
    {
        if (sharedThreatLineMaterial != null) Destroy(sharedThreatLineMaterial);
    }
}
