using System.Collections.Generic;
using UnityEngine;

/// <summary>이동 후보 타일의 위험도와 추격 Enemy의 예상 도착 타일을 원형 표식으로 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleThreatTileMarkerView : MonoBehaviour
{
    [SerializeField] private Color attackRiskColor = new Color(1f, 0.12f, 0.05f, 0.95f);
    [SerializeField] private Color chaseRiskColor = new Color(1f, 0.55f, 0.08f, 0.9f);
    [SerializeField, Min(0.1f)] private float baseMarkerRadius = 0.42f;
    [SerializeField, Min(0.01f)] private float baseMarkerWidth = 0.06f;
    [SerializeField, Min(0f)] private float markerHeight = 0.08f;
    [SerializeField, Range(8, 64)] private int circleSegments = 24;

    private readonly List<LineRenderer> markerPool = new List<LineRenderer>();
    private Material sharedMarkerMaterial;

    /// <summary>
    /// Player 후보 타일은 공격이 하나라도 있으면 빨강, 추격만 있으면 주황으로 표시한다.
    /// 이어서 중복을 제거한 각 추격 Enemy의 예상 도착 타일도 작은 주황 원으로 표시한다.
    /// </summary>
    public void Show(MapInfo playerDestination, IReadOnlyList<EnemyThreatPreviewData> threats)
    {
        List<MarkerRequest> requests = BuildMarkerRequests(playerDestination, threats);
        EnsureMarkerCount(requests.Count);
        for (int i = 0; i < markerPool.Count; i++)
        {
            markerPool[i].enabled = i < requests.Count;
            if (i < requests.Count) DrawCircle(markerPool[i], requests[i]);
        }
    }

    public void HideAll()
    {
        foreach (LineRenderer marker in markerPool)
            if (marker != null) marker.enabled = false;
    }

    private List<MarkerRequest> BuildMarkerRequests(MapInfo playerDestination, IReadOnlyList<EnemyThreatPreviewData> threats)
    {
        List<MarkerRequest> requests = new List<MarkerRequest>();
        if (playerDestination == null || threats == null || threats.Count == 0) return requests;

        bool includesAttack = false;
        HashSet<MapInfo> chaseDestinations = new HashSet<MapInfo>();
        foreach (EnemyThreatPreviewData threat in threats)
        {
            includesAttack |= threat.Intent == EnemyThreatIntent.Attack;
            if (threat.Intent == EnemyThreatIntent.Chase && threat.EnemyPredictedDestination != null)
                chaseDestinations.Add(threat.EnemyPredictedDestination);
        }

        float dangerScale = 1f + Mathf.Min(0.5f, (threats.Count - 1) * 0.1f);
        requests.Add(new MarkerRequest(playerDestination, includesAttack ? attackRiskColor : chaseRiskColor, dangerScale));
        foreach (MapInfo destination in chaseDestinations)
            requests.Add(new MarkerRequest(destination, chaseRiskColor, 0.72f));
        return requests;
    }

    private void DrawCircle(LineRenderer marker, MarkerRequest request)
    {
        int segments = Mathf.Max(8, circleSegments);
        marker.positionCount = segments + 1;
        marker.startWidth = marker.endWidth = baseMarkerWidth * request.Scale;
        marker.startColor = marker.endColor = request.Color;
        Vector3 center = request.Tile.transform.position + Vector3.up * markerHeight;
        float radius = baseMarkerRadius * request.Scale;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            marker.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }

    private void EnsureMarkerCount(int requiredCount)
    {
        if (sharedMarkerMaterial == null) sharedMarkerMaterial = new Material(Shader.Find("Sprites/Default"));
        while (markerPool.Count < requiredCount)
        {
            GameObject markerObject = new GameObject("Enemy Threat Tile Marker");
            markerObject.transform.SetParent(transform, false);
            LineRenderer marker = markerObject.AddComponent<LineRenderer>();
            marker.useWorldSpace = true;
            marker.loop = true;
            marker.material = sharedMarkerMaterial;
            marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marker.receiveShadows = false;
            markerPool.Add(marker);
        }
    }

    private void OnDestroy()
    {
        if (sharedMarkerMaterial != null) Destroy(sharedMarkerMaterial);
    }

    private readonly struct MarkerRequest
    {
        public readonly MapInfo Tile;
        public readonly Color Color;
        public readonly float Scale;

        public MarkerRequest(MapInfo tile, Color color, float scale)
        {
            Tile = tile;
            Color = color;
            Scale = scale;
        }
    }
}
