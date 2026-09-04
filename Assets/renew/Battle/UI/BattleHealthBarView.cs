using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player HUD와 Enemy 월드 UI가 함께 사용하는 공용 체력 표시 컴포넌트다.
/// BattleHealth의 변경 이벤트를 받아 Filled Image와 선택적 텍스트만 갱신한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleHealthBarView : MonoBehaviour
{
    [Header("체력 데이터")]
    [InspectorName("표시할 체력")]
    [SerializeField] private BattleHealth targetHealth;

    [Header("체력 바 UI")]
    [InspectorName("체력 채움 이미지 (Filled)")]
    [SerializeField] private Image fillImage;
    [InspectorName("기존 체력 Slider (선택)")]
    [SerializeField] private Slider healthSlider;
    [InspectorName("현재/최대 체력 텍스트 (선택)")]
    [SerializeField] private TMP_Text healthText;
    [InspectorName("전체 표시 제어 (자동 연결)")]
    [SerializeField] private CanvasGroup visibilityGroup;

    [Header("기존 셰이더 체력 바 (선택)")]
    [InspectorName("진행도 Renderer")]
    [SerializeField] private Renderer progressRenderer;
    [InspectorName("진행도 Shader 속성")]
    [SerializeField] private string progressProperty = "_ProgressBar";

    [Header("표시 설정")]
    [InspectorName("체력을 정수로 표시")]
    [SerializeField] private bool showAsInteger = true;
    [InspectorName("사망 시 체력 바 숨김")]
    [SerializeField] private bool hideWhenDead = true;
    [InspectorName("체력 바 감소 속도")]
    [SerializeField, Min(0.01f)] private float healthChangeSpeed = 2f;

    [Header("Enemy 월드 추적 (선택)")]
    [InspectorName("따라갈 월드 대상")]
    [SerializeField] private Transform worldTarget;
    [InspectorName("월드 UI Canvas")]
    [SerializeField] private Canvas targetCanvas;
    [InspectorName("전투 카메라")]
    [SerializeField] private Camera targetCamera;
    [InspectorName("대상 기준 월드 위치 보정")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Collider 기준 배치")]
    [InspectorName("양쪽 끝 여백 비율")]
    [SerializeField, Range(0f, 0.45f)] private float colliderSidePadding = 0.1f;
    [InspectorName("Collider 위 높이 여백")]
    [SerializeField, Min(0f)] private float colliderHeightOffset = 0.05f;

    [Header("월드 회전 잠금")]
    [InspectorName("월드 회전 고정")]
    [SerializeField] private bool lockWorldRotation = true;
    [InspectorName("고정할 월드 회전")]
    [SerializeField] private Vector3 lockedWorldEulerAngles = new Vector3(90f, 0f, 0f);
    [InspectorName("카메라를 항상 바라보기(빌보드)")]
    [Tooltip("켜면 lockWorldRotation 설정 대신 매 프레임 카메라를 정면으로 바라본다. " +
             "카메라 각도가 0도(사이드뷰)에 가까워질수록 바닥에 눕혀둔 게이지는 옆에서 거의 안 보이므로, " +
             "Enemy처럼 카메라 각도가 자주 바뀌는 대상에는 이 옵션을 켠다.")]
    [SerializeField] private bool billboardToCamera;

    public BattleHealth TargetHealth => targetHealth;

    private Material runtimeProgressMaterial;
    private float targetHealthRatio = 1f;
    private float displayedHealthRatio = 1f;
    private bool hasInitializedRatio;

    private void Awake()
    {
        ResolveUiReferences();
    }

    private void OnEnable()
    {
        Bind(targetHealth, worldTarget);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (runtimeProgressMaterial != null)
        {
            Destroy(runtimeProgressMaterial);
        }
    }

    private void LateUpdate()
    {
        if (hasInitializedRatio)
        {
            displayedHealthRatio = Mathf.MoveTowards(
                displayedHealthRatio,
                targetHealthRatio,
                healthChangeSpeed * Time.unscaledDeltaTime);
            ApplyVisualRatio(displayedHealthRatio);

            if (hideWhenDead && targetHealth != null && targetHealth.IsDead && displayedHealthRatio <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        UpdateWorldPosition();
        ApplyWorldRotationLock();
    }

    /// <summary>
    /// 표시할 체력과 선택적 월드 추적 대상을 연결한다.
    /// Player HUD는 worldTarget을 null로 전달하면 현재 UI 위치를 유지한다.
    /// </summary>
    public void Bind(BattleHealth health, Transform followTarget = null)
    {
        Unsubscribe();
        targetHealth = health;
        worldTarget = followTarget;

        if (targetHealth != null)
        {
            targetHealth.HealthChanged -= HandleHealthChanged;
            targetHealth.Died -= HandleHealthChanged;
            targetHealth.HealthChanged += HandleHealthChanged;
            targetHealth.Died += HandleHealthChanged;
        }

        Refresh();
    }

    /// <summary>
    /// 캐릭터 BoxCollider의 양 끝을 기준으로 HP 바를 중앙 정렬한다.
    /// 가로 폭은 양쪽 여백만큼 줄여 Collider 시작점과 끝점에 닿지 않게 한다.
    /// </summary>
    public void AlignToBoxCollider(GameObject owner)
    {
        if (owner == null)
        {
            return;
        }

        Bounds colliderBounds;
        BoxCollider ownerCollider = owner.GetComponentInChildren<BoxCollider>();
        if (ownerCollider != null)
        {
            colliderBounds = ownerCollider.bounds;
        }
        else if (TryGetOwnerVisualBounds(owner, out Bounds visualBounds))
        {
            // 2026-09-05: 지금 Enemy 데이터의 prefab은 콜라이더 없는 순수 모델(FBX)인 경우가 많다.
            // 예전에는 BoxCollider가 없으면 여기서 그냥 return해버려서 HP 바가 처음 생성된 위치
            // (부모 오브젝트 피벗, 보통 발밑 근처)에 그대로 남아 Enemy 모델과 겹쳐 보였다
            // ("HP가 너무 낮아 Enemy랑 겹친다" 피드백). BoxCollider가 없을 때는 EnemySpawner의
            // 발판 크기 계산과 같은 방식(MeshRenderer/SkinnedMeshRenderer 합산 Bounds)으로 대체해서
            // 최소한 모델 머리 위로는 항상 배치되게 한다.
            colliderBounds = visualBounds;
            Debug.LogWarning(
                $"HP 바 배치용 BoxCollider가 없어 모델 Renderer Bounds로 대체합니다: {owner.name}", owner);
        }
        else
        {
            Debug.LogWarning($"HP 바 배치에 사용할 BoxCollider나 Renderer가 없습니다: {owner.name}", owner);
            return;
        }

        Vector3 center = colliderBounds.center;
        center.y = colliderBounds.max.y + colliderHeightOffset;

        // 하트·화살촉 등 장식 렌더러가 자체 중심(pivot)과 어긋나 있어도
        // HP 바 전체(모든 자식 렌더러 합산 Bounds)가 Collider 중심에 오도록
        // 어긋난 만큼(pivotOffset)을 반영해 오브젝트 전체를 그대로 평행 이동한다.
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        if (allRenderers.Length > 0)
        {
            Bounds combinedBounds = allRenderers[0].bounds;
            for (int i = 1; i < allRenderers.Length; i++)
            {
                combinedBounds.Encapsulate(allRenderers[i].bounds);
            }

            Vector3 pivotOffset = transform.position - combinedBounds.center;
            center.x += pivotOffset.x;
            center.z += pivotOffset.z;
        }

        transform.position = center;

        Renderer barRenderer = progressRenderer != null
            ? progressRenderer
            : GetComponentInChildren<Renderer>(true);
        if (barRenderer == null || barRenderer.bounds.size.x <= Mathf.Epsilon)
        {
            return;
        }

        float desiredWidth = colliderBounds.size.x * (1f - colliderSidePadding * 2f);
        Vector3 scale = transform.localScale;
        scale.x *= desiredWidth / barRenderer.bounds.size.x;
        transform.localScale = scale;
    }

    /// <summary>코드에서 동적으로 조립한 바에 채움 이미지를 연결한다(Inspector 배선 없이 런타임 조립할 때 사용).</summary>
    public void ConfigureFillImage(Image image)
    {
        fillImage = image;
    }

    /// <summary>부모 Enemy나 Camera 상태와 무관하게 HP 바의 월드 회전을 고정한다.</summary>
    public void ConfigureWorldRotationLock(bool enabled, Vector3 worldEulerAngles)
    {
        billboardToCamera = false;
        lockWorldRotation = enabled;
        lockedWorldEulerAngles = worldEulerAngles;
        ApplyWorldRotationLock();
    }

    /// <summary>고정 회전 대신 매 프레임 카메라를 정면으로 바라보게 한다(Enemy 등 카메라 각도가 자주 바뀌는 대상용).</summary>
    public void ConfigureBillboard(bool enabled)
    {
        billboardToCamera = enabled;
        ApplyWorldRotationLock();
    }

    /// <summary>현재 체력 비율과 텍스트를 즉시 다시 표시한다.</summary>
    public void Refresh()
    {
        if (targetHealth == null)
        {
            targetHealthRatio = 0f;
            displayedHealthRatio = 0f;
            hasInitializedRatio = true;
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
            }

            if (healthSlider != null)
            {
                healthSlider.value = 0f;
            }

            SetShaderProgress(0f);

            if (healthText != null)
            {
                healthText.text = "- / -";
            }

            return;
        }

        float ratio = targetHealth.MaxHealth > 0f
            ? targetHealth.CurrentHealth / targetHealth.MaxHealth
            : 0f;

        targetHealthRatio = Mathf.Clamp01(ratio);
        if (!hasInitializedRatio)
        {
            displayedHealthRatio = targetHealthRatio;
            hasInitializedRatio = true;
        }

        ApplyVisualRatio(displayedHealthRatio);

        if (healthText != null)
        {
            healthText.text = showAsInteger
                ? $"{Mathf.CeilToInt(targetHealth.CurrentHealth)} / {Mathf.CeilToInt(targetHealth.MaxHealth)}"
                : $"{targetHealth.CurrentHealth:0.##} / {targetHealth.MaxHealth:0.##}";
        }

    }

    private void ApplyVisualRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        if (fillImage != null)
        {
            // fillMethod/fillOrigin은 프리팹에서 미리 설정한 값을 그대로 존중한다
            // (세로로 차오르는 실린더형 게이지 등, Horizontal이 아닌 프리팹도 있기 때문).
            fillImage.type = Image.Type.Filled;
            fillImage.fillAmount = ratio;
        }

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = ratio;
        }

        SetShaderProgress(ratio);
    }

    private void ResolveUiReferences()
    {
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<BattleHealth>();
        }

        if (healthSlider == null)
        {
            healthSlider = GetComponentInChildren<Slider>(true);
        }

        if (progressRenderer == null)
        {
            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
            {
                Material candidateMaterial = candidate.sharedMaterial;
                if (candidateMaterial != null && candidateMaterial.HasProperty(progressProperty))
                {
                    progressRenderer = candidate;
                    break;
                }
            }
        }

        // Slider와 셰이더형 Prefab은 자체 진행도 구조를 사용하므로 임의의 배경 Image를 Fill로 선택하지 않는다.
        if (fillImage == null && healthSlider == null && progressRenderer == null)
        {
            fillImage = GetComponentInChildren<Image>(true);
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (visibilityGroup == null)
        {
            visibilityGroup = GetComponent<CanvasGroup>();
        }

        if (visibilityGroup == null)
        {
            visibilityGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void UpdateWorldPosition()
    {
        if (worldTarget == null)
        {
            return;
        }

        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 screenPosition = camera.WorldToScreenPoint(worldTarget.position + worldOffset);
        bool isVisible = screenPosition.z > 0f;

        if (targetCanvas != null && targetCanvas.renderMode == RenderMode.WorldSpace)
        {
            transform.position = worldTarget.position + worldOffset;
        }
        else
        {
            transform.position = screenPosition;
        }

        if (visibilityGroup != null)
        {
            visibilityGroup.alpha = isVisible ? 1f : 0f;
            visibilityGroup.interactable = false;
            visibilityGroup.blocksRaycasts = false;
        }
    }

    private void ApplyWorldRotationLock()
    {
        if (billboardToCamera)
        {
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera != null)
            {
                // 2026-09-05: 카메라 회전을 통째로(rotation 전체) 복사하면 카메라가 내려다보는
                // 각도(피치)까지 HP 바가 그대로 물려받아, 카메라가 위에서 아래로 보는 전투 카메라에서는
                // 바가 세워지지 않고 바닥 쪽으로 눕는 것처럼 보였다("x rotation 값이 이상함" 피드백).
                // HP 바는 항상 똑바로 세워진 채로 카메라 쪽(좌우, Y축)만 따라 돌아야 하므로,
                // 카메라의 Y축(좌우 회전)만 반영하고 X·Z는 항상 0으로 고정한다.
                float cameraYawDegrees = camera.transform.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0f, cameraYawDegrees, 0f);
            }

            return;
        }

        if (lockWorldRotation)
        {
            transform.rotation = Quaternion.Euler(lockedWorldEulerAngles);
        }
    }

    private void HandleHealthChanged(BattleHealth health)
    {
        Refresh();
    }

    /// <summary>원본 머티리얼을 변경하지 않도록 인스턴스 머티리얼의 진행도 속성만 갱신한다.</summary>
    private void SetShaderProgress(float ratio)
    {
        if (progressRenderer == null || string.IsNullOrWhiteSpace(progressProperty))
        {
            return;
        }

        if (runtimeProgressMaterial == null)
        {
            runtimeProgressMaterial = progressRenderer.material;
        }

        if (runtimeProgressMaterial.HasProperty(progressProperty))
        {
            runtimeProgressMaterial.SetFloat(progressProperty, ratio);
        }
    }

    /// <summary>
    /// owner(HP 바 소유자, 보통 Enemy 루트)의 실제 모델 Renderer(MeshRenderer/SkinnedMeshRenderer만) Bounds를
    /// 합산한다. BoxCollider가 없는 순수 모델 프리팹에서 AlignToBoxCollider가 대신 사용하는 대체 기준이다.
    /// EnemySpawner.TryGetVisualBounds와 같은 필터링 기준(파티클·UI Renderer 제외)을 그대로 따른다.
    /// </summary>
    private static bool TryGetOwnerVisualBounds(GameObject owner, out Bounds bounds)
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

    private void Unsubscribe()
    {
        if (targetHealth == null)
        {
            return;
        }

        targetHealth.HealthChanged -= HandleHealthChanged;
        targetHealth.Died -= HandleHealthChanged;
    }
}
