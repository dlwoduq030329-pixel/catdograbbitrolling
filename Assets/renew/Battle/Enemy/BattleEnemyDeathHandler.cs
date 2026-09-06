using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy 체력이 0이 되면 입력과 점유 판정에서 즉시 제외하고 가라앉는 연출 후 오브젝트를 비활성화한다.
/// Scene을 다시 로드하지 않는 구조라 Destroy 대신 SetActive(false)로 남겨 다른 곳의 참조가
/// missing 참조가 되지 않게 한다(2026-08-21 변경). 사망 보상과 드롭 처리는 후속 시스템에서 이 책임을 확장한다.
/// 사망 VFX는 후속 작업에서 추가할 예정이므로 현재는 연출을 확장하지 않고 SetActive 전환만 수행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyDeathHandler : MonoBehaviour
{
    [Header("사망 연출")]
    [SerializeField, Min(0.1f)] private float disappearDuration = 0.75f;
    [SerializeField, Min(0f)] private float sinkDistance = 0.35f;

    private BattleHealth health;
    private bool isDying;

    /// <summary>
    /// Enemy의 체력 이벤트를 구독한다. EnemySpawner가 Instantiate 직후 딱 한 번만 호출하는 초기화 API다
    /// (재사용/오브젝트 풀링 없음, 2026-08-21 확인). 이후 이 오브젝트를 풀링해 재사용하는 구조가 생기면
    /// Configure가 같은 인스턴스에 두 번 불릴 수 있으므로 그때는 재구독 방지 로직을 다시 넣어야 한다.
    /// </summary>
    public void Configure(BattleHealth targetHealth)
    {
        health = targetHealth;
        if (health != null)
        {
            health.Died += HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
        }
    }

    private void HandleDied(BattleHealth deadHealth)
    {
        if (isDying) return;
        isDying = true;

        // 죽는 시점은 다른 Enemy의 턴 실행 도중일 수 있어(반격·범위 피해 등) 참가 목록을 여기서 바로
        // 변경하지 않는다. BattleEnemyTurnRunner.RunAll이 다음 적 턴 시작 직전 안전한 시점에
        // DrainPendingUnregisters로 실제 제거한다. 그 전까지는 Registry.Enemies에 남아있을 수 있다.
        BattleUnitRegistry registry = FindFirstObjectByType<BattleUnitRegistry>(FindObjectsInactive.Include);
        registry?.QueueUnregisterEnemy(gameObject);

        foreach (Collider targetCollider in GetComponentsInChildren<Collider>(true))
            targetCollider.enabled = false;
        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != this && !(behaviour is BattleHealthBarView))
                behaviour.enabled = false;
        }

        StartCoroutine(DisappearRoutine());
    }

    /// <summary>
    /// 사망 연출로 두 가지를 동시에 진행한다: (1) transform을 sinkDistance만큼 아래로 가라앉히고,
    /// (2) 이 Enemy의 모든 Renderer가 가진 모든 Material 인스턴스 알파값을 0까지 낮춰 투명해지게 한다.
    /// Material을 이렇게 직접 순회하며 건드리는 이유는, 프리팹마다 셰이더가 달라 공용 Fade/Transparent
    /// 모드를 보장할 수 없기 때문이다 — 그래서 각 Material이 "_Color" 프로퍼티를 갖고 있는지
    /// HasProperty로 먼저 확인한 뒤에만 알파를 깎는, 가장 범용적이지만 그만큼 장황한 방식을 썼다.
    /// originalColors가 Renderer[][] 형태인 이유는 Renderer마다 Material 개수가 다를 수 있어서이고,
    /// 매 프레임 다시 만들지 않도록 시작 시점 색상만 한 번 캐시해 둔 것이다.
    /// 클래스 상단 주석대로 사망 VFX는 나중에 별도로 확장될 예정이라, 지금 이 구현을 더 다듬기보다는
    /// 설명만 남겨 둔다. 나중에 리팩토링할 때는 Material.Lerp/DOTween류 셰이더 페이드나 Animator
    /// 트리거로 교체해서 이 이중 루프(Renderer x Material) 구조 자체를 단순화하는 걸 검토할 것.
    /// </summary>
    private IEnumerator DisappearRoutine()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Color[][] originalColors = new Color[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].materials;
            originalColors[i] = new Color[materials.Length];
            for (int j = 0; j < materials.Length; j++)
                originalColors[i][j] = materials[j].HasProperty("_Color")
                    ? materials[j].color
                    : Color.white;
        }

        Vector3 startPosition = transform.position;
        float elapsed = 0f;
        while (elapsed < disappearDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / disappearDuration);
            transform.position = startPosition + Vector3.down * (sinkDistance * progress);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].materials;
                for (int j = 0; j < materials.Length && j < originalColors[i].Length; j++)
                {
                    if (!materials[j].HasProperty("_Color")) continue;
                    Color color = originalColors[i][j];
                    color.a *= 1f - progress;
                    materials[j].color = color;
                }
            }
            yield return null;
        }

        // Scene을 다시 로드하는 구조가 아니라 이 Enemy 인스턴스를 Destroy하면 BattleUnitRegistry.Player,
        // BattleMapRegistry.occupiedTiles 등 다른 곳에 남아있는 참조가 missing 참조가 될 위험이 있다.
        // Registry 등록 해제는 위에서 대기열에 넣어뒀을 뿐 아직 실제로는 안 빠졌을 수 있지만,
        // 파괴 대신 비활성화만 하므로 그 참조 자체가 missing이 되지는 않는다.
        gameObject.SetActive(false);
    }
}
