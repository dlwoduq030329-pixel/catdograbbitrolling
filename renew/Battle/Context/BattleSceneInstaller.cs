using System.Collections;
using UnityEngine;

/// <summary>
/// Moon Scene의 고정 참조와 런타임 생성 참조를 BattleDataPool 및 Registry에 등록한다.
/// 기존 전투 시스템을 변경하지 않고 병행 등록하는 모듈화 전환용 구성 요소다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleSceneInstaller : MonoBehaviour
{
    [Header("저장소")]
    [SerializeField] private BattleDataPool dataPool;
    [SerializeField] private BattleUnitRegistry unitRegistry;
    [SerializeField] private BattleMapRegistry mapRegistry;
    [SerializeField] private BattleEnemyTargetBroker enemyTargetBroker;

    [Header("현재 Scene 시스템")]
    [SerializeField] private BattleGameManager battleGameManager;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private GameObject playerBody;
    [SerializeField] private Camera battleCamera;
    [SerializeField] private GameObject playerSelectCanvas;
    [SerializeField] private GameObject battleCanvas;

    private Coroutine mapRegistrationRoutine;

    private void Awake()
    {
        ConfigureDataPool();
        EnsureEnemyTargetBroker();
    }

    private void OnEnable()
    {
        SubscribeRuntimeEvents();
    }

    private void Start()
    {
        ConfigureDataPool();
        EnsureEnemyTargetBroker();
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

    /// <summary>Player와 Enemy 등록 순서에 무관하게 Target을 배포하는 전용 Broker를 구성한다.</summary>
    private void EnsureEnemyTargetBroker()
    {
        if (enemyTargetBroker == null)
        {
            enemyTargetBroker = GetComponent<BattleEnemyTargetBroker>();
        }

        if (enemyTargetBroker == null)
        {
            enemyTargetBroker = gameObject.AddComponent<BattleEnemyTargetBroker>();
        }

        enemyTargetBroker.Configure(unitRegistry);
    }

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

    private IEnumerator RegisterGeneratedMap()
    {
        if (mapGenerator == null)
        {
            yield break;
        }

        while (!mapGenerator.IsGenerateEnd())
        {
            yield return null;
        }

        MapInfo[] tiles = FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
        mapRegistry?.RegisterTiles(tiles);
        mapRegistrationRoutine = null;
    }

    private void HandlePlayerRegistered(GameObject player)
    {
        dataPool?.RegisterPlayer(player);
        unitRegistry?.RegisterPlayer(player);
    }

    private void HandleEnemySpawned(GameObject enemy)
    {
        unitRegistry?.RegisterEnemy(enemy);
    }
}
