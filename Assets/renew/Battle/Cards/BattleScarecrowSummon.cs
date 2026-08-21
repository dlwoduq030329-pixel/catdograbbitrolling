using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 생성된 허수아비 한 개의 체력·점유 타일·사망 해제와 Enemy 도발 대상 조회를 제공한다.
/// 현재 자체 static 목록은 UnitRegistry와 중복되므로 소환물을 공식 Unit으로 등록한 뒤 제거한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleScarecrowSummon : MonoBehaviour
{
    private static readonly List<BattleScarecrowSummon> active = new List<BattleScarecrowSummon>();
    private MapInfo occupiedTile;
    public BattleHealth Health { get; private set; }
    public MapInfo OccupiedTile => occupiedTile;

    public static IReadOnlyList<BattleScarecrowSummon> Active => active;

    /// <summary>
    /// 레거시 행동을 끄고 BattleHealth와 HP Bar를 연결한 뒤 활성 허수아비 목록에 등록한다.
    /// maxHealth는 현재 Bridge의 고정값이지만 최종 규칙은 소환 시점 Player 최대 HP의 1/3이다.
    /// </summary>
    public void Initialize(MapInfo tile, float maxHealth)
    {
        occupiedTile = tile;
        name = "BattleScarecrow";

        // 레거시 행동과 실시간 수명은 끄고 외형/Collider만 재사용한다.
        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour != this &&
                !(behaviour is BattleHealth) && !(behaviour is BattleHealthBarView))
                behaviour.enabled = false;
        }
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        Health = BattleComponentResolver.GetOrAdd(gameObject, Health);
        Health.Initialize(Mathf.Max(1f, maxHealth));
        BattleHealthBarFactory.AttachPlayerBar(gameObject, Health);
        Health.Died -= HandleDied;
        Health.Died += HandleDied;
        if (!active.Contains(this)) active.Add(this);
    }

    /// <summary>HP가 0이 되면 활성 목록에서 먼저 해제한 뒤 허수아비 GameObject를 제거한다.</summary>
    private void HandleDied(BattleHealth health)
    {
        active.Remove(this);
        Destroy(gameObject);
    }

    /// <summary>외부 Destroy에도 static 목록과 Health 사망 이벤트 구독이 남지 않도록 정리한다.</summary>
    private void OnDestroy()
    {
        active.Remove(this);
        if (Health != null) Health.Died -= HandleDied;
    }

    /// <summary>살아 있는 허수아비 중 요청 위치와 가장 가까운 대상을 Enemy 도발 Target으로 반환한다.</summary>
    public static Transform FindNearest(Vector3 position)
    {
        BattleScarecrowSummon best = null;
        float bestDistance = float.MaxValue;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            BattleScarecrowSummon summon = active[i];
            if (summon == null || summon.Health == null || summon.Health.IsDead)
            {
                active.RemoveAt(i);
                continue;
            }
            float distance = (summon.transform.position - position).sqrMagnitude;
            if (distance < bestDistance) { best = summon; bestDistance = distance; }
        }
        return best != null ? best.transform : null;
    }

    /// <summary>Target Transform에 BattleScarecrowSummon이 붙어 있는지 검사한다.</summary>
    public static bool IsScarecrow(Transform target)
    {
        return target != null && target.GetComponent<BattleScarecrowSummon>() != null;
    }
}
