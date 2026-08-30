using System.Collections;
using UnityEngine;

/// <summary>
/// Moon Battle Scene의 고정 컴포넌트를 연결하고 런타임에 생성되는 Player·Enemy·Map을 Registry에 최초 등록하는 조립 시작점이다.
/// 전투 규칙을 실행하거나 데이터를 소유하지 않고 Spawn→Register, Death→Unregister 생명주기 연결만 책임져야 한다.
/// 현재 DataPool 복사, 런타임 AddComponent와 Scene 타일 검색은 전환기 호환 코드이며 직접 참조 전환 후 제거한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleSceneInstaller : MonoBehaviour
{
    [Header("저장소")]
    [SerializeField] private BattleDataPool dataPool;
    [SerializeField] private BattleUnitRegistry unitRegistry;
    [SerializeField] private BattleMapRegistry mapRegistry;
    [SerializeField] private BattleEnemyTargetSync enemyTargetSync;

    [Header("현재 Scene 시스템")]
    [SerializeField] private BattleGameManager battleGameManager;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private NewMapGenerator renewMapGenerator;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private GameObject playerBody;
    [SerializeField] private Camera battleCamera;
    [SerializeField] private GameObject playerSelectCanvas;
    [SerializeField] private GameObject battleCanvas;

    private Coroutine mapRegistrationRoutine;

    private void Awake()
    {
        // Scene의 다른 전투 컴포넌트가 Awake에서 참조를 요청하기 전에 고정 연결을 먼저 구성한다.
        ConfigureDataPool();
        EnsureEnemyTargetSync();
    }

    private void OnEnable()
    {
        // Player와 Enemy는 Installer보다 늦게 생성되므로 생성 이벤트를 통해 Registry에 등록한다.
        SubscribeRuntimeEvents();
    }

    private void Start()
    {
        // 현재 Awake와 초기화가 중복된다. 기존 Scene 실행 순서 호환을 위해 남아 있으며 최종적으로 한 경로로 통합한다.
        ConfigureDataPool();
        EnsureEnemyTargetSync();
        SubscribeRuntimeEvents();
        mapRegistrationRoutine = StartCoroutine(RegisterGeneratedMap());

        if (battleGameManager != null && battleGameManager.CurrentPlayer != null)
        {
            HandlePlayerRegistered(battleGameManager.CurrentPlayer);
        }
    }

    private void OnDisable()
    {
        if (battleGameManager != null)
        {
            battleGameManager.PlayerRegistered -= HandlePlayerRegistered;
        }

        if (enemySpawner != null)
        {
            enemySpawner.EnemySpawned -= HandleEnemySpawned;
        }

        if (mapRegistrationRoutine != null)
        {
            StopCoroutine(mapRegistrationRoutine);
            mapRegistrationRoutine = null;
        }
    }

    private void ConfigureDataPool()
    {
        dataPool?.ConfigureSceneReferences(
            playerBody,
            mapGenerator,
            battleCamera,
            playerSelectCanvas,
            battleCanvas,
            unitRegistry,
            mapRegistry);
    }

    /// <summary>
    /// Player와 Enemy 중 어느 쪽이 먼저 등록돼도 Enemy가 현재 Player Target을 받도록 BattleEnemyTargetSync를 UnitRegistry에 연결한다.
    /// 현재 누락 컴포넌트를 런타임 생성하지만 최종 구조에서는 Inspector 직접 참조를 필수로 한다.
    /// </summary>
    private void EnsureEnemyTargetSync()
    {
        if (enemyTargetSync == null)
        {
            enemyTargetSync = GetComponent<BattleEnemyTargetSync>();
        }

        if (enemyTargetSync == null)
        {
            enemyTargetSync = gameObject.AddComponent<BattleEnemyTargetSync>();
        }

        enemyTargetSync.Configure(unitRegistry);
    }

    /// <summary>
    /// BattleGameManager의 Player 생성과 EnemySpawner의 Enemy 생성 이벤트를 중복 없이 구독한다.
    /// -= 후 += 순서는 OnEnable과 Start가 모두 호출돼도 같은 handler가 두 번 등록되는 것을 막는다.
    /// </summary>
    private void SubscribeRuntimeEvents()
    {
        if (battleGameManager != null)
        {
            battleGameManager.PlayerRegistered -= HandlePlayerRegistered;
            battleGameManager.PlayerRegistered += HandlePlayerRegistered;
        }

        if (enemySpawner != null)
        {
            enemySpawner.EnemySpawned -= HandleEnemySpawned;
            enemySpawner.EnemySpawned += HandleEnemySpawned;
        }
    }

    /// <summary>
    /// MapGenerator 완료를 기다린 뒤 생성된 MapInfo를 MapRegistry에 최초 한 번 등록한다.
    /// 현재 Scene 전체 검색을 사용하며 추후 MapGenerator가 생성 결과 목록을 직접 제공하도록 변경한다.
    /// </summary>
    private IEnumerator RegisterGeneratedMap()
    {
        if (mapGenerator == null && renewMapGenerator == null)
        {
            yield break;
        }

        // Scene에 실제로 연결된 생성기 하나의 완료만 기다린다. 구형과 신규 생성기를 동시에
        // 시작하지 않으므로, 연결되지 않았거나 사용하지 않는 쪽 때문에 등록이 영구 대기하지 않는다.
        while ((mapGenerator != null && !mapGenerator.IsGenerateEnd()) ||
               (renewMapGenerator != null && !renewMapGenerator.IsGenerateEnd()))
        {
            yield return null;
        }

        MapInfo[] tiles = FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
        mapRegistry?.RegisterTiles(tiles);
        mapRegistrationRoutine = null;
    }

    /// <summary>생성된 Player를 임시 DataPool과 공식 UnitRegistry에 전달한다.</summary>
    private void HandlePlayerRegistered(GameObject player)
    {
        dataPool?.RegisterPlayer(player);
        unitRegistry?.RegisterPlayer(player);
    }

    /// <summary>EnemySpawner가 생성한 Enemy를 생성 순서 그대로 UnitRegistry에 등록한다.</summary>
    private void HandleEnemySpawned(GameObject enemy)
    {
        unitRegistry?.RegisterEnemy(enemy);
    }
}
