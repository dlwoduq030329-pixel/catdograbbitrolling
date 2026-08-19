using UnityEngine;

/// <summary>
/// 선택한 이동 목적지 위에 화살표 프리팹을 생성하고 표시 상태를 관리한다.
/// 목적지 선택과 이동 확정은 처리하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleMovePreview : MonoBehaviour
{
    private GameObject arrowPrefab;
    private Vector3 arrowOffset;
    private GameObject arrowInstance;

    /// <summary>목적지 안내에 사용할 화살표 Prefab과 타일 기준 위치 보정값을 설정한다.</summary>
    public void Configure(GameObject targetArrowPrefab, Vector3 targetArrowOffset)
    {
        if (arrowPrefab != targetArrowPrefab && arrowInstance != null)
        {
            Destroy(arrowInstance);
            arrowInstance = null;
        }

        arrowPrefab = targetArrowPrefab;
        arrowOffset = targetArrowOffset;
    }

    /// <summary>선택 목적 타일 위에 화살표를 하나만 생성 또는 재사용하여 표시한다.</summary>
    public void Show(MapInfo targetTile)
    {
        if (targetTile == null || arrowPrefab == null)
        {
            Hide();
            return;
        }

        if (arrowInstance == null)
        {
            arrowInstance = Instantiate(arrowPrefab);
            arrowInstance.name = "MoveArrowPreview";
        }

        arrowInstance.transform.SetPositionAndRotation(
            targetTile.transform.position + arrowOffset,
            Quaternion.identity);
        arrowInstance.SetActive(true);
    }

    /// <summary>목적지 선택 취소·확정·턴 종료 시 화살표를 비활성화한다.</summary>
    public void Hide()
    {
        if (arrowInstance != null)
        {
            arrowInstance.SetActive(false);
        }
    }
}
