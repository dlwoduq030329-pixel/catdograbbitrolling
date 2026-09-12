using System.Collections.Generic;
using UnityEngine;

/// <summary>기존 맵을 읽어 Stage별 적과 NPC 임시 오브젝트를 배치한다. 맵·Shop·Chest 생성 및 클리어·보상·전환은 담당하지 않는다.</summary>
[DefaultExecutionOrder(-100)]
public sealed class StageSpawner : MonoBehaviour
{
    [SerializeField, Tooltip("Stage 1~4 배치 데이터. stageNumber로 찾으므로 배열 순서와 무관합니다.")]
    private StageObjData[] stages;
    [SerializeField, Min(1), Tooltip("이번 실행에서 시작할 Stage 번호입니다. 전환 관리자가 생기면 TrySelectStage로 지정합니다.")]
    private int initialStageNumber = 1;
    [SerializeField] private NewMapGenerator mapGenerator;
    [SerializeField] private EnemySpawner enemySpawner;
    [Header("NPC 임시 배치")]
    [Tooltip("NPC 타일에 무작위로 배치할 NPC 원본 데이터베이스입니다. Enemy와 달리 Stage별로 다르게 구성할 필요가 없어 StageObjData를 거치지 않고 여기서 직접 참조합니다.")]
    [SerializeField] private NpcDatabase npcDatabase;
    [Tooltip("비워두거나 NpcDatabase가 비어 있으면 클릭 가능한 Cube를 임시 NPC로 생성합니다.")]
    [SerializeField] private GameObject npcPlaceholderPrefab;
    [Tooltip("타일 표면 위에 NPC를 얼마나 더 띄울지(추가 여유값). 0이면 표면에 딱 붙습니다.")]
    [SerializeField, Min(0f)] private float npcHeight = 1f;
    [Tooltip("생성된 NPC 프리팹의 원본 Scale에 곱할 값입니다. Player 몸체 프리팹을 임시로 쓸 때 SpawnPlayer의 spawnedPlayerScaleMultiplier와 같은 값으로 맞추면 Player와 같은 크기로 보입니다.")]
    [SerializeField, Min(0.01f)] private float npcScaleMultiplier = 1f;
    private readonly List<BattleEnemyData> roster = new List<BattleEnemyData>();
    private readonly List<GameObject> spawnedNpcs = new List<GameObject>();
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

    /// <summary>이번 Stage에서 생성한 적과 NPC 임시 오브젝트를 전부 정리한다.</summary>
    public void ReleaseStageEnemiesAndNpcs()
    {
        enemySpawner.ReleaseAll();
        foreach (GameObject npc in spawnedNpcs)
        {
            if (npc == null)
                continue;

            // NPC를 지우기 전에 타일이 들고 있던 참조도 같이 비워야 다음 Stage에서
            // 그 타일을 검사할 때 이미 파괴된 NPC를 가리키는 참조가 남지 않는다.
            BattleNpcInteractionTrigger trigger = npc.GetComponent<BattleNpcInteractionTrigger>();
            if (trigger != null && trigger.Tile != null)
                trigger.Tile.NpcTrigger = null;

            Destroy(npc);
        }
        spawnedNpcs.Clear();
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
        SpawnNpcPlaceholders(tiles);
        spawned = true;
        return true;
    }

