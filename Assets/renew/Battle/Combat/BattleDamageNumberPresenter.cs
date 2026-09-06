using UnityEngine;
using TMPro;

/// <summary>
/// BattleDamageService에서 확정된 피해만 받아 피격 위치에 떠오르는 데미지 숫자를 생성한다.
/// BattleDamageVfxPresenter(파티클 VFX)와 같은 이벤트·같은 위치 계산 방식을 쓰지만, 이 컴포넌트는
/// 프리팹을 쓰지 않고 TextMeshPro(3D 월드 텍스트)를 코드에서 직접 만들어 붙인다 — 아직 전용 프리팹이
/// 없어서 우선 빠르게 동작을 확인하기 위한 버전이다. 나중에 폰트·외곽선 등을 더 자유롭게 꾸미고
/// 싶어지면 프리팹 기반으로 바꿔도 이 컴포넌트의 나머지 구조(이벤트 구독, 위치 계산, 애니메이션 위임)는
/// 거의 그대로 재사용할 수 있다. 실제 상승·페이드 연출은 생성 직후 붙이는 BattleFloatingDamageNumber가
/// 전담한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDamageNumberPresenter : MonoBehaviour
{
    [Header("데미지 숫자 배치")]
    [InspectorName("플레이어 피격 위치 보정")]
    // 좌우(X)를 0으로 두면 숫자가 유닛 정중앙 위에 뜨는데, 그 자리는 HP바가 차지하고 있어서 숫자가
    // HP바 뒤에 가려지거나 겹쳐 보인다. X에 살짝 값을 줘서 HP바를 피해 옆으로 비켜서 뜨게 한다.
    [Tooltip("플레이어가 피해를 받았을 때 숫자를 띄울 기준점(피격 대상 Collider 중심)에서 더할 오프셋입니다. X를 살짝 틀어 HP바와 겹치지 않게 합니다.")]
    [SerializeField] private Vector3 playerDamageNumberOffset = new Vector3(-0.8f, 1.2f, 0f);
    [InspectorName("적 피격 위치 보정")]
    [Tooltip("적이 피해를 받았을 때 숫자를 띄울 기준점에서 더할 오프셋입니다. X를 살짝 틀어 HP바와 겹치지 않게 합니다.")]
    [SerializeField] private Vector3 enemyDamageNumberOffset = new Vector3(0.8f, 1.2f, 0f);
    [InspectorName("좌우 무작위 흩뿌림 범위")]
    [Tooltip("같은 대상이 짧은 시간 안에 여러 번 맞아 숫자가 완전히 겹쳐 보이지 않도록 스폰 위치에 좌우로 " +
             "더할 무작위 오프셋의 최대 폭입니다. 0이면 흩뿌리지 않습니다.")]
    [SerializeField, Min(0f)] private float randomHorizontalJitter = 0.25f;

    [Header("데미지 숫자 모양")]
    [InspectorName("플레이어 피격 숫자 색상")]
    [Tooltip("플레이어가 맞았을 때(위험 신호) 표시할 숫자 색상입니다.")]
    [SerializeField] private Color playerDamageNumberColor = new Color(1f, 0.28f, 0.24f);
    [InspectorName("적 피격 숫자 색상")]
    [Tooltip("적을 때렸을 때 표시할 숫자 색상입니다.")]
    [SerializeField] private Color enemyDamageNumberColor = Color.white;
    [InspectorName("글자 크기")]
    [SerializeField, Min(0.1f)] private float fontSize = 4f;

    [Header("데미지 숫자 애니메이션")]
    [InspectorName("상승 거리")]
    [Tooltip("스폰 위치에서 위로 이동하는 총 거리입니다.")]
    [SerializeField, Min(0f)] private float riseDistance = 1.3f;
    [InspectorName("지속 시간 (초)")]
    [SerializeField, Min(0.1f)] private float lifetimeSeconds = 0.9f;
    [InspectorName("선명하게 유지되는 비율")]
    [Tooltip("전체 지속시간 중 페이드 없이 완전히 선명하게 유지할 비율입니다. 나머지 구간 동안 0으로 페이드됩니다.")]
    [SerializeField, Range(0f, 1f)] private float holdBeforeFadeRatio = 0.45f;
    [InspectorName("빌보드 기준 카메라 (선택 사항)")]
    [Tooltip("비워두면 Camera.main을 그때그때 찾아서 사용합니다.")]
    [SerializeField] private Camera billboardCamera;

    private void OnEnable()
    {
        BattleDamageService.DamageApplied -= SpawnDamageNumberAfterDamage;
        BattleDamageService.DamageApplied += SpawnDamageNumberAfterDamage;
    }

    private void OnDisable()
    {
        BattleDamageService.DamageApplied -= SpawnDamageNumberAfterDamage;
    }

    /// <summary>
    /// 보호막과 HP에 실제 피해가 적용된 뒤 호출된다. BattleDamageVfxPresenter.SpawnHitVfxAfterDamage와
    /// 같은 조건(대상 존재, 실제 피해 0보다 큼)에서만 숫자를 생성한다.
    /// </summary>
    private void SpawnDamageNumberAfterDamage(BattleDamageResult damageResult)
    {
        GameObject damagedUnit = damageResult.Target;
        if (damagedUnit == null || damageResult.AppliedDamage <= 0f)
        {
            return;
        }

        bool isPlayer = BattleGameManager.Instance != null &&
                        BattleGameManager.Instance.CurrentPlayer == damagedUnit;
        Vector3 positionOffset = isPlayer ? playerDamageNumberOffset : enemyDamageNumberOffset;
        Color numberColor = isPlayer ? playerDamageNumberColor : enemyDamageNumberColor;

        Vector3 spawnPosition = GetDamagedUnitVisualCenter(damagedUnit) + positionOffset;
        if (randomHorizontalJitter > 0f)
        {
            // 좌우(X, Z) 두 축에만 흩뿌린다. 위(Y)까지 흔들면 숫자가 화면에서 위아래로 들쭉날쭉해
            // 오히려 읽기 불편해진다.
            spawnPosition += new Vector3(
                Random.Range(-randomHorizontalJitter, randomHorizontalJitter),
                0f,
                Random.Range(-randomHorizontalJitter, randomHorizontalJitter));
        }

        GameObject damageNumberObject = new GameObject($"DamageNumber ({damagedUnit.name})");
        damageNumberObject.transform.position = spawnPosition;

        TextMeshPro textMesh = damageNumberObject.AddComponent<TextMeshPro>();
        textMesh.text = Mathf.RoundToInt(damageResult.AppliedDamage).ToString();
        textMesh.fontSize = fontSize;
        textMesh.color = numberColor;
        textMesh.alignment = TextAlignmentOptions.Center;

        Camera resolvedCamera = billboardCamera != null ? billboardCamera : Camera.main;
        BattleFloatingDamageNumber floatingNumber =
            damageNumberObject.AddComponent<BattleFloatingDamageNumber>();
        floatingNumber.Initialize(
            textMesh,
            resolvedCamera,
            riseDistance,
            lifetimeSeconds,
            holdBeforeFadeRatio);
    }

    /// <summary>Collider가 있으면 시각 중심을, 없으면 Transform 위치를 피격 기준점으로 사용한다.
    /// BattleDamageVfxPresenter의 같은 이름 함수와 동일한 계산이다(파티클과 숫자가 같은 기준점에서
    /// 각자의 오프셋만큼만 떨어지도록 의도적으로 로직을 맞췄다).</summary>
    private static Vector3 GetDamagedUnitVisualCenter(GameObject damagedUnit)
    {
        Collider damagedUnitCollider = damagedUnit.GetComponentInChildren<Collider>();
        return damagedUnitCollider != null
            ? damagedUnitCollider.bounds.center
            : damagedUnit.transform.position;
    }
}
