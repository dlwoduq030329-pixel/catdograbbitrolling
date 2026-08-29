using System.Collections.Generic;
using UnityEngine;

public class GridWaterSimulation : MonoBehaviour
{
    [Header("Grid")]
    public int sizeX = 100;
    public int sizeY = 30;
    public int sizeZ = 100;

    public float cellSize = 1f;

    [Header("Simulation")]
    public float simulationInterval = 0.05f;

    [Range(0f, 1f)]
    public float horizontalFlowSpeed = 0.25f;

    [Range(0f, 1f)]
    public float downwardFlowSpeed = 1f;

    [Header("Rendering")]
    public GameObject waterBlockPrefab;

    [Header("Water Source")]

    [Tooltip("Source가 매 시뮬레이션마다 공급하는 물의 양")]
    public float sourceWaterAmount = 1f;


    private bool[,,] solidGrid;
    private float[,,] waterGrid;


    // ============================================================
    // Water Source
    // ============================================================

    private HashSet<Vector3Int> waterSources =
        new HashSet<Vector3Int>();


    private Dictionary<Vector3Int, GameObject> waterObjects =
        new Dictionary<Vector3Int, GameObject>();

    private float timer;


    private void Awake()
    {
        solidGrid =
            new bool[sizeX, sizeY, sizeZ];

        waterGrid =
            new float[sizeX, sizeY, sizeZ];
    }


    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= simulationInterval)
        {
            timer = 0f;


            // ============================================
            // 모든 Water Source에서 물 공급
            // ============================================

            SupplyWaterSources();


            // 물 시뮬레이션

            SimulateWater();


            // 물 표시

            UpdateWaterVisual();
        }
    }


    // ============================================================
    // Water Source 등록
    // ============================================================

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

        waterSources.Add(gridPosition);

        waterGrid[
            gridPosition.x,
            gridPosition.y,
            gridPosition.z
        ] = 1f;
    }


    public void AddWaterSourceGrid(
        Vector3Int gridPosition
    )
    {
        if (!IsInside(gridPosition))
            return;

        waterSources.Add(
            gridPosition
        );


        // Source 위치에 최초 물 생성

        waterGrid[
            gridPosition.x,
            gridPosition.y,
            gridPosition.z
        ] = 1f;
    }


    // ============================================================
    // Water Source 제거
    // ============================================================

    public void RemoveWaterSource(
        Vector3 worldPosition
    )
    {
        Vector3Int gridPosition =
            WorldToGrid(worldPosition);

        waterSources.Remove(
            gridPosition
        );
    }


    // ============================================================
    // 모든 Source에서 물 공급
    // ============================================================

    private void SupplyWaterSources()
    {
        foreach (
            Vector3Int source
            in waterSources
        )
        {
            if (!IsInside(source))
                continue;


            waterGrid[
                source.x,
                source.y,
                source.z
            ] =
                Mathf.Clamp01(
                    waterGrid[
                        source.x,
                        source.y,
                        source.z
                    ]
                    +
                    sourceWaterAmount
                );
        }
    }


    // ============================================================
    // 물 생성
    // ============================================================

    public void AddWater(
        Vector3 worldPosition,
        float amount = 1f
    )
    {
        Vector3Int gridPosition =
            WorldToGrid(worldPosition);

        if (!IsInside(gridPosition))
            return;

        waterGrid[
            gridPosition.x,
            gridPosition.y,
            gridPosition.z
        ] += amount;

        waterGrid[
            gridPosition.x,
            gridPosition.y,
            gridPosition.z
        ] =
            Mathf.Clamp01(
                waterGrid[
                    gridPosition.x,
                    gridPosition.y,
                    gridPosition.z
                ]
            );
    }


    // ============================================================
    // 지형 등록
    // ============================================================

    public void AddSolid(
        Vector3 worldPosition
    )
    {
        Vector3Int gridPosition =
            WorldToGrid(worldPosition);

        if (!IsInside(gridPosition))
            return;

        solidGrid[
            gridPosition.x,
            gridPosition.y,
            gridPosition.z
        ] = true;
    }


    // ============================================================
    // 실제 물 시뮬레이션
    // ============================================================

    private void SimulateWater()
    {
        float[,,] nextWater =
            new float[sizeX, sizeY, sizeZ];


        // 현재 물 상태 복사

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


        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    float currentWater =
                        waterGrid[x, y, z];

                    if (currentWater <= 0.001f)
                        continue;


                    // ------------------------------------------------
                    // 1. 아래로 흐르기
                    // ------------------------------------------------

                    Vector3Int below =
                        new Vector3Int(
                            x,
                            y - 1,
                            z
                        );

                    if (
                        IsInside(below) &&
                        !solidGrid[
                            below.x,
                            below.y,
                            below.z
                        ]
                    )
                    {
                        float availableSpace =
                            1f -
                            waterGrid[
                                below.x,
                                below.y,
                                below.z
                            ];

                        float flow =
                            Mathf.Min(
                                currentWater,
                                availableSpace
                            );

                        flow *= downwardFlowSpeed;

                        nextWater[x, y, z] -= flow;

                        nextWater[
                            below.x,
                            below.y,
                            below.z
                        ] += flow;

                        continue;
                    }


                    // ------------------------------------------------
                    // 2. 아래가 막히면 옆으로 퍼짐
                    // ------------------------------------------------

                    SpreadHorizontal(
                        x,
                        y,
                        z,
                        currentWater,
                        nextWater
                    );
                }
            }
        }


        waterGrid = nextWater;
    }


    // ============================================================
    // 수평 방향으로 물 퍼뜨리기
    // ============================================================

    private void SpreadHorizontal(
        int x,
        int y,
        int z,
        float currentWater,
        float[,,] nextWater
    )
    {
        Vector3Int[] directions =
        {
            Vector3Int.left,
            Vector3Int.right,
            Vector3Int.forward,
            Vector3Int.back
        };


        List<Vector3Int> availableCells =
            new List<Vector3Int>();


        foreach (Vector3Int direction in directions)
        {
            Vector3Int neighbour =
                new Vector3Int(
                    x + direction.x,
                    y,
                    z + direction.z
                );

            if (!IsInside(neighbour))
                continue;

            if (
                solidGrid[
                    neighbour.x,
                    neighbour.y,
                    neighbour.z
                ]
            )
                continue;


            float neighbourWater =
                waterGrid[
                    neighbour.x,
                    neighbour.y,
                    neighbour.z
                ];


            if (neighbourWater < currentWater)
            {
                availableCells.Add(neighbour);
            }
        }


        if (availableCells.Count == 0)
            return;


        float totalFlow =
            currentWater *
            horizontalFlowSpeed;


        float flowPerCell =
            totalFlow /
            availableCells.Count;


        foreach (
            Vector3Int neighbour
            in availableCells
        )
        {
            float availableSpace =
                1f -
                waterGrid[
                    neighbour.x,
                    neighbour.y,
                    neighbour.z
                ];

            float actualFlow =
                Mathf.Min(
                    flowPerCell,
                    availableSpace
                );


            nextWater[x, y, z] -= actualFlow;

            nextWater[
                neighbour.x,
                neighbour.y,
                neighbour.z
            ] += actualFlow;
        }
    }


    // ============================================================
    // 물 표시
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

                    if (amount <= 0.01f)
                        continue;


                    Vector3Int gridPosition =
                        new Vector3Int(x, y, z);

                    activeCells.Add(gridPosition);


                    if (
                        waterObjects.ContainsKey(
                            gridPosition
                        )
                    )
                    {
                        UpdateWaterHeight(
                            waterObjects[gridPosition],
                            amount
                        );

                        continue;
                    }


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
                Destroy(pair.Value);

                removeList.Add(pair.Key);
            }
        }


        foreach (
            Vector3Int position
            in removeList
        )
        {
            waterObjects.Remove(position);
        }
    }


    private void UpdateWaterHeight(
        GameObject water,
        float amount
    )
    {
        Vector3 scale =
            water.transform.localScale;

        scale.y =
            amount * cellSize;

        water.transform.localScale =
            scale;


        Vector3 position =
            water.transform.localPosition;

        position.y =
            position.y -
            (cellSize * 0.5f) +
            (scale.y * 0.5f);

        water.transform.localPosition =
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
                worldPosition.x / cellSize
            ),

            Mathf.RoundToInt(
                worldPosition.y / cellSize
            ),

            Mathf.RoundToInt(
                worldPosition.z / cellSize
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
}