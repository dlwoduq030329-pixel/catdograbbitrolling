using System.Collections.Generic;
using UnityEngine;

public class GridWaterSimulation : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int sizeX = 100;
    [SerializeField] private int sizeY = 10;
    [SerializeField] private int sizeZ = 100;

    [SerializeField] private float cellSize = 1f;

    [Header("Simulation")]
    [SerializeField] private float simulationInterval = 0.05f;

    [Tooltip("물이 낮은 곳으로 이동하는 속도")]
    [SerializeField] private float flowSpeed = 0.35f;

    [Tooltip("절벽 아래로 떨어지는 속도")]
    [SerializeField] private float waterfallSpeed = 1f;

    [Tooltip("한 칸에 저장 가능한 물의 최대량")]
    [SerializeField] private float maxWaterAmount = 1f;

    [Header("Water Source")]
    [Tooltip("강 타일마다 계속 공급되는 물")]
    [SerializeField] private float sourceWaterAmount = 0.5f;

    [Header("Rendering")]
    [SerializeField] private GameObject waterBlockPrefab;

    [Tooltip("물이 거의 없는 양은 렌더링하지 않음")]
    [SerializeField] private float renderThreshold = 0.01f;


    // ============================================================
    // 내부 데이터
    // ============================================================

    private float[,,] waterGrid;

    /*
     * terrainHeight[x,z]
     *
     * 해당 타일의 실제 지형 높이.
     *
     * River = 0
     * Height 1 = 1
     * Height 2 = 2
     * Height 3 = 3
     */
    private int[,] terrainHeight;

    /*
     * 지형이 존재하는지.
     *
     * true  = 지형 있음
     * false = 빈 공간
     */
    private bool[,] terrainExists;


    /*
     * River 위치.
     * 모든 River가 Water Source가 된다.
     */
    private HashSet<Vector2Int> waterSources =
        new HashSet<Vector2Int>();


    /*
     * 현재 화면에 생성된 물 오브젝트.
     */
    private Dictionary<Vector3Int, GameObject> waterObjects =
        new Dictionary<Vector3Int, GameObject>();


    private NewMapGenerator mapGenerator;

    private float timer;

    private bool initialized;


    // ============================================================
    // Unity
    // ============================================================

    private void Awake()
    {
        CreateGrid();
    }


    private void Update()
    {
        if (!initialized)
            return;

        timer += Time.deltaTime;

        if (timer < simulationInterval)
            return;

        timer = 0f;

        SupplyWaterSources();

        SimulateWater();

        UpdateWaterVisual();
    }


    // ============================================================
    // Grid 생성
    // ============================================================

    private void CreateGrid()
    {
        waterGrid =
            new float[sizeX, sizeY, sizeZ];

        terrainHeight =
            new int[sizeX, sizeZ];

        terrainExists =
            new bool[sizeX, sizeZ];
    }


    // ============================================================
    // 초기화
    // ============================================================

    public void Initialize(NewMapGenerator generator)
    {
        if (generator == null)
        {
            Debug.LogError(
                "GridWaterSimulation.Initialize : NewMapGenerator가 null입니다."
            );

            return;
        }

        mapGenerator = generator;

        ClearSimulation();

        /*
         * NewMapGenerator의 실제 맵 크기를 가져온다.
         */
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector2Int pos =
                    new Vector2Int(x, z);

                int height =
                    generator.GetHeightIndex(pos);

                /*
                 * Height 0은 River.
                 *
                 * 실제 맵 범위 안의 타일만
                 * terrainExists = true로 취급한다.
                 */
                if (height > 0)
                {
                    terrainExists[x, z] = true;
                    terrainHeight[x, z] = height;
                }
                else
                {
                    /*
                     * River도 지형 바닥은 존재한다.
                     *
                     * 물이 흐를 바닥이 필요하므로
                     * River는 높이 0의 지형으로 취급.
                     */
                    terrainExists[x, z] = true;
                    terrainHeight[x, z] = 0;
                }
            }
        }


        /*
         * 모든 River를 Water Source로 등록.
         */
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector2Int pos =
                    new Vector2Int(x, z);

                if (generator.GetHeightIndex(pos) == 0)
                {
                    waterSources.Add(pos);
                }
            }
        }


        initialized = true;

        Debug.Log(
            $"Water Simulation 초기화 완료 / Source : {waterSources.Count}"
        );
    }


    // ============================================================
    // 시뮬레이션 초기화
    // ============================================================

    private void ClearSimulation()
    {
        waterSources.Clear();

        foreach (GameObject water in waterObjects.Values)
        {
            if (water != null)
                Destroy(water);
        }

        waterObjects.Clear();

        if (waterGrid == null)
            return;

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    waterGrid[x, y, z] = 0f;
                }
            }
        }
    }


    // ============================================================
    // Water Source
    // ============================================================

    /*
     * River 좌표를 직접 등록하고 싶을 때 사용.
     */
    public void AddWaterSource(
        Vector2Int riverPos,
        float blockDistance
    )
    {
        Vector3 worldPosition =
            new Vector3(
                riverPos.x * blockDistance,
                0f,
                riverPos.y * blockDistance
            );

        Vector3Int gridPosition =
            WorldToGrid(worldPosition);

        if (!IsInside(gridPosition))
            return;

        waterSources.Add(riverPos);

        /*
         * River는 y=0에 물이 존재.
         */
        waterGrid[
            gridPosition.x,
            0,
            gridPosition.z
        ] = maxWaterAmount;
    }


    /*
     * Grid 좌표로 Source 추가.
     */
    public void AddWaterSourceGrid(
        Vector3Int gridPosition
    )
    {
        if (!IsInside(gridPosition))
            return;

        Vector2Int source =
            new Vector2Int(
                gridPosition.x,
                gridPosition.z
            );

        waterSources.Add(source);

        waterGrid[
            gridPosition.x,
            gridPosition.y,
            gridPosition.z
        ] = maxWaterAmount;
    }


    // ============================================================
    // Source 물 공급
    // ============================================================

    private void SupplyWaterSources()
    {
        foreach (Vector2Int source in waterSources)
        {
            if (!IsInsideXZ(source))
                continue;

            /*
             * River는 항상 y=0.
             */
            waterGrid[
                source.x,
                0,
                source.y
            ] =
                Mathf.Clamp(
                    waterGrid[
                        source.x,
                        0,
                        source.y
                    ] + sourceWaterAmount,
                    0f,
                    maxWaterAmount
                );
        }
    }


    // ============================================================
    // 물 시뮬레이션
    // ============================================================

    private void SimulateWater()
    {
        float[,,] nextWater =
            new float[sizeX, sizeY, sizeZ];


        /*
         * 현재 상태 복사.
         */
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    nextWater[x, y, z] =
                        waterGrid[x, y, z];
                }
            }
        }


        /*
         * 낮은 위치부터 처리한다.
         *
         * 이렇게 해야 물이 위쪽 → 아래쪽으로
         * 자연스럽게 내려간다.
         */
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    float amount =
                        waterGrid[x, y, z];

                    if (amount <= 0.001f)
                        continue;


                    /*
                     * 1.
                     * 바로 아래가 비어 있으면
                     * 무조건 아래로 떨어진다.
                     */
                    if (TryFlowDown(
                        x,
                        y,
                        z,
                        amount,
                        nextWater))
                    {
                        continue;
                    }


                    /*
                     * 2.
                     * 아래가 지형으로 막혀 있으면
                     * 주변의 낮은 지형으로 흐른다.
                     */
                    FlowToLowerTerrain(
                        x,
                        y,
                        z,
                        amount,
                        nextWater
                    );
                }
            }
        }


        waterGrid = nextWater;
    }


    // ============================================================
    // 아래로 떨어지기
    // ============================================================

    private bool TryFlowDown(
        int x,
        int y,
        int z,
        float amount,
        float[,,] nextWater
    )
    {
        int belowY = y - 1;


        /*
         * Grid 아래쪽으로 나가면
         * 물이 맵 바깥으로 떨어진 것이다.
         */
        if (belowY < 0)
        {
            nextWater[x, y, z] = 0f;

            return true;
        }


        /*
         * 아래에 지형이 있으면 못 내려간다.
         */
        if (IsTerrainAt(
            x,
            belowY,
            z))
        {
            return false;
        }


        float available =
            maxWaterAmount -
            waterGrid[x, belowY, z];


        if (available <= 0f)
            return false;


        float flow =
            Mathf.Min(
                amount * waterfallSpeed,
                available
            );


        nextWater[x, y, z] -= flow;

        nextWater[
            x,
            belowY,
            z
        ] += flow;


        return true;
    }


    // ============================================================
    // 낮은 지형으로 흐르기
    // ============================================================

    private void FlowToLowerTerrain(
        int x,
        int y,
        int z,
        float amount,
        float[,,] nextWater
    )
    {
        /*
         * 현재 물이 놓여있는 지형 높이.
         */
        int currentTerrainHeight =
            GetTerrainSurfaceHeight(x, z);


        List<Vector2Int> targets =
            new List<Vector2Int>();


        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.down
        };


        foreach (Vector2Int direction in directions)
        {
            int nx =
                x + direction.x;

            int nz =
                z + direction.y;


            if (!IsInsideXZ(nx, nz))
            {
                /*
                 * 맵 바깥.
                 *
                 * 물이 절벽 밖으로 빠지는 경우.
                 */
                continue;
            }


            /*
             * 이웃 지형 높이.
             */
            int neighbourHeight =
                GetTerrainSurfaceHeight(
                    nx,
                    nz
                );


            /*
             * 현재보다 낮은 곳으로만 흐른다.
             */
            if (neighbourHeight >= currentTerrainHeight)
                continue;


            /*
             * 물이 실제로 존재할 수 있는
             * 표면 높이.
             */
            int targetY =
                neighbourHeight;


            if (!IsInsideY(targetY))
                continue;


            /*
             * 해당 위치가 비어 있어야 한다.
             */
            if (IsTerrainAt(
                nx,
                targetY,
                nz))
                continue;


            if (
                waterGrid[
                    nx,
                    targetY,
                    nz
                ] >= maxWaterAmount
            )
                continue;


            targets.Add(
                new Vector2Int(nx, nz)
            );
        }


        /*
         * 낮은 곳이 없으면 고인다.
         */
        if (targets.Count == 0)
            return;


        /*
         * 여러 방향으로 균등하게 분배.
         */
        float totalFlow =
            amount * flowSpeed;


        float flowPerTarget =
            totalFlow /
            targets.Count;


        foreach (Vector2Int target in targets)
        {
            int targetY =
                GetTerrainSurfaceHeight(
                    target.x,
                    target.y
                );


            float available =
                maxWaterAmount -
                waterGrid[
                    target.x,
                    targetY,
                    target.y
                ];


            float flow =
                Mathf.Min(
                    flowPerTarget,
                    available
                );


            if (flow <= 0f)
                continue;


            nextWater[x, y, z] -= flow;


            nextWater[
                target.x,
                targetY,
                target.y
            ] += flow;
        }
    }


    // ============================================================
    // 지형 높이
    // ============================================================

    private int GetTerrainSurfaceHeight(
        int x,
        int z
    )
    {
        if (!IsInsideXZ(x, z))
            return 0;

        return terrainHeight[x, z];
    }


    /*
     * 특정 Grid Y 위치에 지형이 존재하는지.
     *
     * 예:
     *
     * Height 1
     * ┌───────┐  ← y=1 물 표면
     * │ water │
     * ├───────┤
     * │solid  │  ← y=0
     * └───────┘
     *
     * Height 3
     * ┌───────┐ ← y=3
     * │ water │
     * ├───────┤ ← y=2
     * │solid  │
     * ├───────┤ ← y=1
     * │solid  │
     * ├───────┤ ← y=0
     * │solid  │
     * └───────┘
     */
    private bool IsTerrainAt(
        int x,
        int y,
        int z
    )
    {
        if (!IsInsideXZ(x, z))
            return false;

        int height =
            terrainHeight[x, z];

        /*
         * Height 0 = River.
         * River 바닥은 y=0 아래에 있으므로
         * y=0은 물이 들어갈 수 있는 공간.
         */
        if (height == 0)
            return false;

        /*
         * Height 1이면
         * y=0만 지형.
         *
         * Height 3이면
         * y=0,1,2가 지형.
         */
        return y < height;
    }


    // ============================================================
    // Water Visual
    // ============================================================

    private void UpdateWaterVisual()
    {
        HashSet<Vector3Int> activeCells =
            new HashSet<Vector3Int>();


        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    float amount =
                        waterGrid[x, y, z];


                    if (amount <= renderThreshold)
                        continue;


                    Vector3Int gridPosition =
                        new Vector3Int(
                            x,
                            y,
                            z
                        );


                    activeCells.Add(
                        gridPosition
                    );


                    if (
                        waterObjects.TryGetValue(
                            gridPosition,
                            out GameObject existing
                        )
                    )
                    {
                        UpdateWaterHeight(
                            existing,
                            amount
                        );

                        continue;
                    }


                    if (waterBlockPrefab == null)
                        continue;


                    GameObject water =
                        Instantiate(
                            waterBlockPrefab,
                            GridToWorld(gridPosition),
                            Quaternion.identity,
                            transform
                        );


                    waterObjects.Add(
                        gridPosition,
                        water
                    );


                    UpdateWaterHeight(
                        water,
                        amount
                    );
                }
            }
        }


        /*
         * 사라진 물 제거.
         */
        List<Vector3Int> removeList =
            new List<Vector3Int>();


        foreach (
            KeyValuePair<
                Vector3Int,
                GameObject
            > pair
            in waterObjects
        )
        {
            if (!activeCells.Contains(pair.Key))
            {
                if (pair.Value != null)
                    Destroy(pair.Value);

                removeList.Add(
                    pair.Key
                );
            }
        }


        foreach (Vector3Int position in removeList)
        {
            waterObjects.Remove(
                position
            );
        }
    }


    // ============================================================
    // 물 높이
    // ============================================================

    private void UpdateWaterHeight(
        GameObject water,
        float amount
    )
    {
        if (water == null)
            return;


        /*
         * 물의 기준 위치.
         */
        Vector3 position =
            GridToWorld(
                WorldToGrid(
                    water.transform.position
                )
            );


        Vector3 scale =
            water.transform.localScale;


        scale.y =
            Mathf.Max(
                amount * cellSize,
                0.001f
            );


        water.transform.localScale =
            scale;


        /*
         * 물은 바닥에서 위로 올라오도록 한다.
         */
        position.y =
            position.y +
            (scale.y * 0.5f);


        water.transform.position =
            position;
    }


    // ============================================================
    // 좌표 변환
    // ============================================================

    private Vector3Int WorldToGrid(
        Vector3 worldPosition
    )
    {
        return new Vector3Int(
            Mathf.RoundToInt(
                worldPosition.x /
                cellSize
            ),

            Mathf.RoundToInt(
                worldPosition.y /
                cellSize
            ),

            Mathf.RoundToInt(
                worldPosition.z /
                cellSize
            )
        );
    }


    private Vector3 GridToWorld(
        Vector3Int gridPosition
    )
    {
        return new Vector3(
            gridPosition.x * cellSize,
            gridPosition.y * cellSize,
            gridPosition.z * cellSize
        );
    }


    // ============================================================
    // 범위 검사
    // ============================================================

    private bool IsInside(
        Vector3Int position
    )
    {
        return
            position.x >= 0 &&
            position.x < sizeX &&

            position.y >= 0 &&
            position.y < sizeY &&

            position.z >= 0 &&
            position.z < sizeZ;
    }


    private bool IsInsideY(
        int y
    )
    {
        return
            y >= 0 &&
            y < sizeY;
    }


    private bool IsInsideXZ(
        int x,
        int z
    )
    {
        return
            x >= 0 &&
            x < sizeX &&

            z >= 0 &&
            z < sizeZ;
    }


    private bool IsInsideXZ(
        Vector2Int position
    )
    {
        return IsInsideXZ(
            position.x,
            position.y
        );
    }
}