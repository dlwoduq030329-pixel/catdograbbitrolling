using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public enum TileType
{
    Empty,          // 아무것도 없는 상태(생성 전)
    Road,           // 일반 이동 가능 타일
    River,          // 강(여러 칸 차지하는 장애물)
    Store,          // 상점
    Box,            // 상자
    Exit,           // 다음 층 이동
    DisMoveable,     // 일반 이동 불가 타일(바위, 나무 등)
    Start
}

public class MapGenerator : MonoBehaviour
{
    [Tooltip("맵 생성기")]
    [Header("코드 설명용 변수. 마우스를 올려 확인.")]
    [SerializeField]
    bool CODE_EXPLAIN;

    [Header("블록간의 거리")]
    [SerializeField]
    float blockDistance;
    [Header("생성 할 블럭의 세로 갯수(z)")]
    [SerializeField]
    int blockCountZ;
    [Header("생성 할 블럭의 가로 갯수(X)")]
    [SerializeField]
    int blockCountX;


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

    private Vector2Int startPos;    //논리적 시작 위치를 의미.
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

    private void Awake()
    {
        mapblueprint = new TileType[blockCountX, blockCountZ];
        blocks = new MapInfo[blockCountX, blockCountZ];
    }
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("야옹~");
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void StartGenerator()
    {
        bool success = false;
        generatorEnd = false;
        while (!success)
        {
            GenerateRoad();

            GenerateStart();
            GenerateExit();

            GenerateRiver();
            GenerateDisMoveable();
            GenerateStore();
            GenerateBox();
            /*
            ClearMap(); //맵 초기화. 새로운 맵 생성시 간섭 안되게.

            GenerateStart();    // 시작점 지정.

            GenerateRoad();     // 맵 깔기 오픈 월드 느낌을 내기 위해서 맵 전체를 일단은 도로로 지정후 장애물 지정.

            GenerateRiver();    // 강 생성

            GenerateDisMoveable();  //이동 불가능 타일을 까는 코드. 강은 범위 지정이라 따로 뺀것.

            GenerateStore();    //상점 생성

            GenerateBox();  //상자 생성

            GenerateExit(); //탈출구 생성. dismoveable Object에 의해 막히는걸 막기 위해 마지막에 생성.
            */

            success = CheckPath();
        }

        DrawGrid();
        //GenerateTerrain();
    }

