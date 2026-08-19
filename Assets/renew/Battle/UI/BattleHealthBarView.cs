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

        BoxCollider ownerCollider = owner.GetComponentInChildren<BoxCollider>();
        if (ownerCollider == null)
        {
            Debug.LogWarning($"HP 바 배치에 사용할 BoxCollider가 없습니다: {owner.name}", owner);
            return;
        }

        Bounds colliderBounds = ownerCollider.bounds;
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

    /// <summary>부모 Enemy나 Camera 상태와 무관하게 HP 바의 월드 회전을 고정한다.</summary>
    public void ConfigureWorldRotationLock(bool enabled, Vector3 worldEulerAngles)
    {
        lockWorldRotation = enabled;
        lockedWorldEulerAngles = worldEulerAngles;
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
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
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
