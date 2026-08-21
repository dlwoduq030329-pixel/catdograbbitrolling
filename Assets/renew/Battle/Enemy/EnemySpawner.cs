using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapGenerator가 만든 Enemy 타일 위에 BattleEnemyDatabase 기반 Enemy를 생성하고 필수 런타임 컴포넌트를 조립한다.
/// 스폰 위치의 Y축은 타일 자체의 Y값과 무관하게 defaultSpawnHeight(기본 0.5)로 고정한다.
/// 2026-08-21 정리: 예전에 "DB 미연결 시 기본 프리팹으로 대체" 호환 경로가 있었으나, enemyDatabase는
/// 항상 채워져 있는 것을 전제로 하는 구조라 실제로 DB가 비는 상황을 정상 케이스로 다루지 않기로 하고
/// 그 호환 필드를 제거했다. DB가 비어 있으면 지금처럼 명확한 Debug.LogError로 실패를 알린다.
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

    /// <summary>근거리·원거리 x 물리·마법 조합별로 표시할 적 유형 아이콘 한 세트.</summary>
    [System.Serializable]
    private struct EnemyTypeIcon
    {
        [InspectorName("공격 방식")]
        public BattleEnemyAttackType attackType;
        [InspectorName("피해 속성")]
        public BattleDamageType damageType;
        [InspectorName("아이콘")]
        public Sprite icon;
    }

    [Header("적 유형별 공격 아이콘")]
    [Tooltip("공격 방식(근거리/원거리) x 피해 속성(물리/마법) 조합마다 표시할 아이콘을 직접 등록한다. " +
             "BattleEnemyData의 attackType·attackDamageType과 일치하는 첫 항목을 사용하며, 일치하는 항목이 없으면 아이콘 없이 표시한다.")]
    [SerializeField] private List<EnemyTypeIcon> typeIcons = new List<EnemyTypeIcon>();

    [Header("맵 배치")]
    [InspectorName("생성할 적 수")]
    [SerializeField, Min(0)] private int enemyCount = 5;
    [InspectorName("플레이어 시작 지점 성역 크기")]
    [SerializeField] private Vector2Int sanctuarySize = new Vector2Int(5, 5);
    [InspectorName("기본 스폰 높이(Y)")]
    [SerializeField] private float defaultSpawnHeight = 0.5f;

    [Header("적 모델 크기를 타일 하나에 맞추기")]
    [Tooltip("적 모델(Renderer 기준)이 타일보다 크거나 작을 때 스폰된 인스턴스만 스케일을 조정해 한 타일 안에 맞춘다. " +
             "원본 프리팹 에셋 자체는 건드리지 않는다.")]
    [InspectorName("타일 크기에 맞춰 스케일 조정")]
    [SerializeField] private bool normalizeEnemyToTile = true;
    [InspectorName("타일 대비 목표 비율")]
    [Tooltip("적 모델의 XZ 폭이 타일 크기의 이 비율이 되도록 스케일을 계산한다(0.75 = 타일의 75%).")]
    [SerializeField, Range(0.1f, 1f)] private float enemyTileFillRatio = 0.75f;
    [InspectorName("작은 적 확대 허용")]
    [Tooltip("끄면 원본보다 작은 적 모델을 확대하지 않고 축소만 허용한다(1배 이하로만 스케일).")]
    [SerializeField] private bool allowEnemyUpscaling;
    [InspectorName("최소 스케일 배율")]
    [Tooltip("계산된 스케일 배율이 이 값보다 작아지지 않도록 하한을 둔다(과도한 축소 방지).")]
    [SerializeField, Range(0.01f, 1f)] private float minimumScaleMultiplier = 0.05f;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();

    public IReadOnlyList<GameObject> SpawnedEnemies => spawnedEnemies;

    /// <summary>
    /// 생성된 맵에 적을 한 번만 배치한다(이미 배치된 적이 있으면 즉시 반환). 순서는:
    /// 1) 플레이어가 서 있는 타일을 찾는다.
    /// 2) 그 타일을 중심으로 sanctuarySize 크기의 사각형("성역") 안에 있는 Road 타일은 스폰 후보에서 뺀다
    ///    (플레이어 시작 지점 바로 옆에 적이 나오지 않도록 하는 안전지대).
    /// 3) 성역 밖 Road 타일 후보를 무작위 순서로 섞고, 앞에서부터 enemyCount개(또는 후보 수가 더 적으면
    ///    후보 수만큼)에 SpawnEnemy를 호출한다.
    /// </summary>
    public void SpawnEnemiesOnGeneratedMap(Transform player)
    {
        if (player == null || spawnedEnemies.Count > 0)
        {
            return;
        }

        MapInfo[] allTiles = FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
        MapInfo playerStartTile = FindClosestTile(allTiles, player.position);
        if (playerStartTile == null)
        {
            Debug.LogError("적 배치 실패: 플레이어 시작 타일을 찾을 수 없습니다.", this);
            return;
        }

        // 플레이어 시작 타일 기준 sanctuarySize 절반 범위(가로·세로)를 "적이 나오면 안 되는 성역"으로 둔다.
        int sanctuaryHalfWidth = Mathf.Max(0, sanctuarySize.x / 2);
        int sanctuaryHalfHeight = Mathf.Max(0, sanctuarySize.y / 2);

        List<MapInfo> spawnableRoadTilesOutsideSanctuary = new List<MapInfo>();
        foreach (MapInfo tile in allTiles)
        {
            if (tile == null || tile.Type != TileType.Road)
            {
                continue;
            }

            Vector2Int tileOffsetFromPlayer = tile.Index - playerStartTile.Index;
            bool insideSanctuary =
                Mathf.Abs(tileOffsetFromPlayer.x) <= sanctuaryHalfWidth &&
                Mathf.Abs(tileOffsetFromPlayer.y) <= sanctuaryHalfHeight;
            if (insideSanctuary)
            {
                continue;
            }

            spawnableRoadTilesOutsideSanctuary.Add(tile);
        }

        Shuffle(spawnableRoadTilesOutsideSanctuary);
        int spawnCount = Mathf.Min(enemyCount, spawnableRoadTilesOutsideSanctuary.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnEnemy(spawnableRoadTilesOutsideSanctuary[i].transform);
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
        GameObject selectedPrefab = selectedData != null ? selectedData.prefab : null;

        if (selectedPrefab == null)
        {
            Debug.LogError("적 생성 실패: enemyDatabase에서 유효한 적 데이터를 찾지 못했습니다.", this);
            return null;
        }

        Vector3 spawnPosition = enemyTile.position;
        spawnPosition.y = defaultSpawnHeight;

        // 월드 좌표에 우선 Instantiate한다. enemyTile을 parent로 바로 넘겨 Instantiate하면 Road 타일
        // 프리팹의 Scale을 그대로 물려받아서, 아직 원래 크기를 모르는 상태로 NormalizeEnemyFootprint의
        // 크기 측정이 왜곡된다. 그래서 부모 없이 먼저 만들고 크기를 재서 스케일을 정한 다음(아래
        // NormalizeEnemyFootprint), 그 다음에 enemyTile의 자식으로 옮긴다(SetParent worldPositionStays=true라
        // 지금까지 계산한 월드 위치·스케일은 유지된다). 결과적으로 이 Enemy는 지금도 타일의 자식 오브젝트다.
        GameObject enemy = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        NormalizeEnemyFootprint(enemy, enemyTile);
        enemy.transform.SetParent(enemyTile, true);

        // GetComponentInChildren로 찾는 이유: EnemyDetector는 Prefab에 따라 루트가 아니라
        // 눈 위치를 표현하는 자식 오브젝트에 붙어있을 수 있다(EnemyDetector.eyePoint 참고).
        // BattleComponentResolver.GetOrAdd는 루트에서만 찾고 없으면 루트에 새로 붙이므로,
        // 자식에 이미 있는 경우를 놓치고 중복 부착할 수 있어 여기서는 쓰지 않는다.
        EnemyDetector detector = enemy.GetComponentInChildren<EnemyDetector>();
        if (detector == null)
        {
            detector = enemy.AddComponent<EnemyDetector>();
        }
        detector.ConfigureDetectRange(selectedData.detectRange);

        EnemyTurnActor turnActor = BattleComponentResolver.GetOrAdd<EnemyTurnActor>(enemy, null);

        // Enemy 행동 실행기는 생성 직후부터 구성 상태를 Inspector에서 확인할 수 있도록 보장한다.
        // EnemyTurnActor도 구형 Scene 호환을 위해 실행 시점 자동 보완을 유지한다.
        BattleComponentResolver.GetOrAdd<BattleEnemyActionExecutor>(enemy, null);

        EnemyAwareness awareness = BattleComponentResolver.GetOrAdd<EnemyAwareness>(enemy, null);

        BattleComponentResolver.GetOrAdd<PathDebugView>(enemy, null);

        BattleUnitMP enemyMP = BattleComponentResolver.GetOrAdd<BattleUnitMP>(enemy, null);
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

            enemyMP.ConfigureMaxMP(Mathf.Max(selectedData.minTurnMP, selectedData.maxTurnMP));
        }

        spawnedEnemies.Add(enemy);
        EnemySpawned?.Invoke(enemy);
        return enemy;
    }

    /// <summary>
    /// typeIcons 목록에서 이 Enemy 데이터의 공격 방식·피해 속성과 일치하는 첫 항목의 아이콘을 반환한다.
    /// 일치하는 항목이 없으면 null(아이콘 표시 안 함)을 반환한다.
    /// </summary>
    private Sprite ResolveTypeIcon(BattleEnemyData data)
    {
        foreach (EnemyTypeIcon entry in typeIcons)
        {
            if (entry.attackType == data.attackType && entry.damageType == data.attackDamageType)
            {
                return entry.icon;
            }
        }

        return null;
    }

    /// <summary>
    /// 스폰된 이 인스턴스만(원본 프리팹 에셋은 그대로 두고) 균등 스케일을 곱해 XZ 기준 모델 크기가
    /// 타일 하나(enemyTileFillRatio 비율)에 맞도록 줄이거나(allowEnemyUpscaling이 true면) 키운다.
    /// 아직 enemyTile의 자식이 되기 전(Road의 Scale을 물려받기 전) 시점에 호출해야 정확히 측정된다.
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

    /// <summary>
    /// 이 Enemy의 실제 모델 크기(MeshRenderer·SkinnedMeshRenderer만)를 합산한 Bounds를 구한다.
    /// 파티클이나 런타임에 붙는 UI(체력바 등) Renderer는 모델 크기가 아니므로 측정에서 제외한다.
    /// </summary>
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

    /// <summary>
    /// 이 Road 타일의 실제 크기를 구한다. Collider가 있으면 그 Bounds를 우선 쓰고,
    /// 없으면 Renderer Bounds로 대체한다(둘 다 없으면 실패).
    /// </summary>
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

    /// <summary>useRandomDatabaseEntry 설정에 따라 enemyDatabase에서 무작위 항목 또는 databaseIndex 고정 항목을 고른다.</summary>
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

    /// <summary>월드 좌표(XYZ 전체 거리 기준)에서 가장 가까운 타일을 찾는다. BattleTileLocator의 공용 계산기를 그대로 감싼 것이다.</summary>
    private static MapInfo FindClosestTile(MapInfo[] tiles, Vector3 position)
    {
        return BattleTileLocator.FindClosest3D(position, tiles);
    }

    /// <summary>
    /// 거리 계산이 아니라 목록 순서를 무작위로 섞는 함수다(Fisher-Yates 셔플).
    /// SpawnEnemiesOnGeneratedMap이 스폰 후보 타일을 무작위 순서로 만든 뒤 앞에서부터 enemyCount개를
    /// 골라 쓰기 위해 사용한다.
    /// </summary>
    private static void Shuffle(List<MapInfo> tiles)
    {
        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (tiles[i], tiles[swapIndex]) = (tiles[swapIndex], tiles[i]);
        }
    }
}
