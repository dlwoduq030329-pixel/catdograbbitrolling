using UnityEngine;

/// <summary>
/// 선택한 이동 목적지 위에 화살표 프리팹을 생성하고 표시 상태를 관리한다.
/// 목적지 선택과 이동 확정은 처리하지 않는다.
/// 화살표는 최초 1회만 Instantiate하고 이후에는 SetActive로 껐다 켜며 재사용한다
/// (Show를 여러 번 호출해도 화살표가 계속 새로 생성되지 않는다).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleMovePreview : MonoBehaviour
{
    private GameObject arrowPrefab;
    private Vector3 arrowOffset;
    private GameObject arrowInstance;

    /// <summary>목적지 안내에 사용할 화살표 Prefab과 타일 기준 위치 보정값을 설정한다.
    /// 이미 생성해둔 화살표 인스턴스가 있는데 Prefab이 바뀌면(기획 변경 등) 옛 인스턴스를 지우고
    /// 다음 Show() 호출 때 새 Prefab으로 다시 만들게 한다.</summary>
    public void SetArrowPrefab(GameObject targetArrowPrefab, Vector3 targetArrowOffset)
    {
        if (arrowPrefab != targetArrowPrefab && arrowInstance != null)
        {
            Destroy(arrowInstance);
            arrowInstance = null;
        }

        arrowPrefab = targetArrowPrefab;
        arrowOffset = targetArrowOffset;
    }

    /// <summary>선택 목적 타일 위에 화살표를 표시한다. 화살표 인스턴스가 이미 있으면 새로 만들지
    /// 않고 위치만 옮겨서 재사용한다(없을 때만 Instantiate 1회 발생).</summary>
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

    /// <summary>목적지 선택 취소·확정·턴 종료 시 화살표를 비활성화한다. Destroy하지 않으므로
    /// 다음 Show() 호출 때 재사용된다.</summary>
    public void Hide()
    {
        if (arrowInstance != null)
        {
            arrowInstance.SetActive(false);
        }
    }
}
