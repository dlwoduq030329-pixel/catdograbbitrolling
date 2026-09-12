using UnityEngine;

/// <summary>
/// 타일 위에 올라가는 오브젝트의 모델 크기·발 높이·클릭 Collider를 맞춘다.
/// Enemy는 최초 풀 생성 시에만 쓰고(대여 때는 재계산하지 않음), NPC 같은 임시 오브젝트는
/// AlignFeetToGround만 매번 새로 호출한다.
/// </summary>
public static class EnemySpawnGeometry
{
    public static void EnsureCollider(GameObject enemy)
    {
        if (enemy == null || enemy.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        if (!TryGetVisualBounds(enemy, out Bounds worldBounds))
        {
            Debug.LogWarning($"적 생성: Collider·Renderer가 모두 없어 클릭 판정용 Collider를 만들지 못했습니다: {enemy.name}", enemy);
            return;
        }
        Vector3 lossyScale = enemy.transform.lossyScale;
        BoxCollider addedCollider = enemy.AddComponent<BoxCollider>();
        addedCollider.center = enemy.transform.InverseTransformPoint(worldBounds.center);
        addedCollider.size = new Vector3(
            SafeDivide(worldBounds.size.x, lossyScale.x),
            SafeDivide(worldBounds.size.y, lossyScale.y),
            SafeDivide(worldBounds.size.z, lossyScale.z));
    }

    private static float SafeDivide(float worldSize, float scaleComponent)
    {
        return Mathf.Abs(scaleComponent) > 0.0001f ? worldSize / Mathf.Abs(scaleComponent) : worldSize;
    }

    public static void Fit(GameObject enemy, Transform tile, float enemyTileFillRatio, bool allowEnemyUpscaling, float minimumScaleMultiplier)
    {
        if (enemy == null || tile == null)
        {
            return;
        }
        if (!TryGetVisualBounds(enemy, out Bounds enemyBounds))
        {
            Debug.LogWarning($"적 크기 맞추기 실패: Renderer가 없어 {enemy.name}의 크기를 구하지 못했습니다.", enemy);
            return;
        }
        if (!TryGetTileBounds(tile, out Bounds tileBounds))
        {
            Debug.LogWarning($"적 크기 맞추기 실패: 타일 {tile.name}의 Collider·Renderer를 찾지 못했습니다.", tile);
            return;
        }
        float enemyFootprint = Mathf.Max(enemyBounds.size.x, enemyBounds.size.z);
        float tileFootprint = Mathf.Min(tileBounds.size.x, tileBounds.size.z) * enemyTileFillRatio;
        if (enemyFootprint <= 0.001f || tileFootprint <= 0.001f)
        {
            return;
        }
        float multiplier = tileFootprint / enemyFootprint;
        if (!allowEnemyUpscaling) 
        {
            multiplier = Mathf.Min(1f, multiplier);
        }
        multiplier = Mathf.Max(minimumScaleMultiplier, multiplier);
        float groundY = enemy.transform.position.y;
        float footOffsetAtUnitScale = enemyBounds.min.y - enemy.transform.position.y;
        enemy.transform.localScale *= multiplier;
        float scaledFootOffset = footOffsetAtUnitScale * multiplier;
        float correctedY = groundY - scaledFootOffset;
        enemy.transform.position = new Vector3(
            enemy.transform.position.x, correctedY, enemy.transform.position.z);

    }

    /// <summary>
    /// 크기는 그대로 두고 Y 위치만 보정한다. Fit()과 똑같이 "타일 pivot 위치 + 여유값"을 바닥으로
    /// 보고(타일의 Collider/Renderer 바운드 최상단은 쓰지 않는다 — 클릭 판정용 Collider가 시각적
    /// 바닥보다 훨씬 높이 솟아있을 수 있어 그 값을 기준으로 삼으면 오히려 하늘로 튕겨 올라간다),
    /// 렌더러 바운드의 최소 Y(실제 모델 발 위치)가 그 자리에 오도록 밀어 올린다. 스케일 조정 없이
    /// Y만 필요한 NPC 같은 임시 오브젝트에 쓴다. extraLift는 표면 위로 살짝 더 띄우고 싶을 때 쓰는
    /// 추가 여유값이다(Enemy의 defaultSpawnHeight와 같은 역할).
    /// 스케일까지 같이 바꿔야 하면 이 메서드 대신 FitScaleAndAlign을 써야 한다 — 스케일을 먼저
    /// 바꾸고 이 메서드를 나중에 부르면 TryGetVisualBounds가 다시 Animator를 건드리면서
    /// 방금 바꾼 스케일이 흐트러질 수 있다.
    /// </summary>
    public static void AlignFeetToGround(GameObject target, Transform tile, float extraLift = 0f)
    {
        if (target == null || tile == null)
        {
            return;
        }
        if (!TryGetVisualBounds(target, out Bounds bounds))
        {
            Debug.LogWarning($"발 위치 보정 실패: Renderer가 없어 {target.name}의 크기를 구하지 못했습니다.", target);
            return;
        }

        float groundY = tile.position.y + extraLift;
        float footOffset = bounds.min.y - target.transform.position.y;
        float correctedY = groundY - footOffset;
        target.transform.position = new Vector3(
            target.transform.position.x, correctedY, target.transform.position.z);
    }

    /// <summary>NPC 크기를 적용한 뒤 Collider 바닥을 타일 높이에 맞춘다. Collider가 없으면 Renderer를 사용한다.</summary>
    public static void FitScaleAndAlign(GameObject target, Transform tile, float scaleMultiplier, float extraLift = 0f)
    {
        if (target == null || tile == null)
        {
            return;
        }

        target.transform.localScale *= Mathf.Max(0.01f, scaleMultiplier);
        Physics.SyncTransforms();

        float bottomY;
        Collider rootCollider = target.GetComponent<Collider>();
        if (rootCollider != null && rootCollider.enabled)
        {
            bottomY = rootCollider.bounds.min.y;
        }
        else if (TryGetVisualBounds(target, out Bounds bounds))
        {
            bottomY = bounds.min.y;
        }
        else
        {
            Debug.LogWarning($"NPC 발 위치 보정 실패: Collider와 Renderer가 없습니다: {target.name}", target);
            return;
        }

        float groundY = tile.position.y + extraLift;
        target.transform.position += Vector3.up * (groundY - bottomY);
    }

    /// <summary>
    /// Animator가 있으면 Instantiate 직후 아직 첫 포즈를 평가하지 않은 상태일 수 있다(바인드 포즈로
    /// 남아 있어 SkinnedMeshRenderer의 bounds가 실제 모습과 다르게 계산된다). 강제로 0초만큼 갱신해서
    /// 실제 애니메이션 포즈를 즉시 반영시킨 뒤에 bounds를 읽는다. Enemy(Fit)·NPC(AlignFeetToGround)
    /// 둘 다 아래 TryGetVisualBounds를 거치므로 여기 한 곳만 고치면 둘 다 적용된다.
    /// </summary>
    private static void SettleAnimatorPose(GameObject owner)
    {
        Animator[] animators = owner.GetComponentsInChildren<Animator>(true);
        foreach (Animator animator in animators)
        {
            // 꺼둔 Animator(예: NPC placeholder)는 갱신할 필요가 없다. 정지 상태 그대로의
            // bounds를 쓰면 되고, 굳이 Update를 부를 이유도 없다.
            if (!animator.enabled)
            {
                continue;
            }

            animator.Update(0f);
        }
    }

    private static bool TryGetVisualBounds(GameObject owner, out Bounds bounds)
    {
        SettleAnimatorPose(owner);
        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return found;
    }

    private static bool TryGetTileBounds(Transform tile, out Bounds bounds)
    {
        Collider tileCollider = tile.GetComponentInChildren<Collider>();
        if (tileCollider != null)
        {
            bounds = tileCollider.bounds;
            return true;
        }

        Renderer tileRenderer = tile.GetComponentInChildren<Renderer>();
        if (tileRenderer != null)
        {
            bounds = tileRenderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }
}
