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

            // 굵은 시작점이 Enemy 쪽에 오도록 position 0을 항상 Enemy로 유지한다.
            lines[i].SetPosition(0, threat.Enemy.transform.position + Vector3.up * lineHeight);
            lines[i].SetPosition(1, lineDestination.transform.position + Vector3.up * lineHeight + endpointOffset);
            Color color = threat.Intent == EnemyThreatIntent.Attack ? attackColor : chaseColor;
            lines[i].startColor = lines[i].endColor = color;
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
