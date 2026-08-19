using System.Collections.Generic;
using UnityEngine;

/// <summary>레거시 허수아비 프리팹 외형을 새 Battle 체력/타일/도발 규칙에 연결한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleScarecrowSummon : MonoBehaviour
{
    private static readonly List<BattleScarecrowSummon> active = new List<BattleScarecrowSummon>();
    private MapInfo occupiedTile;
    public BattleHealth Health { get; private set; }
    public MapInfo OccupiedTile => occupiedTile;

    public static IReadOnlyList<BattleScarecrowSummon> Active => active;

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

    private void HandleDied(BattleHealth health)
    {
        active.Remove(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        active.Remove(this);
        if (Health != null) Health.Died -= HandleDied;
    }

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

    public static bool IsScarecrow(Transform target)
    {
        return target != null && target.GetComponent<BattleScarecrowSummon>() != null;
    }
}
