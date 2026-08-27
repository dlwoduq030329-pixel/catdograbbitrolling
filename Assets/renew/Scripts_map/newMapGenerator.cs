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

    [Header("Tile Checker Material")]
    [SerializeField] private Material brightTileMaterial;
    [SerializeField] private Material darkTileMaterial;

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

    [Header("Map Height")]
    [Tooltip("Height 1단계의 실제 높이. 가로 타일 1칸과 동일하게 blockDistance를 사용한다.")]
    [SerializeField] private float heightStep = 3f;

    [Tooltip("육지의 최대 Height Index. 0=River, 1~3=육지")]
    [SerializeField] private int maxHeightIndex = 3;

    [Tooltip("인접 타일의 일반 이동 허용 최대 높이 차이")]
    [SerializeField] private int maxWalkableHeightDifference = 1;

    [Range(0f, 1f)]
    [SerializeField] private float heightRandomness = 0.25f;

    [SerializeField] private int heightSmoothIterations = 3;

    [Header("Outside Terrain")]
    [Tooltip("맵 외부로 생성할 지형 폭")]
    [SerializeField] private int outsideTerrainWidth = 6;

    [Tooltip("맵 외부 지형의 높이. 기본 3")]
    [SerializeField] private int outsideTerrainHeightIndex = 3;

    [Header("Stacked Terrain")]
    [Tooltip("아래층을 채우는 순수 지형 블록 프리팹. 비워두면 roadPrefab 사용")]
    [SerializeField] private GameObject terrainBlockPrefab;

    [Tooltip("Terrain Fill Prefab 하나가 가지는 기본 아래쪽 길이")]
    [SerializeField] private int tileLength = 10;

    [Tooltip("Terrain Fill Prefab에 적용할 머티리얼")]
    [SerializeField] private Material terrainFillMaterial;

    [Tooltip("Y축 길이에 따른 머티리얼 타일링 배율")]
    [SerializeField] private float terrainFillTileScaleY = 1f;

    private int[,] heightIndices;
    private Transform stackedTerrainParent;






    private void Awake()
    {
        CalculateMapSize();

        mapblueprint = new TileType[blockCountX, blockCountZ];
        blocks = new MapInfo[blockCountX, blockCountZ];
        heightIndices = new int[blockCountX, blockCountZ];
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

            // 높이 생성
            GenerateHeight();

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

        NormalizeHeightIndices();
        DrawGrid();
        //GenerateRiverTerrain();
        //GenerateFogCloud();
        // GenerateTerrain();

        Debug.Log(
            $"맵 생성 완료 / 대륙 블록 : {landBlockCount} / 시도 횟수 : {tryCount}"
        );
    }

    // =========================================================
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
        if (!IsInsideMap(startPos) || !IsInsideMap(exitPos))
            return false;

        if (!IsWalkable(mapblueprint[startPos.x, startPos.y]))
            return false;

        if (!IsWalkable(mapblueprint[exitPos.x, exitPos.y]))
            return false;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        bool[,] visited = new bool[blockCountX, blockCountZ];

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

            if (current == exitPos)
                return true;

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;

                if (!IsInsideMap(next) ||
                    visited[next.x, next.y] ||
                    !IsWalkable(mapblueprint[next.x, next.y]))
                    continue;

                if (!CanMoveBetween(current, next))
                    continue;

                visited[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    public bool CanMoveBetween(Vector2Int from, Vector2Int to)
    {
        if (!IsInsideMap(from) || !IsInsideMap(to))
            return false;

        if (!IsWalkable(mapblueprint[from.x, from.y]) ||
            !IsWalkable(mapblueprint[to.x, to.y]))
            return false;

        int difference = Mathf.Abs(
            heightIndices[to.x, to.y] -
            heightIndices[from.x, from.y]);

        return difference <= maxWalkableHeightDifference;
    }

    public int GetHeightIndex(Vector2Int pos)
    {
        if (!IsInsideMap(pos))
            return 0;

        return heightIndices[pos.x, pos.y];
    }

    public float GetWorldHeight(Vector2Int pos)
    {
        // 단차 1개 = 타일 1칸
        return GetHeightIndex(pos) * blockDistance;
    }

    public int GetHeightDifference(Vector2Int from, Vector2Int to)
    {
        if (!IsInsideMap(from) || !IsInsideMap(to))
            return 0;

        return heightIndices[to.x, to.y] -
               heightIndices[from.x, from.y];
    }

    public HeightTransition GetHeightTransition(
        Vector2Int from,
        Vector2Int to)
    {
        if (!IsInsideMap(from) || !IsInsideMap(to))
            return HeightTransition.Invalid;

        int difference =
            heightIndices[to.x, to.y] -
            heightIndices[from.x, from.y];

        if (difference == 0) return HeightTransition.Flat;
        if (difference == 1) return HeightTransition.StepUp;
        if (difference == -1) return HeightTransition.StepDown;
        if (difference > 1) return HeightTransition.Climb;

        return HeightTransition.Drop;
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
                heightIndices[x, z] = 0;
            }
        }
    }


    // =========================================================
    // 대륙 생성
    // =========================================================


    // =========================================================
    // Height Index
    // =========================================================

    private void GenerateHeight()
    {
        maxHeightIndex = 3;
        outsideTerrainHeightIndex = 3;
        maxWalkableHeightDifference =
            Mathf.Max(0, maxWalkableHeightDifference);

        for (int x = 0; x < blockCountX; x++)
            for (int z = 0; z < blockCountZ; z++)
                heightIndices[x, z] = -1;

        // River = 0, 육지는 최소 1
        heightIndices[startPos.x, startPos.y] = 1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(startPos);

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
            int currentHeight = heightIndices[current.x, current.y];

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;

                if (!IsInsideMap(next) ||
                    !IsHeightCandidate(next) ||
                    heightIndices[next.x, next.y] >= 0)
                    continue;

                int nextHeight = currentHeight;

                if (Random.value < heightRandomness)
                    nextHeight += Random.value < 0.5f ? -1 : 1;

                nextHeight = Mathf.Clamp(nextHeight, 1, 3);

                heightIndices[next.x, next.y] = nextHeight;
                queue.Enqueue(next);
            }
        }

        // 누락된 육지 보정
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                Vector2Int pos = new Vector2Int(x, z);

                if (IsHeightCandidate(pos) &&
                    heightIndices[x, z] < 0)
                {
                    heightIndices[x, z] = FindNearestHeight(pos);
                }
            }
        }

        for (int i = 0; i < heightSmoothIterations; i++)
            SmoothHeightIndices();

        ClampNeighbourHeightDifference();
        ForceRiverHeightZero();
    }

    private bool IsHeightCandidate(Vector2Int pos)
    {
        if (!IsInsideMap(pos))
            return false;

        TileType type = mapblueprint[pos.x, pos.y];

        return type == TileType.Start ||
               type == TileType.Road ||
               type == TileType.Store ||
               type == TileType.Box ||
               type == TileType.Exit ||
               type == TileType.DisMoveable;
    }

    private int FindNearestHeight(Vector2Int target)
    {
        int bestDistance = int.MaxValue;
        int bestHeight = 1;

        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                if (heightIndices[x, z] < 0)
                    continue;

                int distance =
                    Mathf.Abs(x - target.x) +
                    Mathf.Abs(z - target.y);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestHeight = heightIndices[x, z];
                }
            }
        }

        return Mathf.Clamp(bestHeight, 1, 3);
    }

    private void SmoothHeightIndices()
    {
        int[,] result =
            new int[blockCountX, blockCountZ];

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                Vector2Int pos = new Vector2Int(x, z);

                if (!IsHeightCandidate(pos))
                {
                    result[x, z] = 0;
                    continue;
                }

                int sum = heightIndices[x, z];
                int count = 1;

                foreach (Vector2Int dir in directions)
                {
                    Vector2Int next = pos + dir;

                    if (!IsInsideMap(next) ||
                        !IsHeightCandidate(next))
                        continue;

                    sum += heightIndices[next.x, next.y];
                    count++;
                }

                result[x, z] =
                    Mathf.Clamp(
                        Mathf.RoundToInt((float)sum / count),
                        1,
                        3);
            }
        }

        result[startPos.x, startPos.y] = 1;

        for (int x = 0; x < blockCountX; x++)
            for (int z = 0; z < blockCountZ; z++)
                heightIndices[x, z] = result[x, z];
    }

    private void ClampNeighbourHeightDifference()
    {
        for (int iteration = 0; iteration < 10; iteration++)
        {
            bool changed = false;

            for (int x = 0; x < blockCountX; x++)
            {
                for (int z = 0; z < blockCountZ; z++)
                {
                    Vector2Int current =
                        new Vector2Int(x, z);

                    if (!IsHeightCandidate(current))
                        continue;

                    Vector2Int[] directions =
                    {
                        Vector2Int.up,
                        Vector2Int.down,
                        Vector2Int.left,
                        Vector2Int.right
                    };

                    foreach (Vector2Int dir in directions)
                    {
                        Vector2Int next = current + dir;

                        if (!IsHeightCandidate(next))
                            continue;

                        int a = heightIndices[x, z];
                        int b = heightIndices[next.x, next.y];

                        if (Mathf.Abs(a - b) <=
                            maxWalkableHeightDifference)
                            continue;

                        if (a < b)
                        {
                            heightIndices[next.x, next.y] =
                                Mathf.Clamp(
                                    a + maxWalkableHeightDifference,
                                    1, 3);
                        }
                        else
                        {
                            heightIndices[x, z] =
                                Mathf.Clamp(
                                    b + maxWalkableHeightDifference,
                                    1, 3);
                        }

                        changed = true;
                    }
                }
            }

            if (!changed)
                break;
        }
    }

    private void ForceRiverHeightZero()
    {
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                if (mapblueprint[x, z] == TileType.River)
                    heightIndices[x, z] = 0;
                else if (IsHeightCandidate(new Vector2Int(x, z)))
                    heightIndices[x, z] =
                        Mathf.Clamp(heightIndices[x, z], 1, 3);
                else
                    heightIndices[x, z] = 0;
            }
        }
    }

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

        //playerBody.transform.position = mapblueprint[startPos.x, startPos.y].Gameobject.transform.position + new Vector3(0, 2.5f, 0);
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


    // =========================================================
    // 실제 단차 블록 쌓기
    // =========================================================

    private void EnsureStackedTerrainParent()
    {
        if (stackedTerrainParent != null)
            return;

        GameObject parent =
            new GameObject("Stacked Terrain Blocks");

        parent.transform.SetParent(transform);
        parent.transform.localPosition = Vector3.zero;

        stackedTerrainParent = parent.transform;
    }

    private GameObject GetTerrainBlockPrefab()
    {
        return terrainBlockPrefab != null
            ? terrainBlockPrefab
            : roadPrefab;
    }

    /// <summary>
    /// Height 0 = 1개
    /// Height 1 = 2개
    /// Height 2 = 3개
    /// Height 3 = 4개
    ///
    /// 블록의 세로 단위는 blockDistance와 동일하다.
    /// 따라서 한 단계의 단차가 정확히 타일 1칸이다.
    /// </summary>
    /// <summary>
    /// 타일 하나당 Terrain Fill Prefab은 정확히 1개만 생성한다.
    ///
    /// tileLength = 10, blockDistance = 1이라고 하면
    /// Height 0 : 길이 10
    /// Height 1 : 길이 11
    /// Height 2 : 길이 12
    /// Height 3 : 길이 13
    ///
    /// 아래쪽 끝은 항상 -tileLength에 고정된다.
    /// 따라서 Height가 올라갈수록 Fill 하나가 위쪽으로 길어진다.
    /// </summary>
    private void CreateStackedTerrain(
        int x,
        int z,
        int heightIndex)
    {
        // 아래쪽을 채우는 전용 프리팹만 사용한다.
        // roadPrefab / 다른 타일 프리팹으로 대체하지 않는다.
        if (terrainBlockPrefab == null)
        {
            Debug.LogWarning(
                "Terrain Block Prefab이 지정되지 않았습니다."
            );

            return;
        }

        EnsureStackedTerrainParent();

        heightIndex =
            Mathf.Clamp(
                heightIndex,
                0,
                3);

        // 0 높이의 타일에서도 tileLength만큼 아래로 내려간다.
        // 높이가 1이면 tileLength + 1단차,
        // 높이가 2면 tileLength + 2단차,
        // 높이가 3이면 tileLength + 3단차.
        float totalLength =
            tileLength +
            (heightIndex * blockDistance);

        // 현재 타일의 상단 기준 Y.
        // River는 항상 0, 육지는 HeightIndex * blockDistance.
        float topY =
            heightIndex * blockDistance;

        // Pivot이 중앙인 프리팹이므로 일단 전체 길이의 중앙에 배치한다.
        float centerY =
            topY -
            (totalLength * 0.5f) -
            (blockDistance * 0.5f);

        GameObject block =
            Instantiate(
                terrainBlockPrefab,
                new Vector3(
                    x * blockDistance,
                    centerY,
                    z * blockDistance
                ),
                Quaternion.identity,
                stackedTerrainParent
            );

        block.name =
            $"TerrainFill_{x}_{z}_H{heightIndex}";

        StretchTerrainFill(
            block,
            totalLength);

        // ApplyTerrainFillMaterial(
        //   block,
        //  totalLength);
    }

    private void StretchTerrainFill(
        GameObject block,
        float targetLength)
    {
        Renderer renderer =
            block.GetComponentInChildren<Renderer>();

        if (renderer == null)
            return;

        // 프리팹의 원래 월드 Y 길이를 먼저 저장한다.
        float currentLength =
            renderer.bounds.size.y;

        if (currentLength <= 0.0001f)
            return;

        // 기존 scale 변수명은 그대로 유지한다.
        Vector3 scale =
            block.transform.localScale;

        scale.y *=
            targetLength / currentLength;

        block.transform.localScale =
            scale;

        /*
         * 중요:
         * Terrain Fill Prefab의 Pivot은 중앙이라고 가정한다.
         *
         * 단순히
         *     transform.position.y = topY - targetLength * 0.5f
         * 로 끝내면 프리팹의 Renderer 중심/오프셋 때문에
         * 위쪽 또는 아래쪽이 반 칸씩 어긋날 수 있다.
         *
         * 따라서 Scale을 적용한 "실제 Renderer bounds"를 다시 가져온 뒤
         * bounds.max.y가 정확히 topY에 오도록 오브젝트 전체를 이동한다.
         *
         * 이 방식이면 Pivot이 중앙이어도 실제 보이는 지형의
         * 상단/하단이 정확히 맞는다.
         */

        renderer =
            block.GetComponentInChildren<Renderer>();

        float topY =
            block.transform.position.y +
            targetLength * 0.5f;

        float boundsOffset =
            topY - renderer.bounds.max.y;

        Vector3 position =
            block.transform.position;

        position.y += boundsOffset;

        block.transform.position =
            position;
    }

    private void ApplyTerrainFillMaterial(
        GameObject block,
        float targetLength)
    {
        Renderer[] renderers =
            block.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            Material material;

            if (terrainFillMaterial != null)
            {
                material =
                    new Material(terrainFillMaterial);
            }
            else
            {
                material =
                    renderer.material;
            }

            renderer.material = material;

            Vector2 tiling =
                material.mainTextureScale;

            float baseLength =
                Mathf.Max(1, tileLength);

            tiling.y =
                (targetLength / baseLength) *
                terrainFillTileScaleY;

            material.mainTextureScale =
                tiling;
        }
    }

    private void DrawStackedTerrain()
    {
        if (terrainBlockPrefab == null)
        {
            Debug.LogWarning(
                "Terrain Block Prefab이 지정되지 않았습니다."
            );

            return;
        }

        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                TileType type =
                    mapblueprint[x, z];

                if (type == TileType.Empty)
                    continue;

                int height =
                    Mathf.Clamp(
                        heightIndices[x, z],
                        0,
                        3);

                // 타일 하나당 Terrain Block Prefab은 정확히 1개만 생성한다.
                CreateStackedTerrain(
                    x,
                    z,
                    height);
            }
        }
    }

    private void DrawGrid()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        stackedTerrainParent = null;

        // 1. 먼저 아래쪽 빈 공간을 전부 블록으로 채운다.
        DrawStackedTerrain();

        // 2. 논리 타일/상단 오브젝트 생성.
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                int heightIndex =
                    Mathf.Clamp(
                        heightIndices[x, z],
                        0,
                        3);

                float worldY =
                    heightIndex * blockDistance;

                Vector3 pos =
                    new Vector3(
                        x * blockDistance,
                        worldY,
                        z * blockDistance);

                GameObject prefab = null;

                switch (mapblueprint[x, z])
                {
                    case TileType.Empty:
                        prefab = null;
                        break;

                    case TileType.Start:
                    case TileType.Road:
                        prefab = roadPrefab;
                        break;

                    case TileType.River:
                        prefab = riverPrefab;
                        worldY = 0f;
                        pos.y = 0f;
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
                        transform);

                // 맵의 실제 X/Z 좌표를 기준으로 밝은색/어두운색을 지그재그 적용.
                // Road, Start, Store, Box, Exit, DisMoveable 모두 동일한 규칙을 사용한다.
                // River는 기존 강 머티리얼을 유지한다.
                if (mapblueprint[x, z] != TileType.Empty &&
                    mapblueprint[x, z] != TileType.River)
                {
                    Material tileMaterial =
                        ((x + z) % 2 == 0)
                            ? brightTileMaterial
                            : darkTileMaterial;

                    if (tileMaterial != null)
                    {
                        Renderer rootRenderer =
                            obj.GetComponent<Renderer>();

                        if (rootRenderer != null)
                        {
                            rootRenderer.material =
                                tileMaterial;
                        }
                        else
                        {
                            Renderer childRenderer =
                                obj.GetComponentInChildren<Renderer>();

                            if (childRenderer != null)
                                childRenderer.material =
                                    tileMaterial;
                        }
                    }
                }

                MapInfo info =
                    obj.GetComponent<MapInfo>();

                if (info == null)
                {
                    Debug.LogError(
                        $"{prefab.name} 프리팹에 MapInfo가 없습니다.");
                    continue;
                }

                info.Init(
                    new Vector2Int(x, z),
                    mapblueprint[x, z],
                    pos,
                    heightIndex,
                    worldY);

                blocks[x, z] = info;
            }
        }

        ConnectNeighbours();

        Vector3 playerPos =
            new Vector3(
                startPos.x * blockDistance,
                GetWorldHeight(startPos),
                startPos.y * blockDistance);

        if (playerBody != null)
            playerBody.transform.position = playerPos;

        generatorEnd = true;
    }

    private void NormalizeHeightIndices()
    {
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                TileType type = mapblueprint[x, z];

                if (type == TileType.River)
                    heightIndices[x, z] = 0;
                else if (IsHeightCandidate(new Vector2Int(x, z)))
                    heightIndices[x, z] =
                        Mathf.Clamp(
                            heightIndices[x, z],
                            1,
                            3);
                else
                    heightIndices[x, z] = 0;
            }
        }
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


    public bool InvokeMovementEvent(
        Vector2Int from,
        Vector2Int to)
    {
        if (!IsInsideMap(from) || !IsInsideMap(to))
            return false;

        MapInfo fromInfo = blocks[from.x, from.y];
        MapInfo toInfo = blocks[to.x, to.y];

        if (fromInfo == null || toInfo == null)
            return false;

        return fromInfo.TryInvokeMoveEvent(toInfo);
    }

    public MapInfo GetMapInfo(Vector2Int pos)
    {
        if (!IsInsideMap(pos))
            return null;

        return blocks[pos.x, pos.y];
    }

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



}