using UnityEngine;
using UnityEngine.Serialization;

/// <summary>피격 VFX가 어느 방향을 기준으로 회전할지 결정한다.</summary>
public enum BattleHitVfxRotationMode
{
    [InspectorName("월드 회전 고정")]
    WorldFixed,
    [InspectorName("피격 대상 회전 사용")]
    FollowDamagedUnit,
    [InspectorName("공격자 방향 보기")]
    FaceAttacker,
    [InspectorName("카메라 방향 보기")]
    FaceCamera
}

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
    [FormerlySerializedAs("playerHitPrefab")]
    [SerializeField] private GameObject playerHitVfxPrefab;
    [InspectorName("적 피격 VFX")]
    [Tooltip("적이 피해를 받을 때 생성할 프리팹입니다.")]
    [FormerlySerializedAs("enemyHitPrefab")]
    [SerializeField] private GameObject enemyHitVfxPrefab;

    [Header("피격 VFX 배치")]
    [InspectorName("플레이어 위치 보정")]
    [FormerlySerializedAs("playerPositionOffset")]
    [SerializeField] private Vector3 playerHitVfxPositionOffset = new Vector3(0f, 0.8f, 0f);
    [InspectorName("적 위치 보정")]
    [FormerlySerializedAs("enemyPositionOffset")]
    [SerializeField] private Vector3 enemyHitVfxPositionOffset = new Vector3(0f, 0.8f, 0f);
    [InspectorName("플레이어 VFX 크기")]
    [FormerlySerializedAs("playerScale")]
    [SerializeField, Min(0.01f)] private float playerHitVfxScaleMultiplier = 1f;
    [InspectorName("적 VFX 크기")]
    [FormerlySerializedAs("enemyScale")]
    [SerializeField, Min(0.01f)] private float enemyHitVfxScaleMultiplier = 1f;
    [InspectorName("플레이어 VFX 회전 기준")]
    [SerializeField] private BattleHitVfxRotationMode playerHitVfxRotationMode =
        BattleHitVfxRotationMode.WorldFixed;
    [InspectorName("적 VFX 회전 기준")]
    [SerializeField] private BattleHitVfxRotationMode enemyHitVfxRotationMode =
        BattleHitVfxRotationMode.WorldFixed;
    [InspectorName("VFX가 바라볼 카메라")]
    [Tooltip("회전 기준을 카메라 방향으로 선택할 때 사용할 카메라입니다.")]
    [SerializeField] private Camera hitVfxCamera;
    [InspectorName("자동 제거 시간 (초)")]
    [Tooltip("레거시 프리팹이 스스로 제거되지 않아도 남지 않도록 정리하는 시간입니다.")]
    [FormerlySerializedAs("lifetimeSeconds")]
    [SerializeField, Min(0.1f)] private float hitVfxLifetimeSeconds = 2f;

    private void OnEnable()
    {
        BattleDamageService.DamageApplied -= SpawnHitVfxAfterDamage;
        BattleDamageService.DamageApplied += SpawnHitVfxAfterDamage;
    }

    private void OnDisable()
    {
        BattleDamageService.DamageApplied -= SpawnHitVfxAfterDamage;
    }

    /// <summary>
    /// 보호막과 HP에 실제 피해가 적용된 뒤 호출된다.
    /// 피해 대상 종류에 맞는 Inspector 프리팹·위치·크기·회전 설정으로 피격 VFX를 생성하고,
    /// 레거시 VFX가 스스로 제거되지 않더라도 지정 시간이 지나면 생성 인스턴스를 정리한다.
    /// </summary>
    private void SpawnHitVfxAfterDamage(BattleDamageResult damageResult)
    {
        GameObject damagedUnit = damageResult.Target;
        if (damagedUnit == null || damageResult.AppliedDamage <= 0f)
        {
            return;
        }

        bool isPlayer = BattleGameManager.Instance != null &&
                        BattleGameManager.Instance.CurrentPlayer == damagedUnit;
        GameObject selectedHitVfxPrefab = isPlayer ? playerHitVfxPrefab : enemyHitVfxPrefab;
        if (selectedHitVfxPrefab == null)
        {
            return;
        }

        Vector3 positionOffset = isPlayer
            ? playerHitVfxPositionOffset
            : enemyHitVfxPositionOffset;
        float scaleMultiplier = isPlayer
            ? playerHitVfxScaleMultiplier
            : enemyHitVfxScaleMultiplier;
        BattleHitVfxRotationMode rotationMode = isPlayer
            ? playerHitVfxRotationMode
            : enemyHitVfxRotationMode;

        Vector3 spawnPosition = GetDamagedUnitVisualCenter(damagedUnit) + positionOffset;
        Quaternion spawnRotation = GetHitVfxRotation(
            rotationMode,
            damagedUnit,
            damageResult.Attacker,
            spawnPosition);
        GameObject spawnedHitVfx = Instantiate(
            selectedHitVfxPrefab,
            spawnPosition,
            spawnRotation);
        spawnedHitVfx.transform.localScale =
            selectedHitVfxPrefab.transform.localScale * Mathf.Max(0.01f, scaleMultiplier);
        Destroy(spawnedHitVfx, Mathf.Max(0.1f, hitVfxLifetimeSeconds));
    }

    /// <summary>Collider가 있으면 시각 중심을, 없으면 Transform 위치를 피격 기준점으로 사용한다.</summary>
    private static Vector3 GetDamagedUnitVisualCenter(GameObject damagedUnit)
    {
        Collider damagedUnitCollider = damagedUnit.GetComponentInChildren<Collider>();
        return damagedUnitCollider != null
            ? damagedUnitCollider.bounds.center
            : damagedUnit.transform.position;
    }

    /// <summary>
    /// 월드 고정은 기존 연출처럼 회전하지 않으며, 대상 회전은 캐릭터 몸 방향을 따른다.
    /// 공격자 방향은 피격 위치가 공격자를 바라보게 하고, 카메라 방향은 2D VFX가 화면을 향하게 한다.
    /// 필요한 참조가 없으면 연출 누락 대신 안전하게 월드 고정 회전을 사용한다.
    /// </summary>
    private Quaternion GetHitVfxRotation(
        BattleHitVfxRotationMode rotationMode,
        GameObject damagedUnit,
        GameObject attacker,
        Vector3 spawnPosition)
    {
        switch (rotationMode)
        {
            case BattleHitVfxRotationMode.FollowDamagedUnit:
                return damagedUnit.transform.rotation;

            case BattleHitVfxRotationMode.FaceAttacker:
                if (attacker != null)
                {
                    Vector3 directionToAttacker = attacker.transform.position - spawnPosition;
                    if (directionToAttacker.sqrMagnitude > Mathf.Epsilon)
                    {
                        return Quaternion.LookRotation(directionToAttacker.normalized, Vector3.up);
                    }
                }
                return Quaternion.identity;

            case BattleHitVfxRotationMode.FaceCamera:
                if (hitVfxCamera != null)
                {
                    Vector3 directionToCamera = hitVfxCamera.transform.position - spawnPosition;
                    if (directionToCamera.sqrMagnitude > Mathf.Epsilon)
                    {
                        return Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
                    }
                }
                return Quaternion.identity;

            default:
                // 방향이 없는 폭발·원형 VFX는 대상과 Camera 회전에 영향받지 않도록 월드 회전을 고정한다.
                return Quaternion.identity;
        }
    }
}
