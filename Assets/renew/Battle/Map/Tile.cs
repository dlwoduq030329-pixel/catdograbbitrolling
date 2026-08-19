using UnityEngine;

/// <summary>
/// 격자 좌표, 타일 유형, 이동 및 감지 색상 표시를 관리한다.
/// </summary>
public class Tile : MonoBehaviour
{
    public enum TileType
    {
        [InspectorName("일반")]
        Normal,
        [InspectorName("이동 불가")]
        Blocked,
        [InspectorName("적")]
        Enemy,
        [InspectorName("이벤트")]
        Event,
        [InspectorName("상점")]
        Shop,
        [InspectorName("상호작용")]
        Interactable
    }

    [Header("타일 정보")]
    [InspectorName("격자 좌표")]
    public Vector2Int gridPosition;
    [InspectorName("타일 유형")]
    public TileType tileType = TileType.Normal;

    [Header("타일 색상 표시")]
    [InspectorName("타일 렌더러")]
    [SerializeField] private Renderer tileRenderer;
    [SerializeField] private Color movableColor = new Color(0.25f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color blockedColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color detectColor = new Color(1f, 0.6f, 0.15f, 1f);
    [SerializeField] private Color defaultColor = Color.white;

    /// <summary>렌더러 참조가 비어 있으면 같은 오브젝트에서 자동으로 찾는다.</summary>
    private void Awake()
    {
        if (tileRenderer == null)
        {
            tileRenderer = GetComponent<Renderer>();
        }
    }

    /// <summary>외부 맵 생성기가 타일 격자 좌표를 기록할 때 사용한다.</summary>
    public void SetGridPosition(int x, int y)
    {
        gridPosition = new Vector2Int(x, y);
    }

    /// <summary>이동 가능 여부에 따라 이동/차단 색상을 표시한다.</summary>
    public void SetMovable(bool canMove)
    {
        SetColor(canMove ? movableColor : blockedColor);
    }

    /// <summary>적 감지 범위 포함 여부에 따라 감지 색상 또는 기본 색상을 표시한다.</summary>
    public void SetDetectRange(bool inDetectRange)
    {
        SetColor(inDetectRange ? detectColor : defaultColor);
    }

    /// <summary>외부 시스템이 덮어쓴 타일 색상을 기본 색상으로 되돌린다.</summary>
    public void ResetColor()
    {
        SetColor(defaultColor);
    }

    /// <summary>이동, 공격 등 외부 표시 시스템이 전달한 색상으로 타일을 덮어쓴다.</summary>
    public void SetColorOverride(Color color)
    {
        SetColor(color);
    }

    /// <summary>렌더러 재질이 준비된 경우에만 실제 재질 색상을 변경한다.</summary>
    private void SetColor(Color color)
    {
        if (tileRenderer == null)
        {
            return;
        }

        if (tileRenderer.material != null)
        {
            tileRenderer.material.color = color;
        }
    }
}
