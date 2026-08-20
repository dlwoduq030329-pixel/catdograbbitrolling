using UnityEngine;

/// <summary>
/// BattleDamageService에서 확정된 피해만 받아 피격 위치에 레거시 VFX를 생성한다.
/// 프리팹·위치·크기·수명을 Inspector에서 교체할 수 있어 아트 미세 조정이 코드와 분리된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDamageVfxPresenter : MonoBehaviour
{
    [Header("피격 VFX 프리팹")]
    [InspectorName("플레이어 피격 VFX")]
    [Tooltip("플레이어가 피해를 받을 때 생성할 프리팹입니다.")]
    [SerializeField] private GameObject playerHitPrefab;
    [InspectorName("적 피격 VFX")]
    [Tooltip("적이 피해를 받을 때 생성할 프리팹입니다.")]
    [SerializeField] private GameObject enemyHitPrefab;

    [Header("피격 VFX 배치")]
    [InspectorName("플레이어 위치 보정")]
    [SerializeField] private Vector3 playerPositionOffset = new Vector3(0f, 0.8f, 0f);
    [InspectorName("적 위치 보정")]
    [SerializeField] private Vector3 enemyPositionOffset = new Vector3(0f, 0.8f, 0f);
    [InspectorName("플레이어 VFX 크기")]
    [SerializeField, Min(0.01f)] private float playerScale = 1f;
    [InspectorName("적 VFX 크기")]
    [SerializeField, Min(0.01f)] private float enemyScale = 1f;
    [InspectorName("자동 제거 시간 (초)")]
    [Tooltip("레거시 프리팹이 스스로 제거되지 않아도 남지 않도록 정리하는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float lifetimeSeconds = 2f;

    private void OnEnable()
    {
        BattleDamageService.DamageApplied -= HandleDamageApplied;
        BattleDamageService.DamageApplied += HandleDamageApplied;
    }

    private void OnDisable()
    {
        BattleDamageService.DamageApplied -= HandleDamageApplied;
    }

    /// <summary>피해 대상이 현재 플레이어인지 판별하고 대응하는 프리팹과 조정값으로 VFX를 생성한다.</summary>
    private void HandleDamageApplied(BattleDamageResult result)
    {
        GameObject target = result.Target;
        if (target == null || result.AppliedDamage <= 0f)
        {
            return;
        }

        bool isPlayer = BattleGameManager.Instance != null &&
                        BattleGameManager.Instance.CurrentPlayer == target;
        GameObject prefab = isPlayer ? playerHitPrefab : enemyHitPrefab;
        if (prefab == null)
        {
            return;
        }

        Vector3 offset = isPlayer ? playerPositionOffset : enemyPositionOffset;
        float scale = isPlayer ? playerScale : enemyScale;
        Vector3 position = ResolveTargetCenter(target) + offset;
        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        instance.transform.localScale = prefab.transform.localScale * Mathf.Max(0.01f, scale);
        Destroy(instance, Mathf.Max(0.1f, lifetimeSeconds));
    }

    /// <summary>Collider가 있으면 시각 중심을, 없으면 Transform 위치를 피격 기준점으로 사용한다.</summary>
    private static Vector3 ResolveTargetCenter(GameObject target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        return targetCollider != null ? targetCollider.bounds.center : target.transform.position;
    }
}
