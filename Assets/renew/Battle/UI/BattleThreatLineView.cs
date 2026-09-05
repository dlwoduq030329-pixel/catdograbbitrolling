using System.Collections.Generic;
using UnityEngine;

/// <summary>Enemy 의도별 연결선의 생성·재사용·시작점 강조·끝점 분산만 담당한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleThreatLineView : MonoBehaviour
{
    [SerializeField] private Color attackColor = new Color(1f, 0.15f, 0.08f, 0.95f);
    [SerializeField] private Color chaseColor = new Color(1f, 0.55f, 0.08f, 0.9f);
    [SerializeField, Min(0.01f)] private float enemyStartWidth = 0.18f;
    [SerializeField, Min(0.01f)] private float destinationEndWidth = 0.045f;
    [SerializeField] private float lineHeight = 0.65f;
    [SerializeField, Min(0f)] private float sharedDestinationSpread = 0.35f;

    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private Material sharedMaterial;

    public void Show(IReadOnlyList<EnemyThreatPreviewData> threats)
    {
        int count = threats != null ? threats.Count : 0;
        EnsureLineCount(count);
        Dictionary<MapInfo, int> destinationCounts = CountSharedDestinations(threats);
        Dictionary<MapInfo, int> usedDestinationSlots = new Dictionary<MapInfo, int>();

        for (int i = 0; i < lines.Count; i++)
        {
            bool visible = i < count;
            lines[i].enabled = visible;
            if (!visible) continue;

            EnemyThreatPreviewData threat = threats[i];
            MapInfo lineDestination = threat.Intent == EnemyThreatIntent.Attack
                ? threat.PlayerDestination
                : threat.EnemyPredictedDestination;
            if (threat.Enemy == null || lineDestination == null)
            {
                lines[i].enabled = false;
                continue;
            }

            int slot = usedDestinationSlots.TryGetValue(lineDestination, out int used) ? used : 0;
            usedDestinationSlots[lineDestination] = slot + 1;
            int sharedCount = destinationCounts.TryGetValue(lineDestination, out int total) ? total : 1;
            Vector3 endpointOffset = CalculateRadialOffset(slot, sharedCount);

            LineRenderer line = lines[i];

            // 2026-09-05: 예전에는 Enemy와 목적지, 딱 2점만 직선으로 이었다. 맵에 단차(높이 차이)가
            // 생긴 뒤로는 그 직선이 두 타일 사이의 지형을 그대로 뚫고 지나가서 이상하게 떠 보이거나
            // 파묻힌 것처럼 보였다("선이 엉뚱한 데 위치함" 피드백). PathDebugView(AI 경로 디버그 선)와
            // 같은 방식으로, 계획에 이미 계산돼 있는 경로(threat.Plan.Path)의 타일마다 점을 찍어서
            // 실제 지형 단차를 따라 꺾이는 선으로 그린다. 겹치는 목적지를 위한 endpointOffset은
            // 실제 도착 지점인 마지막 점에만 적용한다.
            IReadOnlyList<MapInfo> path = threat.Plan != null ? threat.Plan.Path : null;
            int fullPathTileCount = path != null ? path.Count : 0;

            // 2026-09-05: 추격(Move) 계획의 threat.Plan.Path는 "목표 타일까지의 전체 최단 경로"이지,
            // 이번 턴에 실제로 이동하는 칸 수가 아니다(공격 사거리·MP 때문에 도중에 멈춘다). 여기서
            // 전체 경로를 그대로 그리면 실제로는 몇 칸만 가고 멈추는 Enemy인데도 경고선이 거의
            // 플레이어 타일까지 쭉 이어져 보였다("추격선이 실제 정지 위치보다 훨씬 더 나가서 그려짐").
            // PlannedMoveTileCount(이번 턴에 실제로 이동할 칸 수, EnemyTurnPlanner가 계산)까지만 잘라서
            // 그리면 마지막 점이 정확히 EnemyPredictedDestination과 같은 타일이 된다. 공격(Attack)
            // 계획은 애초에 Path 전체 길이가 공격 사거리 이내라서 자르면 오히려 목적지에 못 미치게
            // 짧아지므로 자르지 않는다.
            int pathTileCount = threat.Intent == EnemyThreatIntent.Attack || threat.Plan == null
                ? fullPathTileCount
                : Mathf.Min(fullPathTileCount, threat.Plan.PlannedMoveTileCount);

            if (pathTileCount == 0)
            {
                // 계획에 경로가 없으면(예: 이동 없이 바로 공격) 예전처럼 Enemy-목적지 2점만 잇는다.
                line.positionCount = 2;
                line.SetPosition(0, threat.Enemy.transform.position + Vector3.up * lineHeight);
                line.SetPosition(1, lineDestination.transform.position + Vector3.up * lineHeight + endpointOffset);
            }
            else
            {
                line.positionCount = pathTileCount + 1;
                // 굵은 시작점이 Enemy 쪽에 오도록 position 0을 항상 Enemy로 유지한다.
                line.SetPosition(0, threat.Enemy.transform.position + Vector3.up * lineHeight);
                for (int p = 0; p < pathTileCount; p++)
                {
                    MapInfo tile = path[p];
                    Vector3 point = (tile != null ? tile.transform.position : lineDestination.transform.position)
                        + Vector3.up * lineHeight;
                    if (p == pathTileCount - 1)
                    {
                        // 마지막 점 = 실제 표시 목적지(Attack은 PlayerDestination, Move는 위에서 자른
                        // pathTileCount 덕분에 이 tile이 곧 EnemyPredictedDestination과 같은 타일이
                        // 된다). 여러 Enemy가 같은 칸을 노릴 때 서로 겹치지 않도록 여기에만 방사형
                        // 분산 오프셋을 더한다.
                        point += endpointOffset;
                    }

                    line.SetPosition(p + 1, point);
                }
            }

            Color color = threat.Intent == EnemyThreatIntent.Attack ? attackColor : chaseColor;
            line.startColor = line.endColor = color;
        }
    }

    public void HideAll()
    {
        foreach (LineRenderer line in lines) if (line != null) line.enabled = false;
    }

    private Dictionary<MapInfo, int> CountSharedDestinations(IReadOnlyList<EnemyThreatPreviewData> threats)
    {
        Dictionary<MapInfo, int> counts = new Dictionary<MapInfo, int>();
        if (threats == null) return counts;
        foreach (EnemyThreatPreviewData threat in threats)
        {
            MapInfo destination = threat.Intent == EnemyThreatIntent.Attack
                ? threat.PlayerDestination
                : threat.EnemyPredictedDestination;
            if (destination == null) continue;
            counts[destination] = counts.TryGetValue(destination, out int count) ? count + 1 : 1;
        }
        return counts;
    }

    private Vector3 CalculateRadialOffset(int slot, int count)
    {
        if (count <= 1 || sharedDestinationSpread <= 0f) return Vector3.zero;
        float angle = Mathf.PI * 2f * slot / count;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * sharedDestinationSpread;
    }

    private void EnsureLineCount(int requiredCount)
    {
        if (sharedMaterial == null) sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        while (lines.Count < requiredCount)
        {
            GameObject child = new GameObject("Enemy Threat Line");
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = enemyStartWidth;
            line.endWidth = destinationEndWidth;
            line.material = sharedMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lines.Add(line);
        }
    }

    private void OnDestroy()
    {
        if (sharedMaterial != null) Destroy(sharedMaterial);
    }
}
