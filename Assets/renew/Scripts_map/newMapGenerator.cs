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

    [Header("Water")]
    [Tooltip("River 타일 위에 생성되는 일반 물 프리팹")]
    [SerializeField] private GameObject waterPrefab;

    [Tooltip("River와 Empty가 만나는 외곽에 생성되는 흐르는 물 프리팹")]
    [SerializeField] private GameObject flowWaterPrefab;

    [Tooltip("흐르는 물 프리팹의 기본 Forward 방향이 실제 물의 흐름 방향과 다를 경우 회전 보정")]
    [SerializeField] private Vector3 flowWaterRotationOffset;

    [Tooltip("River와 외부/Empty가 만나는 경계에서 물 프리팹을 얼마나 River 쪽/Empty 쪽으로 이동할지")]
    [SerializeField] private float flowWaterEdgeOffset = 0f;

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

    // 맵 전체 그리드의 중심을 월드 원점으로 맞추기 위한 오프셋
    private Vector3 mapWorldOffset;


    private void Awake()
    {
        CalculateMapSize();

        mapblueprint = new TileType[blockCountX, blockCountZ];
        blocks = new MapInfo[blockCountX, blockCountZ];
        heightIndices = new int[blockCountX, blockCountZ];

        // 전체 그리드의 중심이 월드 (0,0,0)이 되도록 계산
        mapWorldOffset = new Vector3(
            (blockCountX - 1) * blockDistance * 0.5f,
            0f,
            (blockCountZ - 1) * blockDistance * 0.5f
        );
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

    public TileType GetTileType(Vector2Int pos)
    {
        if (!IsInsideMap(pos))
            return TileType.Empty;

        return mapblueprint[pos.x, pos.y];
    }


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
        // 기존처럼 2~4개의 강
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
        int minLength =
            Mathf.Max(
                8,
                Mathf.RoundToInt(
                    Mathf.Sqrt(landBlockCount) * 1.5f));

        int maxLength =
            Mathf.Max(
                12,
                Mathf.RoundToInt(
                    Mathf.Sqrt(landBlockCount) * 3.0f));

        int length =
            Random.Range(
                minLength,
                maxLength);

        // 강 폭도 대륙 크기에 따라 증가
        int minWidth =
            Mathf.Max(
                2,
                Mathf.RoundToInt(
                    Mathf.Sqrt(landBlockCount) * 0.08f));

        int maxWidth =
            Mathf.Max(
                3,
                Mathf.RoundToInt(
                    Mathf.Sqrt(landBlockCount) * 0.15f));

        int width =
            Random.Range(
                minWidth,
                maxWidth + 1);

        Vector2Int direction =
            Random.value < 0.5f
            ? Vector2Int.right
            : Vector2Int.up;

        for (int i = 0; i < length; i++)
        {
            PaintRiver(
                current,
                width);

            current += direction;

            if (Random.value < 0.3f)
            {
                direction =
                    GetRandomDirection(direction);
            }

            if (!IsInsideMap(current))
                break;

            if (mapblueprint[current.x, current.y]
                != TileType.Road)
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
    // Empty / 맵 외부 판정
    // =========================================================

    /// <summary>
    /// 맵 내부의 Empty 또는 맵 배열 바깥이면 true.
    ///
    /// 맵 바깥은 실제 배열에 존재하지 않지만
    /// 물 생성 판정에서는 전부 가상의 Empty로 취급한다.
    /// </summary>
    private bool IsEmptyOrOutsideMap(Vector2Int pos)
    {
        // 맵 바깥
        if (!IsInsideMap(pos))
            return true;

        // 맵 내부 Empty
        return mapblueprint[pos.x, pos.y] ==
               TileType.Empty;
    }


    private bool IsOutsideMap(Vector2Int pos)
    {
        return !IsInsideMap(pos);
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
    private void CreateStackedTerrain(
        int x,
        int z,
        int heightIndex)
    {
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
        float totalLength =
            tileLength +
            (heightIndex * blockDistance);

        // 현재 타일의 상단 기준 Y.
        float topY =
            heightIndex * blockDistance;

        // Pivot이 중앙인 프리팹이므로 전체 길이의 중앙에 배치
        float centerY =
            topY -
            (totalLength * 0.5f) -
            (blockDistance * 0.5f);

        GameObject block =
            Instantiate(
                terrainBlockPrefab,
                new Vector3(
                    x * blockDistance - mapWorldOffset.x,
                    centerY,
                    z * blockDistance - mapWorldOffset.z
                ),
                Quaternion.identity,
                stackedTerrainParent
            );

        block.name =
            $"TerrainFill_{x}_{z}_H{heightIndex}";

        StretchTerrainFill(
            block,
            totalLength);
    }


    private void StretchTerrainFill(
        GameObject block,
        float targetLength)
    {
        Renderer renderer =
            block.GetComponentInChildren<Renderer>();

        if (renderer == null)
            return;

        // 프리팹의 원래 월드 Y 길이
        float currentLength =
            renderer.bounds.size.y;

        if (currentLength <= 0.0001f)
            return;

        Vector3 scale =
            block.transform.localScale;

        scale.y *=
            targetLength / currentLength;

        block.transform.localScale =
            scale;

        // Scale 적용 후 실제 Renderer bounds를 다시 확인
        renderer =
            block.GetComponentInChildren<Renderer>();

        float topY =
            block.transform.position.y +
            targetLength * 0.5f;

        float boundsOffset =
            topY -
            renderer.bounds.max.y;

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

            renderer.material =
                material;

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

                // 타일 하나당 Terrain Block Prefab은 정확히 1개
                CreateStackedTerrain(
                    x,
                    z,
                    height);
            }
        }
    }


    // =========================================================
    // Water
    // =========================================================

    /// <summary>
    /// River 타일 하나당 일반 물 프리팹을 생성한다.
    /// </summary>
    private void CreateRiverWater(
        int x,
        int z)
    {
        if (waterPrefab == null)
            return;

        Vector2Int pos =
            new Vector2Int(x, z);

        Vector3 worldPos =
            GetMapWorldPosition(pos);

        worldPos.y =
            GetWorldHeight(pos);

        GameObject water =
            Instantiate(
                waterPrefab,
                worldPos,
                Quaternion.identity,
                transform);

        water.name =
            $"RiverWater_{x}_{z}";
    }


    /// <summary>
    /// River의 특정 방향이 Empty 또는 맵 외부라면
    /// 그 경계에 흐르는 물 프리팹을 생성한다.
    ///
    /// direction은 River 기준으로 Empty가 있는 방향이다.
    /// </summary>
    private void CreateFlowWater(
        Vector2Int riverPos,
        Vector2Int direction)
    {
        if (flowWaterPrefab == null)
            return;

        Vector2Int emptyPos =
            riverPos + direction;

        // Empty 또는 맵 바깥인지 확인
        if (!IsEmptyOrOutsideMap(emptyPos))
            return;

        Vector3 riverWorldPos =
            GetMapWorldPosition(riverPos);

        float riverWorldHeight =
            GetWorldHeight(riverPos);

        riverWorldPos.y =
            riverWorldHeight;

        // River와 Empty의 경계.
        // River 중심에서 direction 방향으로 반 타일 이동하면
        // 정확히 두 타일의 경계에 위치한다.
        Vector3 flowWorldPos =
            riverWorldPos +
            new Vector3(
                direction.x,
                0f,
                direction.y
            ) *
            (blockDistance * 0.5f);

        // 사용자가 직접 미세 조정할 수 있는 값
        flowWorldPos +=
            new Vector3(
                direction.x,
                0f,
                direction.y
            ) *
            flowWaterEdgeOffset;

        flowWorldPos.y =
            riverWorldHeight;

        // Flow Water의 Forward 방향을
        // River -> Empty 방향으로 맞춘다.
        Quaternion rotation =
            Quaternion.LookRotation(
                new Vector3(
                    direction.x,
                    0f,
                    direction.y
                ),
                Vector3.up
            );

        rotation *=
            Quaternion.Euler(
                flowWaterRotationOffset
            );

        GameObject flowWater =
            Instantiate(
                flowWaterPrefab,
                flowWorldPos,
                rotation,
                transform);

        string directionName;

        if (direction == Vector2Int.left)
            directionName = "Left";
        else if (direction == Vector2Int.right)
            directionName = "Right";
        else if (direction == Vector2Int.up)
            directionName = "Up";
        else
            directionName = "Down";

        if (IsOutsideMap(emptyPos))
        {
            flowWater.name =
                $"FlowWater_Outside_{riverPos.x}_{riverPos.y}_{directionName}";
        }
        else
        {
            flowWater.name =
                $"FlowWater_Empty_{riverPos.x}_{riverPos.y}_{directionName}";
        }
    }


    /// <summary>
    /// 모든 River를 검사하여
    /// River 자체에는 일반 물,
    /// River와 Empty가 만나는 경계에는 흐르는 물을 생성한다.
    /// </summary>
    private void DrawWater()
    {
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
                if (mapblueprint[x, z] !=
                    TileType.River)
                    continue;

                // -------------------------------------------------
                // 1. River 자체의 일반 물
                // -------------------------------------------------

                CreateRiverWater(
                    x,
                    z);

                Vector2Int riverPos =
                    new Vector2Int(x, z);

                // -------------------------------------------------
                // 2. River 주변 4방향 검사
                // -------------------------------------------------

                foreach (Vector2Int direction in directions)
                {
                    Vector2Int checkPos =
                        riverPos + direction;

                    // 내부 Empty + 외부 Empty 모두 처리
                    if (!IsEmptyOrOutsideMap(checkPos))
                        continue;

                    CreateFlowWater(
                        riverPos,
                        direction);
                }
            }
        }
    }


    // =========================================================
    // 월드 좌표 계산
    // =========================================================

    private Vector3 GetMapWorldPosition(
        Vector2Int pos)
    {
        return new Vector3(
            pos.x * blockDistance -
            mapWorldOffset.x,

            0f,

            pos.y * blockDistance -
            mapWorldOffset.z
        );
    }


    // =========================================================
    // 실제 맵 그리기
    // =========================================================

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
                    GetMapWorldPosition(
                        new Vector2Int(x, z));

                pos.y =
                    worldY;

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

                // 맵의 실제 X/Z 좌표를 기준으로 밝은색/어두운색 적용
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

        // 3. Water 생성
        //
        // River 일반 물
        // River-Empty 경계의 흐르는 물
        //
        // 맵 바깥도 Empty로 처리하기 때문에
        // x=0 / x=max / z=0 / z=max의 River도 검사된다.
        DrawWater();

        ConnectNeighbours();

        Vector3 playerPos =
            GetMapWorldPosition(startPos);

        playerPos.y =
            GetWorldHeight(startPos);

        if (playerBody != null)
            playerBody.transform.position = playerPos;

        generatorEnd = true;
    }


    // =========================================================
    // 외부 접근용
    // =========================================================

    public int GetMapSizeX()
    {
        return blockCountX;
    }


    public int GetMapSizeZ()
    {
        return blockCountZ;
    }


    public float GetBlockDistance()
    {
        return blockDistance;
    }


    private void NormalizeHeightIndices()
    {
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                TileType type =
                    mapblueprint[x, z];

                if (type == TileType.River)
                {
                    heightIndices[x, z] = 0;
                }
                else if (IsHeightCandidate(
                    new Vector2Int(x, z)))
                {
                    heightIndices[x, z] =
                        Mathf.Clamp(
                            heightIndices[x, z],
                            1,
                            3);
                }
                else
                {
                    heightIndices[x, z] = 0;
                }
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
    // 이동 이벤트
    // =========================================================

    public bool InvokeMovementEvent(
        Vector2Int from,
        Vector2Int to)
    {
        if (!IsInsideMap(from) ||
            !IsInsideMap(to))
            return false;

        MapInfo fromInfo =
            blocks[from.x, from.y];

        MapInfo toInfo =
            blocks[to.x, to.y];

        if (fromInfo == null ||
            toInfo == null)
            return false;

        return fromInfo.TryInvokeMoveEvent(
            toInfo);
    }


    public MapInfo GetMapInfo(
        Vector2Int pos)
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