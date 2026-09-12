using UnityEngine;

/// <summary>플레이어 이동이 끝난 뒤 Fog와 도착 타일 이벤트를 갱신한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BattleUnitMoveFlow))]
public sealed class BattlePlayerMoveResultHandler : MonoBehaviour
{
    private BattleUnitMoveFlow moveFlow;

    private void Awake()
    {
        moveFlow = GetComponent<BattleUnitMoveFlow>();
        if (moveFlow == null)
        {
            Debug.LogError("이동 완료 처리를 위한 BattleUnitMoveFlow가 없습니다.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (moveFlow == null)
        {
            moveFlow = GetComponent<BattleUnitMoveFlow>();
        }

        if (moveFlow != null)
        {
            moveFlow.MoveCompleted -= HandleMoveCompleted;
            moveFlow.MoveCompleted += HandleMoveCompleted;
        }
    }

    private void OnDisable()
    {
        if (moveFlow != null)
        {
            moveFlow.MoveCompleted -= HandleMoveCompleted;
        }
    }

    private void HandleMoveCompleted(MapInfo arrivedTile)
    {
        if (arrivedTile == null)
        {
            Debug.LogWarning("이동은 끝났지만 도착 타일 정보가 없어 후처리를 건너뜁니다.", this);
            return;
        }

        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.Reveal(arrivedTile.transform.position);
        }
        else
        {
            Debug.LogWarning("이동 완료 후 Fog를 갱신할 FogOfWarManager가 없습니다.", this);
        }

        BattleGameManager manager = BattleGameManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("이동 완료 후 상점과 상자를 확인할 BattleGameManager가 없습니다.", this);
            return;
        }

        manager.ChestRewardSystem?.TryOpen(arrivedTile);
        manager.CardShopSystem?.TryEnter(arrivedTile);
    }
}
