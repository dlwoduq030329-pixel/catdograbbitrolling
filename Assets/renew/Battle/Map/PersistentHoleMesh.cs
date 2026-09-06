using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class PersistentHoleMesh : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Plane")]
    [SerializeField] private float width = 50f;
    [SerializeField] private float length = 50f;

    [Tooltip("Grid 한 칸의 크기")]
    [SerializeField] private float cellSize = 1f;

    [Header("Hole")]
    [Tooltip("플레이어 주변에 뚫리는 맨해튼 거리")]
    [SerializeField] private int holeRadius = 3;

    [Header("Optimization")]
    [Tooltip("플레이어가 이 거리 이상 이동했을 때만 갱신")]
    [SerializeField] private float updateDistance = 0.5f;

    private Mesh mesh;
    private MeshCollider meshCollider;

    private int gridX;
    private int gridZ;

    // 지금까지 뚫린 모든 Cell
    private HashSet<Vector2Int> holes =
        new HashSet<Vector2Int>();

    private Vector3 lastPlayerPosition;

    private void Awake()
    {
        meshCollider = GetComponent<MeshCollider>();

        gridX = Mathf.RoundToInt(width / cellSize);
        gridZ = Mathf.RoundToInt(length / cellSize);

        if (player != null)
            lastPlayerPosition = player.position;

        GenerateMesh();
    }

    private void Update()
    {
        if (player == null)
            return;

        if (Vector3.Distance(
                player.position,
                lastPlayerPosition) < updateDistance)
        {
            return;
        }

        AddHoleAroundPlayer();

        lastPlayerPosition = player.position;
    }

    private void AddHoleAroundPlayer()
    {
        Vector2Int playerGrid =
            WorldToGrid(player.position);

        bool changed = false;

        for (int z = -holeRadius; z <= holeRadius; z++)
        {
            for (int x = -holeRadius; x <= holeRadius; x++)
            {
                // 맨해튼 거리
                int distance =
                    Mathf.Abs(x) +
                    Mathf.Abs(z);

                if (distance > holeRadius)
                    continue;

                Vector2Int cell =
                    playerGrid +
                    new Vector2Int(x, z);

                // Plane 범위 밖이면 무시
                if (cell.x < 0 ||
                    cell.x >= gridX ||
                    cell.y < 0 ||
                    cell.y >= gridZ)
                {
                    continue;
                }

                if (holes.Add(cell))
                {
                    changed = true;
                }
            }
        }

        if (changed)
            GenerateMesh();
    }

    private void GenerateMesh()
    {
        if (mesh != null)
            Destroy(mesh);

        mesh = new Mesh();
        mesh.name = "Player Follow Hole Mesh";
        mesh.indexFormat = IndexFormat.UInt32;

        List<Vector3> vertices =
            new List<Vector3>();

        List<int> triangles =
            new List<int>();

        int vertexIndex = 0;

        for (int z = 0; z < gridZ; z++)
        {
            for (int x = 0; x < gridX; x++)
            {
                Vector2Int cell =
                    new Vector2Int(x, z);

                // 이미 뚫린 영역이면 생성하지 않음
                if (holes.Contains(cell))
                    continue;

                float x0 =
                    x * cellSize -
                    width * 0.5f;

                float x1 =
                    (x + 1) * cellSize -
                    width * 0.5f;

                float z0 =
                    z * cellSize -
                    length * 0.5f;

                float z1 =
                    (z + 1) * cellSize -
                    length * 0.5f;

                vertices.Add(
                    new Vector3(x0, 0, z0)
                );

                vertices.Add(
                    new Vector3(x1, 0, z0)
                );

                vertices.Add(
                    new Vector3(x1, 0, z1)
                );

                vertices.Add(
                    new Vector3(x0, 0, z1)
                );

                triangles.Add(vertexIndex + 0);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 1);

                triangles.Add(vertexIndex + 0);
                triangles.Add(vertexIndex + 3);
                triangles.Add(vertexIndex + 2);

                vertexIndex += 4;
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    private Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector3 local =
            transform.InverseTransformPoint(
                worldPosition
            );

        int x = Mathf.FloorToInt(
            (local.x + width * 0.5f) /
            cellSize
        );

        int z = Mathf.FloorToInt(
            (local.z + length * 0.5f) /
            cellSize
        );

        return new Vector2Int(x, z);
    }

    public void ClearAllHoles()
    {
        holes.Clear();
        GenerateMesh();
    }

    public int GetHoleCount()
    {
        return holes.Count;
    }
}