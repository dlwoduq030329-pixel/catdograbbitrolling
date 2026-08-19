using UnityEngine;

/// <summary>
/// 기존 Player·Enemy HP Prefab을 재사용하여 BattleHealthBarView와 연결한다.
/// 기존 캐릭터 Prefab에 같은 이름의 HP 바가 있으면 중복 생성하지 않는다.
/// </summary>
public static class BattleHealthBarFactory
{
    private const string EnemyBarName = "EnemyHPBar";
    private const string PlayerBarName = "PlayerHPBar";
    private const string EnemyResourcePath = "UI/HealthBars/EnemyHPBar";
    private const string PlayerResourcePath = "UI/HealthBars/PlayerHPBar";

    /// <summary>Enemy 내부의 기존 HP 바를 우선 연결하고 없을 때만 Resources Prefab을 생성한다.</summary>
    public static BattleHealthBarView AttachEnemyBar(GameObject enemy, BattleHealth health)
    {
        return AttachBar(enemy, health, EnemyBarName, EnemyResourcePath);
    }

    /// <summary>Player 내부의 기존 HP 바를 우선 연결하고 없을 때만 Resources Prefab을 생성한다.</summary>
    public static BattleHealthBarView AttachPlayerBar(GameObject player, BattleHealth health)
    {
        return AttachBar(player, health, PlayerBarName, PlayerResourcePath);
    }

    private static BattleHealthBarView AttachBar(
        GameObject owner,
        BattleHealth health,
        string barName,
        string resourcePath)
    {
        if (owner == null || health == null)
        {
            return null;
        }

        Transform barTransform = FindChildByName(owner.transform, barName);
        if (barTransform == null)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"체력 바 Resources를 찾지 못했습니다: {resourcePath}", owner);
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, owner.transform, false);
            instance.name = barName;
            barTransform = instance.transform;
        }

        // HP 바는 월드 타일과 같은 평면을 바라보도록 항상 X축 90도로 고정한다.
        barTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        BattleHealthBarView view = barTransform.GetComponent<BattleHealthBarView>();
        if (view == null)
        {
            view = barTransform.gameObject.AddComponent<BattleHealthBarView>();
        }

        barTransform.gameObject.SetActive(true);
        view.ConfigureWorldRotationLock(true, new Vector3(90f, 0f, 0f));
        view.AlignToBoxCollider(owner);
        view.Bind(health);
        return view;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
