using UnityEngine;

/// <summary>최초 풀 생성 시 모델 크기·발 높이·클릭 Collider를 맞춘다. 대여 때는 재계산하지 않는다.</summary>
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
        if (enemy == null || tile == null ||
            !TryGetVisualBounds(enemy, out Bounds enemyBounds) ||
            !TryGetTileBounds(tile, out Bounds tileBounds))
        {
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

    private static bool TryGetVisualBounds(GameObject owner, out Bounds bounds)
    {
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
