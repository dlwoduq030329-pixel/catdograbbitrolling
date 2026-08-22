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

    /// <summary>
    /// 전달받은 Enemy 내부에서 기존 EnemyHPBar를 먼저 찾아 재사용하고, 없거나 필수 View가 빠졌으면
    /// World Space Canvas와 HP/MP 아트 프리팹을 새로 조립한다. 완성된 View에는 체력·MP 데이터,
    /// Enemy 추적 대상과 적 유형 아이콘을 연결하고 최종 체력 View를 반환한다.
    /// </summary>
    public static BattleHealthBarView AttachEnemyBar(
        GameObject enemyObject,
        BattleHealth enemyHealth,
        BattleUnitMP enemyMana = null,
        Sprite enemyTypeIcon = null)
    {
        if (enemyObject == null || enemyHealth == null)
        {
            return null;
        }

        Transform existingBarRoot = FindDescendantByName(enemyObject.transform, EnemyBarName);
        BattleHealthBarView healthBarView = existingBarRoot != null
            ? existingBarRoot.GetComponent<BattleHealthBarView>()
            : null;
        BattleManaBarView manaBarView = existingBarRoot != null
            ? existingBarRoot.GetComponentInChildren<BattleManaBarView>(true)
            : null;
        Image enemyTypeImage = existingBarRoot != null
            ? FindDescendantImageByName(existingBarRoot, TypeIconChildName)
            : null;

        if (existingBarRoot == null || healthBarView == null)
        {
            EnemyBarBuildResult builtBar = BuildEnemyWorldBar(enemyObject);
            if (builtBar == null)
            {
                return null;
            }

            existingBarRoot = builtBar.BarRoot;
            healthBarView = builtBar.HealthBarView;
            manaBarView = builtBar.ManaBarView;
            enemyTypeImage = builtBar.EnemyTypeImage;
        }

        existingBarRoot.gameObject.SetActive(true);
        ApplyParentScaleCompensatedWorldSize(existingBarRoot, EnemyBarWorldScale);
        healthBarView.ConfigureBillboard(true);
        healthBarView.AlignToBoxCollider(enemyObject);
        healthBarView.Bind(enemyHealth, enemyObject.transform);

        if (manaBarView != null && enemyMana != null)
        {
            manaBarView.Bind(enemyMana);
        }

        if (enemyTypeImage != null)
        {
            enemyTypeImage.sprite = enemyTypeIcon;
            enemyTypeImage.preserveAspect = true;
            enemyTypeImage.gameObject.SetActive(enemyTypeIcon != null);
        }

        return healthBarView;
    }

    /// <summary>
    /// 전달받은 Player 오브젝트의 자식 계층에서 기존 PlayerHPBar를 찾아 재사용하고,
    /// 없으면 Resources 프리팹을 Player 자식으로 생성한다. 체력 View가 프리팹에 없으면 추가한 뒤
    /// Player Collider 기준 위치, 고정 월드 회전과 체력 데이터 연결을 적용한다.
    /// </summary>
    public static BattleHealthBarView AttachPlayerBar(
        GameObject playerObject,
        BattleHealth playerHealth)
    {
        if (playerObject == null || playerHealth == null)
        {
            return null;
        }

        Transform playerBarRoot = FindDescendantByName(playerObject.transform, PlayerBarName);
        if (playerBarRoot == null)
        {
            GameObject playerBarPrefab = Resources.Load<GameObject>(PlayerResourcePath);
            if (playerBarPrefab == null)
            {
                Debug.LogWarning(
                    $"체력 바 Resources를 찾지 못했습니다: {PlayerResourcePath}",
                    playerObject);
                return null;
            }

            GameObject playerBarInstance = Object.Instantiate(
                playerBarPrefab,
                playerObject.transform,
                false);
            playerBarInstance.name = PlayerBarName;
            playerBarRoot = playerBarInstance.transform;
        }

        // Player HUD는 화면 고정 UI이므로 월드 타일과 같은 평면을 바라보도록 고정한다.
        playerBarRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);

        BattleHealthBarView healthBarView = playerBarRoot.GetComponent<BattleHealthBarView>();
        if (healthBarView == null)
        {
            healthBarView = playerBarRoot.gameObject.AddComponent<BattleHealthBarView>();
        }

        playerBarRoot.gameObject.SetActive(true);
        healthBarView.ConfigureWorldRotationLock(true, new Vector3(90f, 0f, 0f));
        healthBarView.AlignToBoxCollider(playerObject);
        healthBarView.Bind(playerHealth, null);
        return healthBarView;
    }

    /// <summary>새 Enemy 월드 체력 바를 조립한 뒤 호출부에 필요한 루트와 각 View 참조를 함께 반환한다.</summary>
    private sealed class EnemyBarBuildResult
    {
        public Transform BarRoot;
        public BattleHealthBarView HealthBarView;
        public BattleManaBarView ManaBarView;
        public Image EnemyTypeImage;
    }

    /// <summary>
    /// 먼저 순수 코드로 World Space Canvas 래퍼를 만들고,
    /// 그 안에 아트 프리팹(NewHpPre 등)의 시각 요소만 자식으로 붙인다.
    /// 이미 만들어진(살아있는) 프리팹 인스턴스에 나중에 AddComponent&lt;Canvas&gt;를 붙이면
    /// 드물게 참조가 깨지는 문제가 재현되어(런타임 MissingReferenceException), 처음부터
    /// Canvas를 가진 오브젝트로 시작하는 이 방식이 훨씬 안전하다.
    /// </summary>
    private static EnemyBarBuildResult BuildEnemyWorldBar(GameObject enemyObject)
    {
        GameObject enemyBarArtPrefab = Resources.Load<GameObject>(EnemyResourcePath);
        if (enemyBarArtPrefab == null)
        {
            Debug.LogWarning(
                $"체력 바 Resources를 찾지 못했습니다: {EnemyResourcePath}",
                enemyObject);
            return null;
        }

        GameObject worldCanvasRoot = new GameObject(
            EnemyBarName,
            typeof(RectTransform),
            typeof(Canvas));
        worldCanvasRoot.transform.SetParent(enemyObject.transform, false);

        Canvas worldCanvas = worldCanvasRoot.GetComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = 210;

        RectTransform worldCanvasRect = worldCanvasRoot.GetComponent<RectTransform>();
        ApplyParentScaleCompensatedWorldSize(worldCanvasRect, EnemyBarWorldScale);

        GameObject enemyBarArt = Object.Instantiate(
            enemyBarArtPrefab,
            worldCanvasRoot.transform,
            false);
        enemyBarArt.name = "Visual";

        BattleHealthBarView healthBarView = worldCanvasRoot.AddComponent<BattleHealthBarView>();
        Image healthFillImage = FindDescendantImageByName(
            enemyBarArt.transform,
            HpFillChildName);
        if (healthFillImage == null)
        {
            Debug.LogWarning(
                $"체력 바 프리팹에서 '{HpFillChildName}' 채움 이미지를 찾지 못했습니다.",
                enemyObject);
        }

        healthBarView.ConfigureFillImage(healthFillImage);

        BattleManaBarView manaBarView = null;
        Image manaFillImage = FindDescendantImageByName(
            enemyBarArt.transform,
            MpFillChildName);
        if (manaFillImage != null)
        {
            manaBarView = worldCanvasRoot.AddComponent<BattleManaBarView>();
            manaBarView.ConfigureManaFillImage(manaFillImage);
        }

        Image enemyTypeImage = FindDescendantImageByName(
            enemyBarArt.transform,
            TypeIconChildName);

        return new EnemyBarBuildResult
        {
            BarRoot = worldCanvasRoot.transform,
            HealthBarView = healthBarView,
            ManaBarView = manaBarView,
            EnemyTypeImage = enemyTypeImage
        };
    }

    /// <summary>
    /// 전달받은 UI 루트의 전체 자식 계층에서 정확히 같은 이름의 Transform을 찾고 Image를 반환한다.
    /// Unit이나 Road를 찾는 함수가 아니라 Enemy 체력 바 아트 프리팹 내부의 HP·MP·종류 이미지를 연결하기 위한 함수다.
    /// </summary>
    private static Image FindDescendantImageByName(Transform uiRoot, string targetObjectName)
    {
        Transform matchingTransform = FindDescendantByName(uiRoot, targetObjectName);
        return matchingTransform != null ? matchingTransform.GetComponent<Image>() : null;
    }

    /// <summary>
    /// Enemy 모델의 부모 Scale이 달라도 World Space UI가 같은 화면 크기로 보이도록 로컬 Scale을 보정한다.
    /// 각 축의 부모 lossyScale로 목표 월드 크기를 나누어 Enemy 모델 크기 변경이 체력 바 크기에 전파되지 않게 한다.
    /// </summary>
    private static void ApplyParentScaleCompensatedWorldSize(
        Transform worldUiRoot,
        float targetWorldScale)
    {
        if (worldUiRoot == null)
        {
            return;
        }

        Transform parentTransform = worldUiRoot.parent;
        Vector3 parentWorldScale = parentTransform != null
            ? parentTransform.lossyScale
            : Vector3.one;
        worldUiRoot.localScale = new Vector3(
            targetWorldScale / Mathf.Max(0.0001f, Mathf.Abs(parentWorldScale.x)),
            targetWorldScale / Mathf.Max(0.0001f, Mathf.Abs(parentWorldScale.y)),
            targetWorldScale / Mathf.Max(0.0001f, Mathf.Abs(parentWorldScale.z)));
    }

    /// <summary>
    /// 전달받은 루트 자신과 모든 하위 Transform을 깊이 우선으로 순회해 정확히 같은 이름의 오브젝트를 반환한다.
    /// Attach 단계에서는 Unit 자식의 기존 HP 바 루트를 찾고, Build 단계에서는 HP 바 아트 내부 이미지를 찾는 데 사용한다.
    /// </summary>
    private static Transform FindDescendantByName(
        Transform searchRoot,
        string targetObjectName)
    {
        if (searchRoot == null)
        {
            return null;
        }

        if (searchRoot.name == targetObjectName)
        {
            return searchRoot;
        }

        for (int childIndex = 0; childIndex < searchRoot.childCount; childIndex++)
        {
            Transform matchingDescendant = FindDescendantByName(
                searchRoot.GetChild(childIndex),
                targetObjectName);
            if (matchingDescendant != null)
            {
                return matchingDescendant;
            }
        }

        return null;
    }
}
