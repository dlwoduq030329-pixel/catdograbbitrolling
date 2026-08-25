using UnityEngine;

/// <summary>
/// Battle 카드 실행 결과에 애니메이션과 레거시 VFX 연출만 연결한다.
/// 실제 피해와 상태 변화는 BattleCardEffectPipeline이 담당한다.
/// 카드 효과 파이프라인이 전달한 BattleCardData.presentation을 직접 읽으며 카드 번호 배열을 별도로 관리하지 않는다.
/// Registry 조회는 직접 VFX Prefab이 비어 있는 이전 데이터만 지원하는 임시 폴백이다.
/// </summary>
public static class BattleLegacyCardPresentationBridge
{
    /// <summary>
    /// 카드 효과가 모두 적용된 뒤 카드 데이터에 직접 연결된 Player 애니메이션과 VFX만 재생한다.
    /// PlayerSkills는 이전 Animator 재생 호환에 사용하고, 직접 VFX가 없을 때만 이전 Prefab과 Registry를 확인한다.
    /// </summary>
    public static void Play(
        GameObject player,
        GameObject selectedTarget,
        MapInfo selectedTile,
        BattleCardData cardData)
    {
        if (player == null || cardData == null || cardData.presentation == null)
            return;

        BattleCardPresentationData presentationData = cardData.presentation;
        string animationStateName = presentationData.animationStateName;

        PlayerSkills legacy = player.GetComponentInChildren<PlayerSkills>(true);
        if (legacy != null && !string.IsNullOrWhiteSpace(animationStateName))
        {
            legacy.PlayPresentationOnly(animationStateName);
        }
        else if (!string.IsNullOrWhiteSpace(animationStateName) &&
                 !BattleCharacterAnimationBridge.PlayState(player, animationStateName) &&
                 cardData.category == BattleCardCategory.Attack)
        {
            BattleCharacterAnimationBridge.PlayAttack(player);
        }

        // 카드 데이터의 직접 Prefab을 최우선으로 사용한다.
        GameObject visualPrefab = presentationData.vfxPrefab;

        // 아직 직접 데이터가 비어 있는 이전 카드만 PlayerSkills가 보관한 Prefab을 임시 사용한다.
        if (visualPrefab == null && legacy != null && !string.IsNullOrWhiteSpace(animationStateName))
            visualPrefab = legacy.GetPresentationPrefab(animationStateName);

        // 모든 카드 데이터 마이그레이션과 Unity QA가 끝날 때까지만 Registry를 마지막 폴백으로 유지한다.
        if (visualPrefab == null)
        {
            BattleCardVfxRegistry registry = BattleCardVfxRegistry.Load();
            visualPrefab = registry != null ? registry.Find(animationStateName) : null;
        }

        // 지속 영역 VFX는 BattleHealingArea가 영역의 실제 수명과 함께 관리하므로 여기서 중복 생성하지 않는다.
        if (!HasPersistentAreaEffect(cardData))
        {
            SpawnVisualOnly(
                visualPrefab,
                ResolvePosition(player, selectedTarget, selectedTile, presentationData.vfxSpawnPosition),
                presentationData);
        }
    }

    /// <summary>카드 데이터에 저장된 위치 규칙에 따라 VFX 생성 월드 좌표를 반환한다.</summary>
    private static Vector3 ResolvePosition(
        GameObject player,
        GameObject selectedTarget,
        MapInfo selectedTile,
        BattleCardVfxSpawnPosition spawnPosition)
    {
        switch (spawnPosition)
        {
            case BattleCardVfxSpawnPosition.Player:
                return player.transform.position;
            case BattleCardVfxSpawnPosition.SelectedTile:
                return selectedTile != null
                    ? selectedTile.transform.position
                    : selectedTarget != null
                        ? selectedTarget.transform.position
                        : player.transform.position;
            default:
                return selectedTarget != null
                    ? selectedTarget.transform.position
                    : selectedTile != null
                        ? selectedTile.transform.position
                        : player.transform.position;
        }
    }

    /// <summary>
    /// Legacy Prefab을 생성하되 내부 MonoBehaviour를 모두 꺼서 과거 피해·상태 로직이 중복 실행되지 않게 한다.
    /// ParticleSystem만 재생하고 2.5초 뒤 제거한다.
    /// </summary>
    private static void SpawnVisualOnly(
        GameObject prefab,
        Vector3 position,
        BattleCardPresentationData presentationData)
    {
        if (prefab == null) return;

        GameObject instance = Object.Instantiate(prefab, position, prefab.transform.rotation);
        if (presentationData.activateAllVfxChildren)
        {
            // 일부 이전 VFX는 시각 파티클 자식이 기본 비활성 상태라 데이터 설정에 따라 모두 활성화한다.
            foreach (Transform child in instance.transform)
                child.gameObject.SetActive(true);
        }
        if (presentationData.disableRuntimeBehaviours)
        {
            // 이전 Prefab 내부의 피해·상태 스크립트를 꺼서 EffectPipeline과 효과가 중복되지 않게 한다.
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
        }
        foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            particles.Play(true);
        Object.Destroy(instance, Mathf.Max(0f, presentationData.vfxLifetimeSeconds));
    }

    /// <summary>CreateArea 효과가 있으면 VFX 수명을 지속 영역 컴포넌트가 관리해야 하므로 true를 반환한다.</summary>
    private static bool HasPersistentAreaEffect(BattleCardData cardData)
    {
        return cardData.effects != null &&
               cardData.effects.Exists(effect =>
                   effect != null && effect.effectType == BattleCardEffectType.CreateArea);
    }
}
