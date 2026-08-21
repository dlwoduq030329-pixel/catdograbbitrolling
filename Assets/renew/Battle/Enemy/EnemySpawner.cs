using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapGenerator가 만든 Enemy 타일 위에 DB 기반 Enemy를 생성하고 필수 런타임 컴포넌트를 조립한다.
/// 적 데이터가 없으면 기본 프리팹으로 적을 생성한다.
/// 스폰 위치의 Y축은 타일 자체의 Y값과 무관하게 defaultSpawnHeight(기본 0.5)로 고정한다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    /// <summary>새 Enemy가 생성되고 런타임 구성이 끝난 뒤 전달한다.</summary>
    public event System.Action<GameObject> EnemySpawned;

    [Header("적 데이터베이스")]
    [InspectorName("전투 적 데이터베이스")]
    [SerializeField] private BattleEnemyDatabase enemyDatabase;
    [InspectorName("데이터 항목 무작위 선택")]
    [SerializeField] private bool useRandomDatabaseEntry = true;
    [InspectorName("고정 데이터 인덱스")]
    [SerializeField, Min(0)] private int databaseIndex;

    [Header("데이터베이스 미연결 호환 설정")]
    [InspectorName("기본 적 프리팹")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Enemy Type Icons")]
    [InspectorName("Ranged Physical Icon")]
    [SerializeField] private Sprite rangedPhysicalIcon;
    [InspectorName("Ranged Magic Icon")]
    [SerializeField] private Sprite rangedMagicIcon;
    [InspectorName("Melee Physical Icon")]
    [SerializeField] private Sprite meleePhysicalIcon;
    [InspectorName("Melee Magic Icon")]
    [SerializeField] private Sprite meleeMagicIcon;

    [Header("맵 배치")]
    [InspectorName("생성할 적 수")]
    [SerializeField, Min(0)] private int enemyCount = 5;
    [InspectorName("플레이어 시작 지점 성역 크기")]
    [SerializeField] private Vector2Int sanctuarySize = new Vector2Int(5, 5);
    [InspectorName("기본 스폰 높이(Y)")]
    [SerializeField] private float defaultSpawnHeight = 0.5f;

    [Header("Enemy Tile Footprint")]
    [InspectorName("Normalize Enemy To One Tile")]
    [Tooltip("Shrinks oversized Enemy renderers to fit inside one Road tile. The original prefab asset is not modified.")]
    [SerializeField] private bool normalizeEnemyToTile = true;
    [InspectorName("Tile Fill Ratio")]
    [SerializeField, Range(0.1f, 1f)] private float enemyTileFillRatio = 0.75f;
    [InspectorName("Allow Small Enemy Upscaling")]
    [SerializeField] private bool allowEnemyUpscaling;
    [InspectorName("Minimum Scale Multiplier")]
    [SerializeField, Range(0.01f, 1f)] private float minimumScaleMultiplier = 0.05f;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();

    public IReadOnlyList<GameObject> SpawnedEnemies => spawnedEnemies;

    /// <summary>생성된 Road 타일 중 플레이어 주변 성역을 제외한 위치에 적을 배치한다.</summary>
    public void SpawnEnemiesOnGeneratedMap(Transform player)
    {
        if (player == null || spawnedEnemies.Count > 0)
        {
            return;
        }

        MapInfo[] allTiles = FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
        Debug.Log("모든 타일 찾기 갯수는 " + allTiles.Length);
        List<MapInfo> candidates = new List<MapInfo>();
        MapInfo playerTile = FindClosestTile(allTiles, player.position);
        if (playerTile == null)
        {
            Debug.LogError("적 배치 실패: 플레이어 시작 타일을 찾을 수 없습니다.", this);
            return;
        }

        int halfWidth = Mathf.Max(0, sanctuarySize.x / 2);
        int halfHeight = Mathf.Max(0, sanctuarySize.y / 2);
        foreach (MapInfo tile in allTiles)
        {
            if (tile == null || tile.Type != TileType.Road)
            {
                continue;
            }

            Vector2Int delta = tile.Index - playerTile.Index;
            if (Mathf.Abs(delta.x) <= halfWidth && Mathf.Abs(delta.y) <= halfHeight)
            {
                continue;
            }

            candidates.Add(tile);
        }

        Shuffle(candidates);
        int spawnCount = Mathf.Min(enemyCount, candidates.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnEnemy(candidates[i].transform);
        }
    }

    /// <summary>
    /// 지정된 Enemy 타일에 Enemy를 생성하고 Detector, TurnActor, Awareness, MP 등을 보장한다.
    /// </summary>
    public GameObject SpawnEnemy(Transform enemyTile)
    {
        if (enemyTile == null)
        {
            Debug.LogError("적 생성 실패: 적 타일 참조가 없습니다.", this);
            return null;
        }

        BattleEnemyData selectedData = SelectEnemyData();
        GameObject selectedPrefab = selectedData != null ? selectedData.prefab : enemyPrefab;

        if (selectedPrefab == null)
        {
            Debug.LogError("적 생성 실패: DB 데이터와 기본 프리팹이 모두 없습니다.", this);
            return null;
        }

        Vector3 spawnPosition = enemyTile.position;
        spawnPosition.y = defaultSpawnHeight;

        // Instantiate in world space first. Passing enemyTile directly to Instantiate makes the
        // Enemy inherit the Road prefab scale before its visual footprint can be measured.
        GameObject enemy = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        NormalizeEnemyFootprint(enemy, enemyTile);
        enemy.transform.SetParent(enemyTile, true);

        EnemyDetector detector = enemy.GetComponentInChildren<EnemyDetector>();
        if (detector == null)
        {
            detector = enemy.AddComponent<EnemyDetector>();
            Debug.LogWarning(
                $"{enemy.name} 프리팹에 EnemyDetector가 없어 런타임에 기본 컴포넌트를 추가했습니다.",
                enemy);
        }

        if (selectedData != null)
        {
            detector.ConfigureDetectRange(selectedData.detectRange);
        }

        EnemyTurnActor turnActor = BattleComponentResolver.GetOrAdd<EnemyTurnActor>(enemy, null);

        // Enemy 행동 실행기는 생성 직후부터 구성 상태를 Inspector에서 확인할 수 있도록 보장한다.
        // EnemyTurnActor도 구형 Scene 호환을 위해 실행 시점 자동 보완을 유지한다.
        BattleComponentResolver.GetOrAdd<BattleEnemyActionExecutor>(enemy, null);

        EnemyAwareness awareness = BattleComponentResolver.GetOrAdd<EnemyAwareness>(enemy, null);

        BattleComponentResolver.GetOrAdd<PathDebugView>(enemy, null);

        CharacterMP enemyMP = BattleComponentResolver.GetOrAdd<CharacterMP>(enemy, null);
        BattleHealth enemyHealth = BattleComponentResolver.GetOrAdd<BattleHealth>(enemy, null);
        BattleEnemyDeathHandler deathHandler =
            BattleComponentResolver.GetOrAdd<BattleEnemyDeathHandler>(enemy, null);
        BattleComponentResolver.GetOrAdd<BattleEnemyControlState>(enemy, null);
        BattleComponentResolver.GetOrAdd<BattleEnemyStatusView>(enemy, null);

        enemyHealth.Initialize(selectedData != null ? selectedData.maxHP : 10f);
        deathHandler.Configure(enemyHealth);
        Sprite typeIcon = selectedData != null ? ResolveTypeIcon(selectedData) : null;
        BattleHealthBarFactory.AttachEnemyBar(enemy, enemyHealth, enemyMP, typeIcon);

        if (selectedData != null)
        {
            BattleEnemyRuntimeData runtimeData =
                BattleComponentResolver.GetOrAdd<BattleEnemyRuntimeData>(enemy, null);

            runtimeData.Initialize(selectedData);
            turnActor.ConfigureFromData(selectedData);
            awareness.ConfigureAlertRange(selectedData.alertRange);

            enemyMP.ConfigureFixedMaxMP(Mathf.Max(selectedData.minTurnMP, selectedData.maxTurnMP));
        }

        spawnedEnemies.Add(enemy);
        EnemySpawned?.Invoke(enemy);
        return enemy;
    }

    /// <summary>Selects one of the four authored type icons from attack range and damage type.</summary>
    private Sprite ResolveTypeIcon(BattleEnemyData data)
    {
        bool ranged = data.attackType == BattleEnemyAttackType.Ranged;
        bool magic = data.attackDamageType == BattleDamageType.Magic;
        if (ranged) return magic ? rangedMagicIcon : rangedPhysicalIcon;
        return magic ? meleeMagicIcon : meleePhysicalIcon;
    }

    /// <summary>
    /// Uniformly scales only the spawned instance so its XZ renderer bounds fit one Road tile.
    /// Parenting happens afterwards with worldPositionStays=true, preventing Road scale inheritance.
    /// </summary>
    private void NormalizeEnemyFootprint(GameObject enemy, Transform tile)
    {
        if (!normalizeEnemyToTile || enemy == null || tile == null ||
            !TryGetVisualBounds(enemy, out Bounds enemyBounds) ||
            !TryGetTileBounds(tile, out Bounds tileBounds))
        {
            return;
        }

        float enemyFootprint = Mathf.Max(enemyBounds.size.x, enemyBounds.size.z);
        float tileFootprint = Mathf.Min(tileBounds.size.x, tileBounds.size.z) * enemyTileFillRatio;
        if (enemyFootprint <= 0.001f || tileFootprint <= 0.001f)
        {
            return;
        }

        float multiplier = tileFootprint / enemyFootprint;
        if (!allowEnemyUpscaling)
        {
            multiplier = Mathf.Min(1f, multiplier);
        }
        multiplier = Mathf.Max(minimumScaleMultiplier, multiplier);
        enemy.transform.localScale *= multiplier;
        Debug.Log(
            $"[Enemy Scale] {enemy.name}: model footprint {enemyFootprint:0.##}, " +
            $"tile target {tileFootprint:0.##}, scale multiplier {multiplier:0.###}",
            enemy);
    }

    /// <summary>Combines only model renderers, excluding particles and runtime UI from size measurement.</summary>
    private static bool TryGetVisualBounds(GameObject owner, out Bounds bounds)
    {
        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return found;
    }

    /// <summary>Uses the Road collider first and falls back to its renderer bounds.</summary>
    private static bool TryGetTileBounds(Transform tile, out Bounds bounds)
    {
        Collider tileCollider = tile.GetComponentInChildren<Collider>();
        if (tileCollider != null)
        {
            bounds = tileCollider.bounds;
            return true;
        }

        Renderer tileRenderer = tile.GetComponentInChildren<Renderer>();
        if (tileRenderer != null)
        {
            bounds = tileRenderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>파괴된 Enemy 참조를 생성 목록에서 제거한다.</summary>
    public void ClearMissingEnemyReferences()
    {
        spawnedEnemies.RemoveAll(enemy => enemy == null);
    }

    /// <summary>설정에 따라 DB의 무작위 항목 또는 고정 인덱스 항목을 선택한다.</summary>
    private BattleEnemyData SelectEnemyData()
    {
        if (enemyDatabase == null || enemyDatabase.Count == 0)
        {
            return null;
        }

        return useRandomDatabaseEntry
            ? enemyDatabase.GetRandom()
            : enemyDatabase.GetAt(databaseIndex);
    }

    private static MapInfo FindClosestTile(MapInfo[] tiles, Vector3 position)
    {
        return BattleTileLocator.FindClosest3D(position, tiles);
    }

    private static void Shuffle(List<MapInfo> tiles)
    {
        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (tiles[i], tiles[swapIndex]) = (tiles[swapIndex], tiles[i]);
        }
    }
}
