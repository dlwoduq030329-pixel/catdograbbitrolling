using System.Collections.Generic;
using UnityEngine;

public class NewMapGenerator : MonoBehaviour
{
    [Tooltip("새로운 맵 생성기")]
    [Header("코드 설명용 변수. 마우스를 올려 확인.")]
    [SerializeField]
    bool CODE_EXPLAIN;

    [Header("블록간의 거리")]
    [SerializeField]
    float blockDistance;

    [Header("생성할 육지 블록 개수")]
    [Tooltip("실제로 생성되는 대륙의 블록 개수")]
    [SerializeField]
    private int landBlockCount = 300;

    [Header("대륙 크기 제한")]
    [Tooltip("대륙이 들어갈 내부 배열의 여유 공간")]
    [SerializeField]
    private int mapPadding = 5;

    // 내부적으로 사용하는 맵 크기
    private int blockCountX;
    private int blockCountZ;

    private MapInfo[,] blocks;

    [Header("prefabs")]
    [SerializeField] private GameObject roadPrefab;
    [SerializeField] private GameObject disMoveablePrefab;
    [SerializeField] private GameObject riverPrefab;
    [SerializeField] private GameObject storePrefab;
    [SerializeField] private GameObject boxPrefab;
    [SerializeField] private GameObject exitPrefab;

    [Header("장애물 설정")]
    [SerializeField] private int disMoveableCount = 40;

    [Header("상점 설정")]
    [SerializeField] private int storeCount = 3;

    [Header("상자 설정")]
    [SerializeField] private int boxCount = 10;

    [SerializeField]
    GameObject playerBody;

    private TileType[,] mapblueprint;

    private Vector2Int startPos;
    private Vector2Int exitPos;

    private bool generatorEnd = false;

    [Header("Terrain 설정")]
    [SerializeField] private Terrain terrainPrefab;

    [SerializeField] private int terrainHeightmapResolution = 513;
    [SerializeField] private float terrainHeight = 30f;

    [Header("Terrain Texture")]
    [SerializeField] private TerrainLayer grassLayer;
    [SerializeField] private TerrainLayer dirtLayer;
    [SerializeField] private TerrainLayer rockLayer;
    [SerializeField] private TerrainLayer sandLayer;

    [Header("Terrain Noise")]
    [SerializeField] private float terrainNoiseScale = 0.03f;
    [SerializeField] private float terrainNoiseHeight = 15f;

    private Terrain generatedTerrain;

    [Header("Terrain")]
    [SerializeField] private Material terrainMaterial;

    [Header("Cloud 설정")]
    [SerializeField] private GameObject fogCloudPrefab;

    [SerializeField] private int fogDistance = 2;

    [SerializeField] private float fogHeight = 3f;

    [SerializeField] private float fogScaleMin = 0.9f;
    [SerializeField] private float fogScaleMax = 1.3f;
    [SerializeField] int cloudInterval = 2;

    private Transform cloudParent;
    [Header("River Terrain")]
    [SerializeField]
    private Terrain riverTerrainPrefab;

    [SerializeField]
    private Material riverTerrainMaterial;

    [SerializeField]
    private int riverTerrainHeightmapResolution = 513;

    [SerializeField]
    private float riverTerrainDepth = 8f;

    [SerializeField]
    private float riverTerrainSurfaceY = 0f;

    [SerializeField]
    private float riverTerrainEdgeHeight = -0.1f;

    private Terrain generatedRiverTerrain;

    private void Awake()
    {
        CalculateMapSize();

        mapblueprint = new TileType[blockCountX, blockCountZ];
        blocks = new MapInfo[blockCountX, blockCountZ];
    }


    private void Start()
    {
        Debug.Log("야옹~");
    }


    private void Update()
    {

    }


    // =========================================================
    // 맵 생성 시작
    // =========================================================

