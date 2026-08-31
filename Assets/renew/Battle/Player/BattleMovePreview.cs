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
    [Header("이동 목적지 화살표")]
    [Tooltip("선택한 이동 타일 위에 표시할 화살표 Prefab입니다.")]
    [SerializeField] private GameObject moveArrowPrefab;
    [Tooltip("선택한 타일의 월드 Y 위치에서 화살표를 띄울 높이입니다.")]
    [SerializeField, Min(0f)] private float arrowHeightAboveTile = 1f;

    private GameObject arrowInstance;

    /// <summary>선택 목적 타일 위에 화살표를 표시한다. 화살표 인스턴스가 이미 있으면 새로 만들지
    /// 않고 위치만 옮겨서 재사용한다(없을 때만 Instantiate 1회 발생).</summary>
    public void Show(MapInfo targetTile)
    {
        if (targetTile == null)
        {
            Hide();
            return;
        }

        if (moveArrowPrefab == null)
        {
            Debug.LogError("BattleMovePreview에 이동 화살표 Prefab이 연결되지 않았습니다.", this);
            return;
        }

        if (arrowInstance == null)
        {
            arrowInstance = Instantiate(moveArrowPrefab);
            arrowInstance.name = "MoveArrowPreview";
        }

        Vector3 tileWorldPosition = targetTile.transform.position;
        Vector3 arrowWorldPosition = new Vector3(
            tileWorldPosition.x,
            tileWorldPosition.y + arrowHeightAboveTile,
            tileWorldPosition.z);

        arrowInstance.transform.SetPositionAndRotation(arrowWorldPosition, Quaternion.identity);
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
