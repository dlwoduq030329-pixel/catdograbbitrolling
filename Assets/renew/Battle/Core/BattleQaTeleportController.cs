using UnityEngine;

/// <summary>
/// 동적으로 생성된 전투 맵에서 상점과 상자 이벤트를 빠르게 확인하기 위한 Editor 전용 QA 입력이다.
/// F6은 가장 가까운 상점, F7은 가장 가까운 상자 타일로 Player를 옮긴 뒤 기존 이벤트 진입 함수를 호출한다.
/// 실제 게임 이동 규칙이나 MapGenerator는 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleQaTeleportController : MonoBehaviour
{
    private const KeyCode TeleportToStoreKey = KeyCode.F6;
    private const KeyCode TeleportToChestKey = KeyCode.F7;

    private BattleGameManager battleGameManager;

    /// <summary>QA 대상 Player와 상점·상자 시스템을 제공하는 전투 Manager를 연결한다.</summary>
    public void Attach(BattleGameManager manager)
    {
        battleGameManager = manager;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(TeleportToStoreKey))
        {
            TeleportPlayerAndOpenEvent(TileType.Store);
        }
        else if (Input.GetKeyDown(TeleportToChestKey))
        {
            TeleportPlayerAndOpenEvent(TileType.Box);
        }
#endif
    }

    /// <summary>
    /// 현재 Player에서 가장 가까운 요청 타입 타일을 찾아 Player 위치와 Registry 점유 정보를 갱신한다.
    /// 이동 직후 상점 또는 상자의 기존 공개 진입 함수를 호출해 실제 UI 흐름도 함께 검증한다.
    /// </summary>
    private void TeleportPlayerAndOpenEvent(TileType targetTileType)
    {
        if (battleGameManager == null || battleGameManager.CurrentPlayer == null)
        {
            Debug.LogWarning("[QA Teleport] 등록된 Player가 없어 텔레포트할 수 없습니다.", this);
            return;
        }

        if (battleGameManager.IsBattleBlockingUiOpen)
        {
            Debug.LogWarning("[QA Teleport] 열려 있는 상점 또는 상자를 먼저 닫아주세요.", this);
            return;
        }

        BattleMapRegistry mapRegistry = FindFirstObjectByType<BattleMapRegistry>();
        if (mapRegistry == null || mapRegistry.Tiles == null || mapRegistry.Tiles.Count == 0)
        {
            Debug.LogWarning("[QA Teleport] 생성 완료된 전투 타일 Registry가 없습니다.", this);
            return;
        }

        GameObject player = battleGameManager.CurrentPlayer;
        MapInfo targetTile = FindClosestTileOfType(mapRegistry, player.transform.position, targetTileType);
        if (targetTile == null)
        {
            Debug.LogWarning($"[QA Teleport] {targetTileType} 타입 타일을 찾지 못했습니다.", this);
            return;
        }

        MapInfo currentTile = mapRegistry.FindClosestTile(player.transform.position);
        float playerHeightAboveTile = currentTile != null
            ? player.transform.position.y - currentTile.transform.position.y
            : 0f;

        player.transform.position = targetTile.transform.position + Vector3.up * playerHeightAboveTile;
        mapRegistry.SetOccupiedTile(player, targetTile);

        if (targetTileType == TileType.Store)
        {
            battleGameManager.CardShopSystem?.TryEnter(targetTile);
        }
        else if (targetTileType == TileType.Box)
        {
            battleGameManager.ChestRewardSystem?.TryOpen(targetTile);
        }

        Debug.Log($"[QA Teleport] Player를 {targetTile.name} ({targetTileType}) 타일로 이동했습니다.", targetTile);
    }

    /// <summary>등록된 타일 중 요청 타입이면서 Player의 현재 XZ 위치와 가장 가까운 타일을 반환한다.</summary>
    private static MapInfo FindClosestTileOfType(
        BattleMapRegistry mapRegistry,
        Vector3 playerWorldPosition,
        TileType targetTileType)
    {
        MapInfo closestTile = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (MapInfo tile in mapRegistry.Tiles)
        {
            if (tile == null || tile.Type != targetTileType)
            {
                continue;
            }

            Vector3 difference = tile.transform.position - playerWorldPosition;
            difference.y = 0f;
            float sqrDistance = difference.sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closestTile = tile;
        }

        return closestTile;
    }
}
