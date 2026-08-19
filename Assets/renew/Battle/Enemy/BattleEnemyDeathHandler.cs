using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy 체력이 0이 되면 입력과 점유 판정에서 즉시 제외하고 오브젝트를 제거한다.
/// 사망 보상, 애니메이션과 드롭 처리는 후속 시스템에서 이 책임을 확장한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyDeathHandler : MonoBehaviour
{
    [Header("사망 연출")]
    [SerializeField, Min(0.1f)] private float disappearDuration = 0.75f;
    [SerializeField, Min(0f)] private float sinkDistance = 0.35f;

    private BattleHealth health;
    private bool isDying;

    /// <summary>Enemy의 체력 이벤트를 구독한다. 같은 체력을 재연결해도 구독이 중복되지 않는다.</summary>
    public void Configure(BattleHealth targetHealth)
    {
        if (health != null)
        {
            health.Died -= HandleDied;
        }

        health = targetHealth;
        if (health != null)
        {
            health.Died -= HandleDied;
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

        // Registry에 남아있으면 죽은 Enemy가 턴 진행·타겟팅 판단에 계속 잡힐 수 있어 먼저 해제한다.
        BattleUnitRegistry registry = FindFirstObjectByType<BattleUnitRegistry>(FindObjectsInactive.Include);
        registry?.UnregisterEnemy(gameObject);

        foreach (Collider targetCollider in GetComponentsInChildren<Collider>(true))
            targetCollider.enabled = false;
        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != this && !(behaviour is BattleHealthBarView))
                behaviour.enabled = false;
        }

        StartCoroutine(DisappearRoutine());
    }

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

        Destroy(gameObject);
    }
}