    public void StartGenerator()
    {
        bool success = false;

        generatorEnd = false;

        int tryCount = 0;

        while (!success)
        {
            tryCount++;

            ClearMap();

            // 대륙 생성
            GenerateRoad();

            // 시작점
            GenerateStart();

            // 강
            GenerateRiver();

            // 장애물
            GenerateDisMoveable();

            // 상점
            GenerateStore();

            // 상자
            GenerateBox();

            // 출구
            GenerateExit();

            // 시작 -> 출구 연결 확인
            success = CheckPath();

            // 혹시 극단적인 경우 무한루프 방지
            if (tryCount > 100)
            {
                Debug.LogWarning("맵 생성 시도가 100회를 초과했습니다.");
                break;
            }
        }

        DrawGrid();
        GenerateRiverTerrain();
        //GenerateFogCloud();
        // GenerateTerrain();

        Debug.Log(
            $"맵 생성 완료 / 대륙 블록 : {landBlockCount} / 시도 횟수 : {tryCount}"
        );
    }

    private void GenerateRiverTerrain()
    {
        // 기존 River Terrain 제거
        if (generatedRiverTerrain != null)
        {
            Destroy(
                generatedRiverTerrain.gameObject
            );
        }


        TerrainData terrainData =
            new TerrainData();


        terrainData.heightmapResolution =
            riverTerrainHeightmapResolution;


        // 전체 맵 크기와 동일하게 생성
        float terrainWidth =
            blockCountX * blockDistance +
    blockDistance + ((blockDistance * 3f)*2);

        float terrainLength =
            blockCountZ * blockDistance +
    blockDistance + ((blockDistance * 3f) * 2);


        terrainData.size =
            new Vector3(
                terrainWidth,
                riverTerrainDepth,
                terrainLength
            );


        // River Heightmap 생성
        GenerateRiverTerrainHeight(
            terrainData
        );


        // Terrain 생성
        GameObject terrainObject =
            Terrain.CreateTerrainGameObject(
                terrainData
            );


        terrainObject.name =
            "Generated River Terrain";


        terrainObject.transform.SetParent(
            transform
        );


        // 기존 Grid 좌표와 맞춤
        terrainObject.transform.position =
            new Vector3(
        -blockDistance * 0.5f,
        riverTerrainSurfaceY -
        riverTerrainDepth,
        -blockDistance * 0.5f
    );


        generatedRiverTerrain =
            terrainObject.GetComponent<Terrain>();


        // Material 적용
        if (riverTerrainMaterial != null)
        {
            generatedRiverTerrain.materialTemplate =
                riverTerrainMaterial;
        }


        Debug.Log(
            "River Terrain 생성 완료"
        );
    }

