using UnityEngine;

/// <summary>
/// 화면 좌표에서 Player, Enemy, MapInfo를 판별하는 전투 Raycast 전용 모듈이다.
/// 입력 상태와 이동·공격 규칙은 판단하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleRaycaster : MonoBehaviour
{
    [Header("Raycast 참조")]
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private GameObject player;
    [SerializeField] private LayerMask tileLayerMask = ~0;
    [SerializeField, Min(1f)] private float rayDistance = 500f;

    /// <summary>화면 좌표 판정에 사용할 카메라, Player와 맵 타일 LayerMask를 한 번에 연결한다.</summary>
    public void Configure(Camera camera, GameObject targetPlayer, LayerMask mapTileLayerMask)
    {
        raycastCamera = camera;
        player = targetPlayer;
        tileLayerMask = mapTileLayerMask;
    }

    /// <summary>Scene 전환 또는 카메라 교체 시 Raycast 기준 카메라만 갱신한다.</summary>
    public void SetCamera(Camera camera)
    {
        raycastCamera = camera;
    }

    /// <summary>현재 클릭 대상으로 판정할 Player Body 참조를 갱신한다.</summary>
    public void SetPlayer(GameObject targetPlayer)
    {
        player = targetPlayer;
    }

    /// <summary>맵 타일 판정에 허용할 LayerMask를 교체한다.</summary>
    public void SetTileLayerMask(LayerMask mapTileLayerMask)
    {
        tileLayerMask = mapTileLayerMask;
    }

    /// <summary>화면 좌표에서 등록된 Player 본체 또는 자식 Collider를 찾는다.</summary>
    public bool TryGetPlayer(Vector2 screenPosition, out GameObject clickedPlayer)
    {
        clickedPlayer = null;
        if (raycastCamera == null || player == null)
        {
            return false;
        }

        RaycastHit[] hits = RaycastAll(screenPosition, ~0);
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == player.transform || hitTransform.IsChildOf(player.transform))
            {
                clickedPlayer = player;
                return true;
            }
        }

        return false;
    }

    /// <summary>화면 좌표의 Collider 부모에서 활성 EnemyTurnActor를 찾는다.</summary>
    public bool TryGetEnemy(Vector2 screenPosition, out EnemyTurnActor enemy)
    {
        enemy = null;
        if (raycastCamera == null)
        {
            return false;
        }

        RaycastHit[] hits = RaycastAll(screenPosition, ~0);
        foreach (RaycastHit hit in hits)
        {
            enemy = hit.collider.GetComponentInParent<EnemyTurnActor>();
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        enemy = null;
        return false;
    }

    /// <summary>화면 좌표의 타일 Layer Collider 부모에서 MapInfo를 찾는다.</summary>
    public bool TryGetMapTile(Vector2 screenPosition, out MapInfo tile)
    {
        tile = null;
        if (raycastCamera == null)
        {
            return false;
        }

        RaycastHit[] hits = RaycastAll(screenPosition, tileLayerMask);
        foreach (RaycastHit hit in hits)
        {
            tile = hit.collider.GetComponentInParent<MapInfo>();
            if (tile != null)
            {
                return true;
            }
        }

        tile = null;
        return false;
    }

    private RaycastHit[] RaycastAll(Vector2 screenPosition, int layerMask)
    {
        Ray ray = raycastCamera.ScreenPointToRay(screenPosition);
        return Physics.RaycastAll(
            ray,
            rayDistance,
            layerMask,
            QueryTriggerInteraction.Collide);
    }
}
