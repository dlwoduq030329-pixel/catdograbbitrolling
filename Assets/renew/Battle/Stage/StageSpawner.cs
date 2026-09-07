using System.Collections.Generic;
using UnityEngine;

/// <summary>기존 맵을 읽어 Stage별 적만 배치한다. 맵·Shop·Chest 생성 및 클리어·보상·전환은 담당하지 않는다.</summary>
[DefaultExecutionOrder(-100)]
public sealed class StageSpawner : MonoBehaviour
{
    [SerializeField, Tooltip("Stage 1~4 배치 데이터. stageNumber로 찾으므로 배열 순서와 무관합니다.")]
    private StageObjData[] stages;
    [SerializeField, Min(1), Tooltip("이번 실행에서 시작할 Stage 번호입니다. 전환 관리자가 생기면 TrySelectStage로 지정합니다.")]
    private int initialStageNumber = 1;
    [SerializeField] private NewMapGenerator mapGenerator;
    [SerializeField] private EnemySpawner enemySpawner;
    private readonly List<BattleEnemyData> roster = new List<BattleEnemyData>();
    private bool spawned;
    public StageObjData CurrentStage { get; private set; }

    private void Awake() { TrySelectStage(initialStageNumber); }

    public bool HasStage(int number)
    {
        if (stages == null) return false;
        foreach (StageObjData stage in stages)
            if (stage != null && stage.stageNumber == number) return true;
        return false;
    }

    public bool CanSelectStage(int number)
    {
        StageObjData selected = null;
        if (stages == null || mapGenerator == null || enemySpawner == null) return false;
        foreach (StageObjData stage in stages)
            if (stage != null && stage.stageNumber == number)
            {
                if (selected != null)
                { Debug.LogWarning($"Stage {number} 번호가 중복되어 전환하지 않습니다.", this); return false; }
                selected = stage;
            }
        if (selected == null || selected.enemyDatabase == null)
        { Debug.LogWarning($"Stage {number} 데이터 또는 DB가 없어 전환하지 않습니다.", this); return false; }
        return true;
    }

    public void ReleaseStageEnemies()
    {
        enemySpawner.ReleaseAll();
        spawned = false;
        roster.Clear();
    }

    public void CollectMapTiles(List<MapInfo> tiles)
    {
        tiles.Clear();
        if (mapGenerator == null) return;
        for (int x = 0; x < mapGenerator.GetMapSizeX(); x++)
            for (int z = 0; z < mapGenerator.GetMapSizeZ(); z++)
            {
                MapInfo tile = mapGenerator.GetMapInfo(new Vector2Int(x, z));
                if (tile != null) tiles.Add(tile);
            }
    }

    /// <summary>적 배치 전에 호출한다. 원본 데이터는 읽기만 하며 생성 목록은 이번 Stage 동안 유지한다.</summary>
    public bool TrySelectStage(int stageNumber)
    {
        if (mapGenerator == null || enemySpawner == null || stages == null)
        { Debug.LogError("StageSpawner의 맵·하위 스포너·Stage 데이터 참조가 필요합니다.", this); return false; }
        if (spawned)
        { Debug.LogError("Stage 변경 전에 기존 맵과 유닛을 정리해야 합니다.", this); return false; }
        StageObjData selected = null;
        foreach (StageObjData data in stages)
            if (data != null && data.stageNumber == stageNumber)
            {
                if (selected != null) { Debug.LogError("Stage 번호가 중복되었습니다.", this); return false; }
                selected = data;
            }
        if (selected == null) { Debug.LogError($"Stage {stageNumber} 데이터가 없습니다.", this); return false; }
        if (selected.enemyDatabase == null)
        { Debug.LogWarning("Stage에 Enemy DB가 연결되지 않았습니다.", this); return false; }
        roster.Clear();
        CurrentStage = selected;
        return true;
    }

    /// <summary>맵 생성과 Player 등록 뒤 UIFlow가 한 번 호출한다. EnemySpawner의 기존 생성 이벤트는 그대로 유지한다.</summary>
    public bool TrySpawnStageEnemies(Transform player)
    {
        if (spawned) return true;
        if (CurrentStage == null && !TrySelectStage(initialStageNumber)) return false;
        if (CurrentStage == null || player == null || !mapGenerator.IsGenerateEnd())
        { Debug.LogError("Stage 배치 실패: Stage·맵·Player가 준비되지 않았습니다.", this); return false; }
        var tiles = new List<MapInfo>();
        CollectMapTiles(tiles);
        MapInfo start = BattleTileLocator.FindClosestXZ(player.position, tiles);
        if (start == null) { Debug.LogError("Stage 시작 타일을 찾지 못했습니다.", this); return false; }
        enemySpawner.PreparePool(CurrentStage.enemyDatabase, start.transform);
        if (!StageEnemyBridge.TryBuild(CurrentStage, roster, out string warning, enemySpawner.AvailableCount))
        { Debug.LogWarning(warning, this); return false; }
        if (!string.IsNullOrEmpty(warning)) Debug.LogWarning(warning, this);
        var candidates = new List<MapInfo>();
        foreach (MapInfo tile in tiles)
            if (tile != null && tile.Type == TileType.Road && tile.IsWalkable &&
                !StagePlacement.IsInsideSquare(tile.Index, start.Index, CurrentStage.safeRadiusTiles))
                candidates.Add(tile);
        if (candidates.Count < roster.Count)
            Debug.LogWarning($"Stage 빈 타일 부족: 적 {roster.Count}마리 중 {candidates.Count}마리까지만 배치합니다.", this);
        if (enemySpawner.SpawnedEnemies.Count > 0)
        { Debug.LogError("이미 생성된 적이 있습니다. 기존 배치와 Stage 배치를 중복 호출하지 마세요.", this); return false; }
        StagePlacement.Shuffle(candidates);
        for (int i = 0; i < Mathf.Min(roster.Count, candidates.Count); i++)
            enemySpawner.SpawnEnemy(candidates[i].transform, roster[i]);
        spawned = true;
        return true;
    }
}
