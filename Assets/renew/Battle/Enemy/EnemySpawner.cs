using System.Collections.Generic;
using UnityEngine;
/// <summary>적의 대여·배치·참가 등록·반환을 담당한다. 최초 조립과 모델 보정은 전용 클래스에 맡긴다.</summary>
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyPool enemyPool;
    [SerializeField] private GameObject playerBody;
    private BattleUnitRegistry unitRegistry;
    private BattleMapRegistry mapRegistry;
    private BattleDataPool dataPool;

    //enemy가 유저 따라올떄 쓰는 코드 보여줘.

    public void ConfigureRegistries(BattleUnitRegistry units, BattleMapRegistry map, BattleDataPool shared)
    { unitRegistry = units; mapRegistry = map; dataPool = shared; }

    /// <summary>DB 종류별 고정 수량을 최초 한 번 준비한다. 비활성 인스턴스는 참가 목록에 등록하지 않는다.</summary>
    public void PreparePool(BattleEnemyDatabase database, Transform referenceTile)
    {
        if (enemyPool != null && referenceTile != null)
            enemyPool.Prepare(database, data => CreateEnemy(referenceTile, data));
    }

    public int AvailableCount(BattleEnemyData data) => enemyPool != null ? enemyPool.Available(data) : int.MaxValue;

    /// <summary>Stage 전환 시 생존·사망 연출 중인 적을 모두 비활성화한다.</summary>
    public void ReleaseAll()
    {
        unitRegistry?.DrainPendingUnregisters();
        foreach (GameObject enemy in spawnedEnemies.ToArray()) ReleaseEnemy(enemy);
        spawnedEnemies.Clear();
    }

    private void ReleaseEnemy(GameObject enemy)
    {
        if (enemy == null) return;
        unitRegistry?.DrainPendingUnregisters();
        unitRegistry?.UnregisterEnemy(enemy);
        mapRegistry?.RemoveUnit(enemy);
        spawnedEnemies.Remove(enemy);
        enemy.SetActive(false);
    }
    public event System.Action<GameObject> EnemySpawned;

    [Header("적 데이터베이스")]
    [InspectorName("전투 적 데이터베이스")]
    [SerializeField] private BattleEnemyDatabase enemyDatabase;
    [InspectorName("데이터 항목 무작위 선택")]
    [SerializeField] private bool useRandomDatabaseEntry = true;
    [InspectorName("고정 데이터 인덱스")]
    [SerializeField, Min(0)] private int databaseIndex;
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
    private Transform enemyListContainer;

    public IReadOnlyList<GameObject> SpawnedEnemies => spawnedEnemies;
    /// <summary>StageSpawner를 쓰지 않는 기존 씬의 최초 배치 경로다. 보스 소환은 SpawnEnemy를 사용한다.</summary>
    public void SpawnEnemiesOnGeneratedMap(Transform player)
    {
        if (player == null || spawnedEnemies.Count > 0)
        {
            return;
        }
        MapInfo[] allTiles = FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
        Debug.Log("모든 타일 찾기 갯수는 " + allTiles.Length);
        MapInfo playerStartTile = FindClosestTile(allTiles, player.position);
        if (playerStartTile == null)
        {
            Debug.LogError("적 배치 실패: 플레이어 시작 타일을 찾을 수 없습니다.", this);
            return;
        }
        PreparePool(enemyDatabase, playerStartTile.transform);
        int sanctuaryHalfWidth = Mathf.Max(0, sanctuarySize.x / 3);
        int sanctuaryHalfHeight = Mathf.Max(0, sanctuarySize.y / 3);
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
    public GameObject SpawnEnemy(Transform enemyTile)
    {
        return SpawnEnemy(enemyTile, SelectEnemyData());
    }
    /// <summary>Stage 배치와 보스 소환이 공유한다. 지정한 종류의 풀이 소진되면 null을 반환한다.</summary>
    public GameObject SpawnEnemy(Transform enemyTile, BattleEnemyData selectedData)
    {
        if (enemyTile == null || selectedData == null || selectedData.prefab == null) return null;
        GameObject enemy = enemyPool != null ? enemyPool.Take(selectedData) : CreateEnemy(enemyTile, selectedData);
        if (enemy == null) return null;
        unitRegistry?.DrainPendingUnregisters();
        if (!enemy.activeSelf)
        {
            var death = enemy.GetComponent<BattleEnemyDeathHandler>();
            death.ResetForReuse();
            enemy.transform.position = enemyTile.position + death.SpawnOffset;
            enemy.transform.rotation = Quaternion.identity;
            enemy.GetComponent<BattleHealth>().Initialize(selectedData.maxHP);
            enemy.GetComponent<BattleUnitMP>().RestoreFull();
            enemy.GetComponent<EnemyAwareness>().ClearTarget();
            enemy.GetComponent<BattleStatusEffects>()?.ClearAllNegativeStatuses();
            enemy.GetComponent<EnemyTurnActor>().ResetForSpawn(dataPool);
            enemy.SetActive(true);
        }
        spawnedEnemies.Add(enemy);
        EnemySpawned?.Invoke(enemy);
        enemy.GetComponent<EnemyTurnActor>().PrepareNextTurnMP();
        return enemy;
    }

    private GameObject CreateEnemy(Transform enemyTile, BattleEnemyData selectedData)
    {
        if (enemyTile == null)
        {
            Debug.LogError("적 생성 실패: 적 타일 참조가 없습니다.", this);
            return null;
        }

        GameObject selectedPrefab = selectedData != null ? selectedData.prefab : null;

        if (selectedPrefab == null)
        {
            Debug.LogError("적 생성 실패: enemyDatabase에서 유효한 적 데이터를 찾지 못했습니다.", this);
            return null;
        }
        Vector3 spawnPosition = enemyTile.position + new Vector3(0f, defaultSpawnHeight, 0f);
        GameObject enemy = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);

        enemy.GetComponent<DisableFarEnemy>().SetTarget(playerBody);
        if (normalizeEnemyToTile)
            EnemySpawnGeometry.Fit(enemy, enemyTile, enemyTileFillRatio, allowEnemyUpscaling, minimumScaleMultiplier);
        enemy.transform.SetParent(GetOrCreateEnemyListContainer(), true);
        EnemySpawnGeometry.EnsureCollider(enemy);
        EnemyRuntimeFactory.Configure(enemy, selectedData, ResolveTypeIcon(selectedData),
            unitRegistry, dataPool, ReleaseEnemy, enemy.transform.position - enemyTile.position);
        return enemy;
    }
    private Transform GetOrCreateEnemyListContainer()
    {
        if (enemyListContainer == null)
        {
            enemyListContainer = new GameObject("Current EnemyList").transform;
            enemyListContainer.SetParent(transform, false);
        }

        return enemyListContainer;
    }
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
