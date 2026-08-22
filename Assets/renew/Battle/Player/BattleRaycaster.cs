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

    // 화면 좌표 -> 3D Ray로 쏠 때 최대 사거리. 실제 카메라-맵 사이 거리를 재서 나온 값이 아니라,
    // "이 배틀 씬에서 카메라가 아무리 멀어져도 확실히 다 닿을 만큼 넉넉한" 안전 기본값이다.
    // 같은 폴더의 BattleUnitHoverHighlighter도 동일하게 500f를 기본값으로 쓰는 프로젝트 관례이며,
    // Inspector에서 [Min(1f)]로 노출돼 있어 특정 Scene 카메라 세팅이 유별나게 멀면 그때 조정하면 된다.
    [SerializeField, Min(1f)] private float rayDistance = 500f;

    /// <summary>화면 좌표 판정에 사용할 카메라, Player와 맵 타일 LayerMask를 한 번에 연결한다.
    /// 매 프레임 EnsureBattleRaycaster()에서 호출되어 참조를 최신 상태로 유지한다.</summary>
    public void AttachReferences(Camera camera, GameObject targetPlayer, LayerMask mapTileLayerMask)
    {
        raycastCamera = camera;
        player = targetPlayer;
        tileLayerMask = mapTileLayerMask;
    }

    /// <summary>현재 클릭 대상으로 판정할 Player Body 참조만 갱신한다(카메라·LayerMask는 그대로 유지).</summary>
    public void SetPlayer(GameObject targetPlayer)
    {
        player = targetPlayer;
    }

    /// <summary>
    /// 화면 좌표(마우스 클릭 등)에서 등록된 Player 본체(playerBody) 또는 그 자식 Collider에 맞았는지 판정한다.
    /// LayerMask 제한 없이(~0 = 전체 Layer) 쏘고, 맞은 Collider의 Transform이 player 자신이거나
    /// player의 자식이면(IsChildOf) Player를 클릭한 것으로 본다. 순서 있는 히트 배열을 순회하다 첫
    /// 매칭에서 바로 반환하므로 "Player보다 앞에 다른 오브젝트가 겹쳐 있어도" 그 히트만 건너뛰고 계속 찾는다.
    /// </summary>
    public bool TryGetPlayer(Vector2 screenPosition, out GameObject clickedPlayer)
    {
        clickedPlayer = null;
        if (raycastCamera == null || player == null)
        {
            return false;
        }

        RaycastHit[] hits = RaycastFromScreenPoint(screenPosition, ~0);
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

    /// <summary>
    /// 화면 좌표에서 맞은 Collider의 부모 계층을 타고 올라가(GetComponentInParent) 활성 EnemyTurnActor를 찾는다.
    /// TryGetPlayer와 달리 "누구의 자식인지" 미리 알 필요가 없어 GetComponentInParent로 역탐색한다.
    /// 히트 하나당 EnemyTurnActor 하나만 나올 수 있으므로 반환값은 항상 단일 EnemyTurnActor이고,
    /// 배열(RaycastHit[])은 "화면 좌표에 겹쳐 있는 여러 Collider 후보"를 순서대로 검사하기 위한 중간 결과일 뿐이다.
    /// 비활성(activeInHierarchy == false) Enemy는 후보에서 제외하고 다음 히트를 계속 검사한다.
    /// </summary>
    public bool TryGetEnemy(Vector2 screenPosition, out EnemyTurnActor enemy)
    {
        enemy = null;
        if (raycastCamera == null)
        {
            return false;
        }

        RaycastHit[] hits = RaycastFromScreenPoint(screenPosition, ~0);
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

    /// <summary>
    /// 화면 좌표에서 tileLayerMask에 해당하는 Layer의 Collider만 골라 그 부모에서 MapInfo를 찾는다.
    /// TryGetPlayer/TryGetEnemy와 달리 처음부터 tileLayerMask로 제한해서 쏘기 때문에, 애초에 타일이
    /// 아닌 다른 Layer의 오브젝트는 히트 자체에 안 잡힌다(Player/Enemy Layer가 타일과 겹쳐도 안전).
    ///
    /// 참고: BattleTileLocator.FindClosestXZ/BattleMapTraversalService 등은 "이미 아는 3D 월드 좌표"에서
    /// 가장 가까운 타일을 찾는 순수 계산이고, 여기 TryGetMapTile은 "아직 3D 위치를 모르는 화면 클릭 좌표"에서
    /// 실제로 그 아래 어떤 타일이 있는지 Physics Raycast로 알아내는 것이라 용도가 다르다(입력 판정 vs 좌표 계산).
    /// </summary>
    public bool TryGetMapTile(Vector2 screenPosition, out MapInfo tile)
    {
        tile = null;
        if (raycastCamera == null)
        {
            return false;
        }

        RaycastHit[] hits = RaycastFromScreenPoint(screenPosition, tileLayerMask);
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

    /// <summary>
    /// 화면 좌표를 카메라 기준 3D Ray로 바꿔 Physics.RaycastAll을 실행하는 공통 구현. 위 3개의
    /// TryGet* 메서드가 전부 이 메서드 하나로 수렴해서(중복 Raycast 코드 방지), 여기서만 쓰이는
    /// private 헬퍼다 — 외부(ActionController, BattleMoveThreatPreview 등)는 TryGetPlayer/TryGetEnemy/
    /// TryGetMapTile만 호출하고 이 메서드를 직접 호출하지 않는다. UnityEngine.Physics.RaycastAll을
    /// 그대로 감싼 것뿐이라 이름이 겹쳐 보이지만 이 클래스 안의 별개 private 메서드다.
    /// </summary>
    private RaycastHit[] RaycastFromScreenPoint(Vector2 screenPosition, int layerMask)
    {
        Ray ray = raycastCamera.ScreenPointToRay(screenPosition);
        return Physics.RaycastAll(
            ray,
            rayDistance,
            layerMask,
            QueryTriggerInteraction.Collide);
    }
}