    private void SpawnNpcPlaceholders(List<MapInfo> tiles)
    {
        foreach (MapInfo tile in tiles)
        {
            if (tile == null || tile.Type != TileType.NPC)
                continue;

            // NpcDatabase에서 무작위로 하나 고르고, 그 NpcData의 prefab을 우선 사용한다.
            // DB가 없거나 비어 있거나(GetRandom이 null) NpcData에 prefab이 비어 있으면
            // 기존처럼 npcPlaceholderPrefab(또는 Cube)로 대체한다.
            NpcData npcData = npcDatabase != null ? npcDatabase.GetRandom() : null;
            GameObject prefabToSpawn = npcData != null && npcData.prefab != null
                ? npcData.prefab
                : npcPlaceholderPrefab;

            GameObject npc = prefabToSpawn != null
                ? Instantiate(prefabToSpawn)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            npc.name = $"NPC_Placeholder_{tile.Index.x}_{tile.Index.y}";
            npc.transform.position = tile.transform.position;

            // NPC는 아직 대사·전투 애니메이션이 없는 정지 상태 placeholder다(work log 기준 미구현).
            // Player 몸체 프리팹을 그대로 쓰면 Animator가 Play 중 매 프레임 자세를 갱신하면서
            // (Root Motion·리타게팅 등) 우리가 스폰 시점에 딱 한 번 맞춘 Scale·위치를 계속 되돌릴 수
            // 있다 — 스폰 코드 순서를 아무리 바꿔도 못 잡는 문제라 아예 꺼서 원천 차단한다.
            // 끄기 전에 Update(0f)를 한 번 불러서 정상 자세(바인드 포즈가 아니라 실제 애니메이션
            // 기본 자세)로 SkinnedMeshRenderer를 갱신해둔다 — 그래야 바로 다음의 발 위치 계산이
            // 올바른 bounds를 기준으로 이루어진다. 한 번도 갱신 안 한 채로 끄면 자세가 이상한
            // 상태로 굳어버려서(예: 바인드 포즈) 발 위치가 어긋난다.
            foreach (Animator animator in npc.GetComponentsInChildren<Animator>(true))
            {
                animator.Update(0f);
                animator.enabled = false;
            }

            // 타일의 자식으로 넣지 않는다 — SetParent(worldPositionStays: true)는 타일 Transform에
            // 회전이나 비균일 Scale이 조금이라도 있으면 자식 오브젝트의 Scale을 다시 계산하면서
            // 틀어뜨린다(순서를 바꿔가며 시도해봤지만 계속 깨짐). 대신 MapInfo.NpcTrigger 참조 하나로
            // "이 타일 위에 이 NPC가 있다"만 기록한다 — 별도 조회 테이블 없이 도착 타일에서 바로 꺼내
            // 쓸 수 있는 건 동일하다.
            // Player 스폰(SpawnPlayer.PlayerPosInit)과 같은 이유로 배율을 곱한다 — 원본 프리팹
            // Scale을 그대로 쓰면 Player 몸체 프리팹을 NPC로 쓸 때 Player보다 훨씬 크게 보인다.
            // Enemy 스폰과 같은 이유: pivot 위치가 프리팹마다 달라서(Cube는 중심, 다른 모델은 발밑 등)
            // 타일 pivot 기준 고정 오프셋만으로는 잔디 장식 등 실제 타일 표면 높이와 안 맞아 파묻히거나
            // 겹친다. npcHeight만큼 타일 위에 띄운다.
            EnemySpawnGeometry.FitScaleAndAlign(npc, tile.transform, npcScaleMultiplier, npcHeight);

            // 임시로 Player 몸체 프리팹을 NPC로 쓰는 경우 Rigidbody(물리 이동용)가 같이 붙어 있다.
            // NPC는 가만히 서 있기만 해야 하는데 이걸 그대로 두면 주변 Collider(타일·장식물)와
            // 부딪히며 물리 엔진이 매 프레임 위치를 밀어내 스폰 위치가 흔들린다. Kinematic으로
            // 바꿔서 물리 영향을 끊는다.
            Rigidbody npcRigidbody = npc.GetComponent<Rigidbody>();
            if (npcRigidbody != null)
                npcRigidbody.isKinematic = true;

            BattleNpcInteractionTrigger interactionTrigger = npc.GetComponent<BattleNpcInteractionTrigger>();
            if (interactionTrigger == null)
                interactionTrigger = npc.AddComponent<BattleNpcInteractionTrigger>();
            interactionTrigger.SetTile(tile);
            interactionTrigger.SetData(npcData);
            tile.NpcTrigger = interactionTrigger;
            spawnedNpcs.Add(npc);
        }
    }
}
