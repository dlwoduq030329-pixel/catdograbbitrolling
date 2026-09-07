using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>사망 시 참가 해제를 예약하고 연출 후 풀에 반환한다. 재사용 전에 연출 상태를 복구한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyDeathHandler : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float disappearDuration = 0.75f;
    [SerializeField, Min(0f)] private float sinkDistance = 0.35f;
    private BattleHealth health;
    private BattleUnitRegistry registry;
    private Action release;
    private bool isDying;
    private readonly List<Collider> disabledColliders = new List<Collider>();
    private readonly List<MonoBehaviour> disabledBehaviours = new List<MonoBehaviour>();
    private readonly Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
    public Vector3 SpawnOffset { get; set; }
    public Vector3 SpawnScale { get; set; } = Vector3.one;

    public void Configure(BattleHealth targetHealth, BattleUnitRegistry units = null, Action onRelease = null)
    {
        if (health != null) health.Died -= HandleDied;
        health = targetHealth;
        registry = units;
        release = onRelease;
        if (health != null) health.Died += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= HandleDied;
    }

    public void ResetForReuse()
    {
        StopAllCoroutines();
        foreach (var pair in originalColors)
            if (pair.Key != null) pair.Key.color = pair.Value;
        originalColors.Clear();
        foreach (Collider collider in disabledColliders)
            if (collider != null) collider.enabled = true;
        foreach (MonoBehaviour behaviour in disabledBehaviours)
            if (behaviour != null) behaviour.enabled = true;
        disabledColliders.Clear();
        disabledBehaviours.Clear();
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            if (animator.runtimeAnimatorController != null) animator.Rebind();
        transform.localScale = SpawnScale;
        isDying = false;
    }

    private void HandleDied(BattleHealth deadHealth)
    {
        if (isDying) return;
        isDying = true;
        registry?.QueueUnregisterEnemy(gameObject);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            if (collider.enabled) { disabledColliders.Add(collider); collider.enabled = false; }
        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != this && !(behaviour is BattleHealthBarView) && behaviour.enabled)
            { disabledBehaviours.Add(behaviour); behaviour.enabled = false; }
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.materials)
                if (material.HasProperty("_Color") && !originalColors.ContainsKey(material))
                    originalColors.Add(material, material.color);
        StartCoroutine(Disappear());
    }

    private IEnumerator Disappear()
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < disappearDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / disappearDuration);
            transform.position = start + Vector3.down * sinkDistance * progress;
            foreach (var pair in originalColors)
            {
                if (pair.Key == null) continue;
                Color color = pair.Value;
                color.a *= 1f - progress;
                pair.Key.color = color;
            }
            yield return null;
        }
        if (release != null) release();
        else gameObject.SetActive(false);
    }
}