    private bool CheckPath()
    {
        if (!IsWalkable(mapblueprint[startPos.x, startPos.y]))
            return false;

        if (!IsWalkable(mapblueprint[exitPos.x, exitPos.y]))
            return false;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        bool[,] visited = new bool[blockCountX, blockCountZ];

        queue.Enqueue(startPos);
        visited[startPos.x, startPos.y] = true;

        // 상, 하, 좌, 우
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

                // 이동 가능한 타일인지
                if (!IsWalkable(mapblueprint[next.x, next.y]))
                    continue;

                visited[next.x, next.y] = true;
                queue.Enqueue(next);
            }
        }

        // 출구까지 도달하지 못함
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

    private void ClearMap()
    {
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                mapblueprint[x, z] = TileType.Empty;
            }
        }
    }


    private void GenerateStart()
    {
        startPos = new Vector2Int(
            0,
            Random.Range(0, blockCountZ)); 

        mapblueprint[startPos.x, startPos.y] = TileType.Start;
    }

    private void GenerateExit()
    {
        exitPos = new Vector2Int(
            blockCountX - 1,
            Random.Range(0, blockCountZ));

        mapblueprint[exitPos.x, exitPos.y] = TileType.Exit;
    }

    private void GenerateRoad()
    {
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                mapblueprint[x, z] = TileType.Road; 
            }
        }
    }

    private void GenerateRiver()
    {
        // 강 개수
        int riverCount = Random.Range(1, 3);

        for (int i = 0; i < riverCount; i++)
        {
            CreateRiver();
        }
    }

    private void CreateRiver()
    {
        // 시작 위치
        Vector2Int current = new Vector2Int(
            Random.Range(2, blockCountX - 2),
            Random.Range(2, blockCountZ - 2));

        // 강 길이
        int length = Random.Range(8, 18);   //매직넘버 사용 원하는 입맛대로 지정

        // 강 폭
        int width = Random.Range(2, 4); //매직넘버 사용 원하는 입맛대로 지정

        // 처음 방향
        Vector2Int direction = Random.value < 0.5f ?
            Vector2Int.right :
            Vector2Int.up;

        for (int i = 0; i < length; i++)
        {
            PaintRiver(current, width); //실제로 강 설치.

            // 이동
            current += direction;

            // 30% 확률로 방향 변경
            if (Random.value < 0.3f)
            {
                direction = GetRandomDirection(direction);
            }

            // 맵 밖이면 종료
            if (!IsInsideMap(current))
                break;
        }
    }

    private void GenerateBox()
    {
        int count = 0;

        while (count < boxCount)
        {
            if (!TryGetRandomRoad(out Vector2Int pos))
                break;

            if (pos == startPos || pos == exitPos)
                continue;

            mapblueprint[pos.x, pos.y] = TileType.Box;
            count++;
        }
    }

    private void GenerateStore()
    {
        int count = 0;

        while (count < storeCount)
        {
            if (!TryGetRandomRoad(out Vector2Int pos))
                break;

            if (pos == startPos || pos == exitPos)
                continue;

            mapblueprint[pos.x, pos.y] = TileType.Store;
            count++;
        }
    }

    private void GenerateDisMoveable()
    {
        int count = 0;

        while (count < disMoveableCount)
        {
            if (!TryGetRandomRoad(out Vector2Int pos))
                break;  //장애물 설치 할 수 있는 공간이 없다면 반복문 탈출

            // 시작과 출구는 제외
            if (pos == startPos || pos == exitPos)
                continue;

            mapblueprint[pos.x, pos.y] = TileType.DisMoveable;
            count++;
        }
    }

    private bool TryGetRandomRoad(out Vector2Int pos)
    {
        pos = Vector2Int.zero;

        List<Vector2Int> roads = new List<Vector2Int>();

        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                if (mapblueprint[x, z] == TileType.Road)
                {
                    roads.Add(new Vector2Int(x, z));
                }
            }
        }

        if (roads.Count == 0)
            return false;

        pos = roads[Random.Range(0, roads.Count)];
        return true;
    }

    private void PaintRiver(Vector2Int center, int width)
    {
        for (int x = -width; x <= width; x++)
        {
            for (int z = -width; z <= width; z++)
            {
                int px = center.x + x;
                int pz = center.y + z;

                Vector2Int pos = new Vector2Int(px, pz);
                if (!IsInsideMap(pos))
                    continue;

                // 원형 느낌으로 생성
                if (x * x + z * z > width * width)
                    continue;

                if (pos == startPos || pos == exitPos)
                    continue;

                mapblueprint[px, pz] = TileType.River;
            }
        }
    }

    private bool IsInsideMap(Vector2Int pos)
    {
        return pos.x >= 0 &&
               pos.x < blockCountX &&
               pos.y >= 0 &&
               pos.y < blockCountZ;
    }

    private Vector2Int GetRandomDirection(Vector2Int current)
    {
        List<Vector2Int> dirs = new List<Vector2Int>()
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

        // 바로 뒤로 가는 방향은 제거
        dirs.Remove(-current);

        return dirs[Random.Range(0, dirs.Count)];
    }

    private void DrawGrid()
    {
        // 기존 맵이 있다면 제거
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                // 월드 좌표 계산
                Vector3 pos =
                    new Vector3(
                        x * blockDistance,
                        0,
                        z * blockDistance);

                GameObject prefab = null;

                switch (mapblueprint[x, z])
                {
                    case TileType.Empty:
                    case TileType.Start:
                        {
                            prefab = roadPrefab;
                            playerBody.transform.position = pos + new Vector3(0, 0.5f, 0);
                        }
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

                // 프리팹 생성
                GameObject obj = Instantiate(
                    prefab,
                    pos,
                    Quaternion.identity,
                    transform);

                // MapInfo 가져오기
                MapInfo info = obj.GetComponent<MapInfo>();

                if (info == null)
                {
                    Debug.LogError($"{prefab.name} 프리팹에 MapInfo가 없습니다.");
                    continue;
                }

                // 초기화
                info.Init(
                    new Vector2Int(x, z),
                    mapblueprint[x, z],
                    pos);

                // 배열 저장
                blocks[x, z] = info;

                // 타입별 리스트 등록 (선택사항)
            }
        }

        // 모든 타일 생성 후 인접 타일 연결
        ConnectNeighbours();

        generatorEnd = true;
    }

    public bool IsGenerateEnd()
    {
        return generatorEnd;
    }

    private void ConnectNeighbours()
    {
        for (int x = 0; x < blockCountX; x++)
        {
            for (int z = 0; z < blockCountZ; z++)
            {
                MapInfo current = blocks[x, z];

                if (current == null)
                    continue;

                current.SetNeighbour(
                    z < blockCountZ - 1 ? blocks[x, z + 1] : null, // Up
                    z > 0 ? blocks[x, z - 1] : null,               // Down
                    x > 0 ? blocks[x - 1, z] : null,               // Left
                    x < blockCountX - 1 ? blocks[x + 1, z] : null  // Right
                );
            }
        }
    }

    private void GenerateTerrain()
    {
        // 기존에 생성된 Terrain이 있다면 제거
        if (generatedTerrain != null)
        {
            Destroy(generatedTerrain.gameObject);
        }


        // TerrainData 생성
        TerrainData terrainData = new TerrainData();


        terrainData.heightmapResolution = terrainHeightmapResolution;


        float terrainWidth = blockCountX * blockDistance;
        float terrainLength = blockCountZ * blockDistance;

        terrainData.size = new Vector3(
            terrainWidth + blockDistance,
            terrainHeight,
            terrainLength + blockDistance
        );


        // Terrain Layer 등록
        terrainData.terrainLayers = new TerrainLayer[]
        {
        grassLayer,
        dirtLayer,
        rockLayer,
        sandLayer
        };


        // 높이 생성
        GenerateTerrainHeight(terrainData);


        // Terrain GameObject 생성
        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);


        terrainObject.name = "Generated Terrain";


        terrainObject.transform.SetParent(transform);


        terrainObject.transform.position =
            new Vector3(
                -blockDistance * 0.5f,
                0,
                -blockDistance * 0.5f
            );

        generatedTerrain = terrainObject.GetComponent<Terrain>();
        

        Debug.Log("Terrain 생성 완료");
    }


    private void GenerateTerrainHeight(TerrainData terrainData)
    {
        int resolution = terrainData.heightmapResolution;

        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (float)x / (resolution - 1);
                float normalizedZ = (float)z / (resolution - 1);

                float noise = Mathf.PerlinNoise(
                    normalizedX * terrainNoiseScale * 100f,
                    normalizedZ * terrainNoiseScale * 100f
                );

                float height = noise * (terrainNoiseHeight / terrainHeight);

                heights[z, x] = height;
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }
}
