using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player·Enemy 체력 바를 조립하고 BattleHealthBarView(및 Enemy는 BattleManaBarView)와 연결한다.
/// 기존 캐릭터 Prefab에 같은 이름의 HP 바가 있으면 중복 생성하지 않는다.
/// </summary>
public static class BattleHealthBarFactory
{
    private const string EnemyBarName = "EnemyHPBar";
    private const string PlayerBarName = "PlayerHPBar";
    private const string EnemyResourcePath = "UI/HealthBars/EnemyHPBar";
    private const string PlayerResourcePath = "UI/HealthBars/PlayerHPBar";
    private const string HpFillChildName = "HP ";
    private const string MpFillChildName = "Mp";
    private const string TypeIconChildName = "종류";

    // NewHpPre 계열 아트 프리팹은 화면(Screen Space) UI 픽셀 크기(가로세로 수백~천 단위)로
    // 만들어져 있다. World Space Canvas에 그대로 두면 몇백 유닛짜리 거대한 판이 되어
    // 카메라가 그 안에 파묻히므로, World Space로 쓸 때는 이 배율로 축소한다.
    private const float EnemyBarWorldScale = 0.003f;

    /// <summary>Enemy 머리 위 HP/MP 게이지를 연결한다. 이미 있으면 재사용하고 없으면 새로 조립한다.</summary>
    public static BattleHealthBarView AttachEnemyBar(
        GameObject enemy,
        BattleHealth health,
        BattleUnitMP mp = null,
        Sprite typeIcon = null)
    {
        if (enemy == null || health == null)
        {
            return null;
        }

        Transform barTransform = FindChildByName(enemy.transform, EnemyBarName);
        BattleHealthBarView view = barTransform != null ? barTransform.GetComponent<BattleHealthBarView>() : null;
        BattleManaBarView manaView = barTransform != null
            ? barTransform.GetComponentInChildren<BattleManaBarView>(true)
            : null;
        Image typeImage = barTransform != null
            ? FindImageByName(barTransform, TypeIconChildName)
            : null;

        if (barTransform == null || view == null)
        {
            EnemyBarBuildResult built = BuildEnemyBar(enemy);
            if (built == null)
            {
                return null;
            }

            barTransform = built.Root;
            view = built.HealthView;
            manaView = built.ManaView;
            typeImage = built.TypeImage;
        }

        barTransform.gameObject.SetActive(true);
        SetWorldScale(barTransform, EnemyBarWorldScale);
        view.ConfigureBillboard(true);
        view.AlignToBoxCollider(enemy);
        view.Bind(health, enemy.transform);

        if (manaView != null && mp != null)
        {
            manaView.Bind(mp);
        }

        if (typeImage != null)
        {
            typeImage.sprite = typeIcon;
            typeImage.preserveAspect = true;
            typeImage.gameObject.SetActive(typeIcon != null);
        }

        return view;
    }

    /// <summary>Player 내부의 기존 HP 바를 우선 연결하고 없을 때만 Resources Prefab을 생성한다.</summary>
    public static BattleHealthBarView AttachPlayerBar(GameObject player, BattleHealth health)
    {
        if (player == null || health == null)
        {
            return null;
        }

        Transform barTransform = FindChildByName(player.transform, PlayerBarName);
        if (barTransform == null)
        {
            GameObject prefab = Resources.Load<GameObject>(PlayerResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"체력 바 Resources를 찾지 못했습니다: {PlayerResourcePath}", player);
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, player.transform, false);
            instance.name = PlayerBarName;
            barTransform = instance.transform;
        }

        // Player HUD는 화면 고정 UI이므로 월드 타일과 같은 평면을 바라보도록 고정한다.
        barTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        BattleHealthBarView view = barTransform.GetComponent<BattleHealthBarView>();
        if (view == null)
        {
            view = barTransform.gameObject.AddComponent<BattleHealthBarView>();
        }

        barTransform.gameObject.SetActive(true);
        view.ConfigureWorldRotationLock(true, new Vector3(90f, 0f, 0f));
        view.AlignToBoxCollider(player);
        view.Bind(health, null);
        return view;
    }

    private sealed class EnemyBarBuildResult
    {
        public Transform Root;
        public BattleHealthBarView HealthView;
        public BattleManaBarView ManaView;
        public Image TypeImage;
    }

    /// <summary>
    /// BattleEnemyStatusView와 같은 방식으로 먼저 순수 코드로 World Space Canvas 래퍼를 만들고,
    /// 그 안에 아트 프리팹(NewHpPre 등)의 시각 요소만 자식으로 붙인다.
    /// 이미 만들어진(살아있는) 프리팹 인스턴스에 나중에 AddComponent&lt;Canvas&gt;를 붙이면
    /// 드물게 참조가 깨지는 문제가 재현되어(런타임 MissingReferenceException), 처음부터
    /// Canvas를 가진 오브젝트로 시작하는 이 방식이 훨씬 안전하다.
    /// </summary>
    private static EnemyBarBuildResult BuildEnemyBar(GameObject enemy)
    {
        GameObject content = Resources.Load<GameObject>(EnemyResourcePath);
        if (content == null)
        {
            Debug.LogWarning($"체력 바 Resources를 찾지 못했습니다: {EnemyResourcePath}", enemy);
            return null;
        }

        GameObject wrapper = new GameObject(EnemyBarName, typeof(RectTransform), typeof(Canvas));
        wrapper.transform.SetParent(enemy.transform, false);

        Canvas canvas = wrapper.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 210;

        RectTransform rect = wrapper.GetComponent<RectTransform>();
        SetWorldScale(rect, EnemyBarWorldScale);

        GameObject visual = Object.Instantiate(content, wrapper.transform, false);
        visual.name = "Visual";

        BattleHealthBarView view = wrapper.AddComponent<BattleHealthBarView>();
        Image hpImage = FindImageByName(visual.transform, HpFillChildName);
        if (hpImage == null)
        {
            Debug.LogWarning($"체력 바 프리팹에서 '{HpFillChildName}' 채움 이미지를 찾지 못했습니다.", enemy);
        }

        view.ConfigureFillImage(hpImage);

        BattleManaBarView manaView = null;
        Image mpImage = FindImageByName(visual.transform, MpFillChildName);
        if (mpImage != null)
        {
            manaView = wrapper.AddComponent<BattleManaBarView>();
            manaView.ConfigureFillImage(mpImage);
        }

        Image typeImage = FindImageByName(visual.transform, TypeIconChildName);

        return new EnemyBarBuildResult
        {
            Root = wrapper.transform,
            HealthView = view,
            ManaView = manaView,
            TypeImage = typeImage
        };
    }

    private static Image FindImageByName(Transform root, string targetName)
    {
        Transform found = FindChildByName(root, targetName);
        return found != null ? found.GetComponent<Image>() : null;
    }

    /// <summary>
    /// Keeps World Space UI at a stable world size even when the Enemy parent is normalized.
    /// The local scale compensates for every axis of the parent's lossy scale.
    /// </summary>
    private static void SetWorldScale(Transform target, float worldScale)
    {
        if (target == null) return;
        Transform parent = target.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
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