    private void GenerateRiverTerrainHeight(
       TerrainData terrainData)
    {
        int resolution =
            terrainData.heightmapResolution;


        float[,] heights =
            new float[
                resolution,
                resolution
            ];


        // =====================================================
        // 맵 외부 울퉁불퉁한 Terrain 설정
        // =====================================================

        float noiseScale = 0.08f;

        float noiseHeight = 0.35f;


        // 매번 같은 랜덤 지형이 아니라
        // 생성할 때마다 다른 형태
        float randomOffsetX =
            Random.Range(
                0f,
                10000f
            );

        float randomOffsetZ =
            Random.Range(
                0f,
                10000f
            );


        // =====================================================
        // Heightmap 전체 순회
        // =====================================================

        for (int z = 0;
             z < resolution;
             z++)
        {
            for (int x = 0;
                 x < resolution;
                 x++)
            {
                float normalizedX =
                    (float)x /
                    (resolution - 1);

                float normalizedZ =
                    (float)z /
                    (resolution - 1);


                // =================================================
                // Terrain 내부 좌표
                // =================================================

                float terrainX =
                    normalizedX *
                    terrainData.size.x;

                float terrainZ =
                    normalizedZ *
                    terrainData.size.z;


                // =================================================
                // Terrain 위치가
                // -blockDistance * 0.5f 로 이동했으므로
                // 실제 Grid 좌표로 변환
                // =================================================

                float worldX =
                    terrainX -
                    blockDistance * 0.5f;

                float worldZ =
                    terrainZ -
                    blockDistance * 0.5f;


                int mapX =
                    Mathf.RoundToInt(
                        worldX /
                        blockDistance
                    );

                int mapZ =
                    Mathf.RoundToInt(
                        worldZ /
                        blockDistance
                    );


                // =================================================
                // 맵 배열 내부인지 확인
                // =================================================

                bool isInsideMap =
                    mapX >= 0 &&
                    mapX < blockCountX &&
                    mapZ >= 0 &&
                    mapZ < blockCountZ;


                // =================================================
                // 맵 외부
                //
                // 실제 배열 범위 밖도 포함해서
                // 울퉁불퉁하게 생성
                // =================================================

                if (!isInsideMap)
                {
                    float noise =
                        Mathf.PerlinNoise(
                            terrainX * noiseScale +
                            randomOffsetX,

                            terrainZ * noiseScale +
                            randomOffsetZ
                        );


                    heights[z, x] =
                        Mathf.Lerp(
                            0.1f,
                            1f,
                            noise
                        );

                    continue;
                }


                TileType currentTile =
                    mapblueprint[
                        mapX,
                        mapZ
                    ];


                // =================================================
                // 맵 내부지만 실제 생성되지 않은 공간
                //
                // Empty도 맵 외부 지형처럼 처리
                // =================================================

                if (currentTile == TileType.Empty)
                {
                    float noise =
                        Mathf.PerlinNoise(
                            terrainX * noiseScale +
                            randomOffsetX,

                            terrainZ * noiseScale +
                            randomOffsetZ
                        );


                    heights[z, x] =
                        Mathf.Lerp(
                            0.3f,
                            1f,
                            noise
                        );

                    continue;
                }


                // =================================================
                // River
                // =================================================

                if (currentTile == TileType.River)
                {
                    float minDistance =
                        float.MaxValue;


                    for (int searchZ = 0;
                         searchZ < blockCountZ;
                         searchZ++)
                    {
                        for (int searchX = 0;
                             searchX < blockCountX;
                             searchX++)
                        {
                            if (mapblueprint[
                                searchX,
                                searchZ
                            ] == TileType.River)
                            {
                                continue;
                            }


                            float targetX =
                                searchX *
                                blockDistance;

                            float targetZ =
                                searchZ *
                                blockDistance;


                            float distance =
                                Vector2.Distance(
                                    new Vector2(
                                        worldX,
                                        worldZ
                                    ),
                                    new Vector2(
                                        targetX,
                                        targetZ
                                    )
                            );


                            minDistance =
                                Mathf.Min(
                                    minDistance,
                                    distance
                                );
                        }
                    }


                    float maxDepthDistance =
                        blockDistance * 2f;


                    float depth01 =
                        Mathf.Clamp01(
                            minDistance /
                            maxDepthDistance
                        );


                    depth01 =
                        depth01 *
                        depth01 *
                        (3f - 2f * depth01);


                    heights[z, x] =
                        1f -
                        depth01;

                    continue;
                }


                // =================================================
                // 실제 맵 타일
                //
                // Road / Store / Box / Exit 등
                // 기존처럼 평평하게 유지
                // =================================================

                heights[z, x] = 0.8f;
            }
        }


        terrainData.SetHeights(
            0,
            0,
            heights
        );
    }
    private void GenerateFogCloud()
    {
        if (fogCloudPrefab == null)
        {
            Debug.LogWarning("Fog Cloud Prefab이 설정되지 않았습니다.");
            return;
        }

        // 외곽선을 따라 몇 타일마다 구름을 하나씩 생성할지

        HashSet<Vector2Int> landPositions =
            new HashSet<Vector2Int>();

        // ---------------------------------------------------------
        // 1. 육지 위치 수집
        // ---------------------------------------------------------

        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                if (mapblueprint[x, z] != TileType.Empty)
                {
                    landPositions.Add(
                        new Vector2Int(x, z)
                    );
                }
            }
        }

        if (landPositions.Count == 0)
            return;


        // ---------------------------------------------------------
        // 2. 육지 외곽 타일 찾기
        // ---------------------------------------------------------

        List<Vector2Int> edgePositions =
            new List<Vector2Int>();

        Vector2Int[] directions =
        {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };


        foreach (Vector2Int land in landPositions)
        {
            foreach (Vector2Int dir in directions)
            {
                if (!landPositions.Contains(land + dir))
                {
                    edgePositions.Add(land);
                    break;
                }
            }
        }


        // ---------------------------------------------------------
        // 3. 외곽선을 따라 일정 간격으로 Fog 생성
        // ---------------------------------------------------------

        HashSet<Vector2Int> fogPositions =
            new HashSet<Vector2Int>();


        for (int i = 0; i < edgePositions.Count; i += cloudInterval)
        {
            Vector2Int edge =
                edgePositions[i];


            // 이 외곽 타일에서 바깥 방향 찾기
            foreach (Vector2Int dir in directions)
            {
                Vector2Int target =
                    edge + dir;


                // 육지라면 안쪽 방향이므로 무시
                if (landPositions.Contains(target))
                    continue;


                // 구름 위치
                fogPositions.Add(target);

                break;
            }
        }


        // ---------------------------------------------------------
        // 4. Fog Cloud 생성
        // ---------------------------------------------------------

        foreach (Vector2Int pos in fogPositions)
        {
            Vector3 worldPos =
                new Vector3(
                    pos.x * blockDistance,
                    fogHeight,
                    pos.y * blockDistance
                );


            GameObject cloud =
                Instantiate(
                    fogCloudPrefab,
                    worldPos,
                    Quaternion.identity,
                    transform
                );


            float scale =
                Random.Range(
                    fogScaleMin,
                    fogScaleMax
                );


            cloud.transform.localScale =
                Vector3.one * scale;


            cloud.transform.rotation =
                Quaternion.Euler(
                    0f,
                    Random.Range(0f, 360f),
                    0f
                );
        }


        Debug.Log(
            $"Fog Cloud 생성 완료 / 개수 : {fogPositions.Count}"
        );
    }    // =========================================================
    // 내부 맵 크기 계산
    // =========================================================

    private void CalculateMapSize()
    {
        // landBlockCount를 기준으로 대략적인 맵 크기 계산
        int size = Mathf.CeilToInt(Mathf.Sqrt(landBlockCount));

        size += mapPadding;

        blockCountX = size;
        blockCountZ = size;
    }


    // =========================================================
    // 경로 검사
    // =========================================================

    private bool CheckPath()
    {
        // 시작점과 출구가 이동 가능한지 먼저 확인
        if (!IsWalkable(mapblueprint[startPos.x, startPos.y]))
            return false;

        if (!IsWalkable(mapblueprint[exitPos.x, exitPos.y]))
            return false;


        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        bool[,] visited =
            new bool[blockCountX, blockCountZ];


        queue.Enqueue(startPos);

        visited[startPos.x, startPos.y] = true;


        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };


        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();


            // 출구 도착
            if (current == exitPos)
                return true;


            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;


                // 맵 밖
                if (!IsInsideMap(next))
                    continue;


                // 이미 방문
                if (visited[next.x, next.y])
                    continue;


                // 이동 불가능
                if (!IsWalkable(mapblueprint[next.x, next.y]))
                    continue;


                visited[next.x, next.y] = true;

                queue.Enqueue(next);
            }
        }


        return false;
    }


    private bool IsWalkable(TileType type)
    {
        switch (type)
        {
            case TileType.Start:
            case TileType.Road:
            case TileType.Store:
            case TileType.Box:
            case TileType.Exit:

                return true;


            default:

                return false;
        }
    }


    // =========================================================
    // 맵 초기화
    // =========================================================

    private void ClearMap()
    {
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                mapblueprint[x, z] = TileType.Empty;

                blocks[x, z] = null;
            }
        }
    }


    // =========================================================
    // 대륙 생성
    // =========================================================

    private void GenerateRoad()
    {
        GenerateLand();
    }


    private void GenerateLand()
    {
        HashSet<Vector2Int> landPositions =
            new HashSet<Vector2Int>();


        // 대륙 시작 위치
        Vector2Int start = new Vector2Int(
            blockCountX / 2,
            blockCountZ / 2
        );


        landPositions.Add(start);


        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };


        // 육지가 landBlockCount가 될 때까지 성장
        while (landPositions.Count < landBlockCount)
        {
            Vector2Int[] currentLand =
                new Vector2Int[landPositions.Count];


            landPositions.CopyTo(currentLand);


            // 기존 육지 중 하나 선택
            Vector2Int basePos =
                currentLand[
                    Random.Range(0, currentLand.Length)
                ];


            // 랜덤 방향
            Vector2Int direction =
                directions[
                    Random.Range(0, directions.Length)
                ];


            Vector2Int next =
                basePos + direction;


            // 내부 맵 밖
            if (!IsInsideMap(next))
                continue;


            // 이미 육지
            if (landPositions.Contains(next))
                continue;


            landPositions.Add(next);
        }


        // 실제 맵에 적용
        foreach (Vector2Int pos in landPositions)
        {
            mapblueprint[pos.x, pos.y] =
                TileType.Road;
        }
    }


    // =========================================================
    // 시작 위치
    // =========================================================

    private void GenerateStart()
    {
        List<Vector2Int> candidates =
            new List<Vector2Int>();


        int minX = blockCountX;


        // 가장 왼쪽 육지 찾기
        for (int x = 0; x < blockCountX; x++)
        {
            bool found = false;


            for (int z = 0; z < blockCountZ; z++)
            {
                if (mapblueprint[x, z] ==
                    TileType.Road)
                {
                    minX = x;

                    found = true;

                    break;
                }
            }


            if (found)
                break;
        }


        // 가장 왼쪽 육지 중 하나 선택
        for (int z = 0; z < blockCountZ; z++)
        {
            if (mapblueprint[minX, z] ==
                TileType.Road)
            {
                candidates.Add(
                    new Vector2Int(minX, z)
                );
            }
        }


        if (candidates.Count == 0)
        {
            Debug.LogError("시작 위치를 찾을 수 없습니다.");
            return;
        }


        startPos =
            candidates[
                Random.Range(0, candidates.Count)
            ];


        mapblueprint[startPos.x, startPos.y] =
            TileType.Start;
    }


    // =========================================================
    // 출구 위치
    // =========================================================

    private void GenerateExit()
    {
        List<Vector2Int> candidates =
            new List<Vector2Int>();


        int maxX = -1;


        // 가장 오른쪽 육지 찾기
        for (int x = blockCountX - 1; x >= 0; x--)
        {
            bool found = false;


            for (int z = 0; z < blockCountZ; z++)
            {
                if (mapblueprint[x, z] ==
                    TileType.Road)
                {
                    maxX = x;

                    found = true;

                    break;
                }
            }


            if (found)
                break;
        }


        // 가장 오른쪽 육지 중 하나 선택
        for (int z = 0; z < blockCountZ; z++)
        {
            if (mapblueprint[maxX, z] ==
                TileType.Road)
            {
                candidates.Add(
                    new Vector2Int(maxX, z)
                );
            }
        }


        if (candidates.Count == 0)
        {
            Debug.LogError("출구 위치를 찾을 수 없습니다.");
            return;
        }


        exitPos =
            candidates[
                Random.Range(0, candidates.Count)
            ];


        mapblueprint[exitPos.x, exitPos.y] =
            TileType.Exit;
    }


    // =========================================================
    // 강 생성
    // =========================================================

    private void GenerateRiver()
    {
        // 기존처럼 1~2개의 강
        int riverCount =
            Random.Range(2, 5);


        for (int i = 0; i < riverCount; i++)
        {
            CreateRiver();
        }
    }


    private void CreateRiver()
    {
        if (!TryGetRandomRoad(out Vector2Int current))
            return;

        // 대륙 크기에 비례해서 강 길이 결정
        int minLength = Mathf.Max(8, Mathf.RoundToInt(Mathf.Sqrt(landBlockCount) * 1.5f));
        int maxLength = Mathf.Max(12, Mathf.RoundToInt(Mathf.Sqrt(landBlockCount) * 3.0f));

        int length = Random.Range(minLength, maxLength);

        // 강 폭도 대륙 크기에 따라 증가
        int minWidth = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(landBlockCount) * 0.08f));
        int maxWidth = Mathf.Max(3, Mathf.RoundToInt(Mathf.Sqrt(landBlockCount) * 0.15f));

        int width = Random.Range(minWidth, maxWidth + 1);

        Vector2Int direction =
            Random.value < 0.5f
            ? Vector2Int.right
            : Vector2Int.up;

        for (int i = 0; i < length; i++)
        {
            PaintRiver(current, width);

            current += direction;

            if (Random.value < 0.3f)
            {
                direction = GetRandomDirection(direction);
            }

            if (!IsInsideMap(current))
                break;

            if (mapblueprint[current.x, current.y] != TileType.Road)
                break;
        }
    }


    private void PaintRiver(
        Vector2Int center,
        int width)
    {
        for (int x = -width; x <= width; x++)
        {
            for (int z = -width; z <= width; z++)
            {
                int px = center.x + x;
                int pz = center.y + z;


                Vector2Int pos =
                    new Vector2Int(px, pz);


                // 맵 밖
                if (!IsInsideMap(pos))
                    continue;


                // 원형 범위
                if (x * x + z * z >
                    width * width)
                {
                    continue;
                }


                // 육지가 아니면 강 생성 안 함
                if (mapblueprint[px, pz]
                    != TileType.Road)
                {
                    continue;
                }


                // 시작점 보호
                if (pos == startPos)
                    continue;


                // 출구 보호
                if (pos == exitPos)
                    continue;


                mapblueprint[px, pz] =
                    TileType.River;
            }
        }
    }


    // =========================================================
    // 상자
    // =========================================================

    private void GenerateBox()
    {
        int count = 0;


        while (count < boxCount)
        {
            if (!TryGetRandomRoad(
                out Vector2Int pos))
            {
                break;
            }


            if (pos == startPos ||
                pos == exitPos)
            {
                continue;
            }


            mapblueprint[pos.x, pos.y] =
                TileType.Box;


            count++;
        }
    }


    // =========================================================
    // 상점
    // =========================================================

    private void GenerateStore()
    {
        int count = 0;


        while (count < storeCount)
        {
            if (!TryGetRandomRoad(
                out Vector2Int pos))
            {
                break;
            }


            if (pos == startPos ||
                pos == exitPos)
            {
                continue;
            }


            mapblueprint[pos.x, pos.y] =
                TileType.Store;


            count++;
        }
    }


    // =========================================================
    // 장애물
    // =========================================================

    private void GenerateDisMoveable()
    {
        int count = 0;


        while (count < disMoveableCount)
        {
            if (!TryGetRandomRoad(
                out Vector2Int pos))
            {
                break;
            }


            if (pos == startPos ||
                pos == exitPos)
            {
                continue;
            }


            mapblueprint[pos.x, pos.y] =
                TileType.DisMoveable;


            count++;
        }
    }


    // =========================================================
    // 랜덤 Road 찾기
    // =========================================================

    private bool TryGetRandomRoad(
        out Vector2Int pos)
    {
        pos = Vector2Int.zero;


        List<Vector2Int> roads =
            new List<Vector2Int>();


        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                if (mapblueprint[x, z] ==
                    TileType.Road)
                {
                    roads.Add(
                        new Vector2Int(x, z)
                    );
                }
            }
        }


        if (roads.Count == 0)
            return false;


        pos =
            roads[
                Random.Range(0, roads.Count)
            ];


        return true;
    }


    // =========================================================
    // 맵 범위 검사
    // =========================================================

    private bool IsInsideMap(
        Vector2Int pos)
    {
        return
            pos.x >= 0 &&
            pos.x < blockCountX &&
            pos.y >= 0 &&
            pos.y < blockCountZ;
    }


    // =========================================================
    // 강 방향 변경
    // =========================================================

    private Vector2Int GetRandomDirection(
        Vector2Int current)
    {
        List<Vector2Int> dirs =
            new List<Vector2Int>()
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };


        // 바로 뒤로 가지 않도록 제거
        dirs.Remove(-current);


        return dirs[
            Random.Range(0, dirs.Count)
        ];
    }


    // =========================================================
    // 실제 맵 생성
    // =========================================================

    private void DrawGrid()
    {
        // 기존 맵 제거
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }


        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                Vector3 pos =
                    new Vector3(
                        x * blockDistance,
                        0,
                        z * blockDistance
                    );


                GameObject prefab = null;


                switch (mapblueprint[x, z])
                {
                    // Empty = 바다
                    // 현재는 바다 프리팹을 따로 생성하지 않음
                    case TileType.Empty:
                        prefab = null;
                        break;


                    case TileType.Start:
                        prefab = roadPrefab;
                        break;


                    case TileType.Road:
                        prefab = roadPrefab;
                        break;


                    case TileType.River:
                        prefab = riverPrefab;
                        break;


                    case TileType.Store:
                        prefab = storePrefab;
                        break;


                    case TileType.Box:
                        prefab = boxPrefab;
                        break;


                    case TileType.Exit:
                        prefab = exitPrefab;
                        break;


                    case TileType.DisMoveable:
                        prefab = disMoveablePrefab;
                        break;
                }


                if (prefab == null)
                    continue;


                GameObject obj =
                    Instantiate(
                        prefab,
                        pos,
                        Quaternion.identity,
                        transform
                    );


                MapInfo info =
                    obj.GetComponent<MapInfo>();


                if (info == null)
                {
                    Debug.LogError(
                        $"{prefab.name} 프리팹에 MapInfo가 없습니다."
                    );

                    continue;
                }


                info.Init(
                    new Vector2Int(x, z),
                    mapblueprint[x, z],
                    pos
                );


                blocks[x, z] = info;
            }
        }


        // 인접 타일 연결
        ConnectNeighbours();


        // 플레이어를 시작 위치에 배치
        Vector3 playerPos =
            new Vector3(
                startPos.x * blockDistance,
                0,
                startPos.y * blockDistance
            );


        playerBody.transform.position =
            playerPos;


        generatorEnd = true;
    }


    // =========================================================
    // 생성 완료 여부
    // =========================================================

    public bool IsGenerateEnd()
    {
        return generatorEnd;
    }


    // =========================================================
    // 인접 타일 연결
    // =========================================================

    private void ConnectNeighbours()
    {
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                MapInfo current =
                    blocks[x, z];


                if (current == null)
                    continue;


                current.SetNeighbour(
                    z < blockCountZ - 1
                        ? blocks[x, z + 1]
                        : null,

                    z > 0
                        ? blocks[x, z - 1]
                        : null,

                    x > 0
                        ? blocks[x - 1, z]
                        : null,

                    x < blockCountX - 1
                        ? blocks[x + 1, z]
                        : null
                );
            }
        }
    }


    // =========================================================
    // Terrain
    // =========================================================

    private void GenerateTerrain()
    {
        if (generatedTerrain != null)
        {
            Destroy(
                generatedTerrain.gameObject
            );
        }


        TerrainData terrainData =
            new TerrainData();


        terrainData.heightmapResolution =
            terrainHeightmapResolution;


        float terrainWidth =
            blockCountX * blockDistance;


        float terrainLength =
            blockCountZ * blockDistance;


        terrainData.size =
            new Vector3(
                terrainWidth + blockDistance,
                terrainHeight,
                terrainLength + blockDistance
            );


        terrainData.terrainLayers =
            new TerrainLayer[]
            {
                grassLayer,
                dirtLayer,
                rockLayer,
                sandLayer
            };


        GenerateTerrainHeight(
            terrainData
        );


        GameObject terrainObject =
            Terrain.CreateTerrainGameObject(
                terrainData
            );


        terrainObject.name =
            "Generated Terrain";


        terrainObject.transform.SetParent(
            transform
        );


        terrainObject.transform.position =
            new Vector3(
                -blockDistance * 0.5f,
                0,
                -blockDistance * 0.5f
            );


        generatedTerrain =
            terrainObject.GetComponent<Terrain>();


        Debug.Log(
            "Terrain 생성 완료"
        );
    }


    private void GenerateTerrainHeight(
        TerrainData terrainData)
    {
        int resolution =
            terrainData.heightmapResolution;


        float[,] heights =
            new float[
                resolution,
                resolution
            ];


        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX =
                    (float)x /
                    (resolution - 1);


                float normalizedZ =
                    (float)z /
                    (resolution - 1);


                float noise =
                    Mathf.PerlinNoise(
                        normalizedX *
                        terrainNoiseScale *
                        100f,

                        normalizedZ *
                        terrainNoiseScale *
                        100f
                    );


                float height =
                    noise *
                    (terrainNoiseHeight /
                     terrainHeight);


                heights[z, x] =
                    height;
            }
        }


        terrainData.SetHeights(
            0,
            0,
            heights
        );
    }
}