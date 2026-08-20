using TMPro;
using UnityEngine;

/// <summary>Enemy 머리 위에 조작 불능 상태와 남은 턴을 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyStatusView : MonoBehaviour
{
    private const float StatusCanvasWorldScale = 0.01f;
    [InspectorName("Fallback World Offset")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.65f, 0f);
    [InspectorName("Height Above Collider")]
    [SerializeField, Min(0f)] private float heightAboveCollider = 0.45f;
    [InspectorName("Pull Toward Camera")]
    [Tooltip("Moves the World Space UI slightly toward the camera so the enemy mesh cannot hide it.")]
    [SerializeField, Min(0f)] private float cameraPullDistance = 0.2f;
    [SerializeField] private Color stunColor = new Color(1f, 0.82f, 0.15f, 1f);
    [SerializeField] private Color rootColor = new Color(1f, 0.42f, 0.18f, 1f);
    [SerializeField] private Color statusColor = new Color(0.75f, 0.92f, 1f, 1f);
    private BattleEnemyControlState state;
    private BattleStatusEffects statusEffects;
    private Canvas canvas;
    private TMP_Text label;
    private Collider ownerCollider;

    private void Awake()
    {
        state = GetComponent<BattleEnemyControlState>();
        statusEffects = GetComponent<BattleStatusEffects>();
        ownerCollider = GetComponentInChildren<Collider>();
        EnsureView();
    }

    private void OnEnable()
    {
        if (state == null) state = GetComponent<BattleEnemyControlState>();
        if (state != null) state.Changed += Refresh;
        BindStatus(statusEffects != null ? statusEffects : GetComponent<BattleStatusEffects>());
        RefreshAll();
    }

    private void OnDisable()
    {
        if (state != null) state.Changed -= Refresh;
        if (statusEffects != null) statusEffects.Changed -= RefreshStatus;
    }

    public void BindStatus(BattleStatusEffects effects)
    {
        if (statusEffects != null) statusEffects.Changed -= RefreshStatus;
        statusEffects = effects;
        if (statusEffects != null) statusEffects.Changed += RefreshStatus;
        RefreshAll();
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (canvas != null && camera != null)
        {
            Vector3 anchorPosition;
            if (ownerCollider != null)
            {
                Bounds bounds = ownerCollider.bounds;
                anchorPosition = new Vector3(
                    bounds.center.x,
                    bounds.max.y + heightAboveCollider,
                    bounds.center.z);
            }
            else
            {
                anchorPosition = transform.position + worldOffset;
            }

            Vector3 towardCamera = camera.transform.position - anchorPosition;
            if (towardCamera.sqrMagnitude > 0.0001f)
                anchorPosition += towardCamera.normalized * cameraPullDistance;

            canvas.transform.position = anchorPosition;
            canvas.transform.rotation = camera.transform.rotation;
        }
    }

    private void EnsureView()
    {
        if (canvas != null) return;
        GameObject root = new GameObject("Enemy Status View", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 220;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240f, 42f);
        Vector3 parentScale = transform.lossyScale;
        rect.localScale = new Vector3(
            StatusCanvasWorldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            StatusCanvasWorldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            StatusCanvasWorldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));

        GameObject text = new GameObject("Status Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        text.transform.SetParent(root.transform, false);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        label = text.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;

    }

    private void Refresh(BattleEnemyControlState changedState)
    {
        RefreshAll();
    }

    private void RefreshStatus(BattleStatusEffects changedStatus)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        EnsureView();
        string controlText = string.Empty;
        if (state != null && state.StunTurns > 0)
        {
            controlText = $"<color=#{ColorUtility.ToHtmlStringRGB(stunColor)}>Stun {state.StunTurns}</color>";
        }
        if (state != null && state.RootTurns > 0)
        {
            if (controlText.Length > 0) controlText += "  ";
            controlText += $"<color=#{ColorUtility.ToHtmlStringRGB(rootColor)}>Root {state.RootTurns}</color>";
        }
        string rawStatusText = statusEffects != null ? statusEffects.BuildCompactLabel() : string.Empty;
        string statusText = string.IsNullOrEmpty(rawStatusText)
            ? string.Empty
            : $"<color=#{ColorUtility.ToHtmlStringRGB(statusColor)}>{rawStatusText}</color>";
        string combinedStatus = string.IsNullOrEmpty(controlText) ? statusText :
            string.IsNullOrEmpty(statusText) ? controlText : controlText + "  " + statusText;
        label.text = combinedStatus;
        canvas.gameObject.SetActive(!string.IsNullOrEmpty(label.text));
    }
}
