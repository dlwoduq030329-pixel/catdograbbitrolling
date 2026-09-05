using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R키 디버그 표시에서 AI가 계산한 최단 경로를 Enemy별 LineRenderer로 표시하는 전용 컴포넌트다.
/// Player 이동 범위가 보이는 동안에는 두 표시가 겹치지 않도록 자동으로 숨긴다.
/// 실제 이동, 경로 계산 또는 MP 차감에는 관여하지 않는다.
/// </summary>
public class PathDebugView : MonoBehaviour
{
    [Header("인공지능 경로 디버그")]
    [InspectorName("경로 표시")]
    [SerializeField] private bool showPath = true;
    [InspectorName("경로 색상")]
    [SerializeField] private Color pathColor = Color.red;
    [InspectorName("경로 높이")]
    [SerializeField] private float pathHeight = 0.35f;
    [InspectorName("경로 두께")]
    [SerializeField] private float pathWidth = 0.08f;
    [InspectorName("선 재질 (비우면 기본 재질 사용)")]
    [Tooltip("비워두면 예전처럼 Sprites/Default(안티앨리어싱 없는 각진 재질)로 자동 대체된다. " +
             "글로우·그라디언트가 있는 재질을 여기 꽂으면 선이 거칠어 보이는 문제를 없앨 수 있다.")]
    [SerializeField] private Material lineMaterial;

    private LineRenderer lineRenderer;
    private bool hasPath;

    /// <summary>활성화될 때 플레이어 이동 범위 표시 이벤트를 구독한다.</summary>
    private void OnEnable()
    {
        BattleRangeVisibilityTracker.VisibilityChanged += HandleMoveRangeVisibilityChanged;
        UpdateLineVisibility();
    }

    /// <summary>비활성화될 때 이벤트 구독을 해제해 중복 호출을 방지한다.</summary>
    private void OnDisable()
    {
        BattleRangeVisibilityTracker.VisibilityChanged -= HandleMoveRangeVisibilityChanged;
    }

    /// <summary>시작 위치와 MapInfo 경로 중심을 연결해 디버그 선을 갱신한다.</summary>
    public void DrawPath(Vector3 startPosition, IReadOnlyList<MapInfo> path)
    {
        if (!showPath || path == null)
        {
            Clear();
            return;
        }

        EnsureLineRenderer();
        lineRenderer.positionCount = path.Count + 1;
        lineRenderer.SetPosition(0, AddHeight(startPosition));

        for (int i = 0; i < path.Count; i++)
        {
            lineRenderer.SetPosition(i + 1, AddHeight(path[i].transform.position));
        }

        hasPath = true;
        UpdateLineVisibility();
    }

    /// <summary>기존 경로 선과 내부 표시 상태를 초기화한다.</summary>
    public void Clear()
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }

        hasPath = false;
    }

    /// <summary>
    /// 경로선 표시기가 없으면 런타임에 생성하고 디버그용 기본 재질과 굵기를 설정한다.
    /// 최종 구조에서는 Enemy Prefab에 LineRenderer를 직접 연결해 런타임 AddComponent와 Material 생성을 제거한다.
    /// </summary>
    private void EnsureLineRenderer()
    {
        if (lineRenderer != null)
        {
            return;
        }

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = pathWidth;
        lineRenderer.endWidth = pathWidth;
        lineRenderer.startColor = pathColor;
        lineRenderer.endColor = pathColor;
        // 2026-09-05: lineMaterial을 인스펙터에 꽂아두면 그걸 그대로 쓰고, 비어 있을 때만 예전처럼
        // 즉석 기본 재질(Sprites/Default, 안티앨리어싱 없는 각진 Unlit)로 대체한다("선이 거칠어 보임" 피드백).
        lineRenderer.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
    }

    /// <summary>경로선이 타일 표면과 겹치지 않도록 표시 높이를 더한다.</summary>
    private Vector3 AddHeight(Vector3 position)
    {
        position.y += pathHeight;
        return position;
    }

    /// <summary>플레이어 이동 범위 표시가 바뀌면 적 경로선 표시 여부를 다시 계산한다.</summary>
    private void HandleMoveRangeVisibilityChanged(bool isMoveRangeVisible)
    {
        UpdateLineVisibility();
    }

    /// <summary>경로 존재 여부와 플레이어 범위 표시 상태를 조합해 최종 선 표시 여부를 결정한다.</summary>
    private void UpdateLineVisibility()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = hasPath && !BattleRangeVisibilityTracker.IsAnyRangeVisible;
        }
    }
}
