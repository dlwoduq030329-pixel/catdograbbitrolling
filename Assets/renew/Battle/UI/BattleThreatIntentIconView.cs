using System.Collections.Generic;
using UnityEngine;

/// <summary>Enemy HP UI 위의 검·눈 아이콘 생성, 위치 추적과 카메라 방향 보정만 담당한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleThreatIntentIconView : MonoBehaviour
{
    private const string AttackIconResourcePath = "Battle/UI/ThreatIcons/EnemyIntent_Attack";
    private const string ChaseIconResourcePath = "Battle/UI/ThreatIcons/EnemyIntent_Chase";

    [SerializeField] private Camera targetCamera;
    [Tooltip("다음 Enemy 턴에 Player를 확실히 공격하는 경우 표시할 검 아이콘입니다.")]
    [SerializeField] private Sprite attackIcon;
    [Tooltip("다음 Enemy 턴에 Player 방향으로 이동하는 경우 표시할 눈 아이콘입니다.")]
    [SerializeField] private Sprite chaseIcon; 
    [SerializeField, Min(0.1f)] private float iconWorldSize = 0.8f;
    [SerializeField] private float fallbackIconHeight = 2.8f;
    [SerializeField, Min(0f)] private float iconGapAboveHealthBar = 0.06f;

    private readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();
    private readonly List<Transform> enemyTargets = new List<Transform>();

    private void Awake()
    {
        LoadDefaultIconsIfNeeded();
    }

    /// <summary>카메라가 회전해도 아이콘이 화면을 향하도록 전투 카메라를 직접 전달받는다.</summary>
    public void SetCamera(Camera battleCamera) => targetCamera = battleCamera;

    public void Show(IReadOnlyList<EnemyThreatPreviewData> threats)
    {
        int count = threats != null ? threats.Count : 0;
        EnsureIconCount(count);
        for (int i = 0; i < icons.Count; i++)
        {
            // Fog 시스템: 아이콘이 Enemy의 정확한 위치(HP바 위)에 그대로 붙으므로, 안개에 가려
            // 안 보여야 할 Enemy까지 아이콘으로 위치가 드러나지 않도록 threat line과 같은 기준으로
            // 가려진 Enemy는 아이콘도 띄우지 않는다.
            FogRevealVisibility enemyVisibility = i < count && threats[i].Enemy != null
                ? threats[i].Enemy.GetComponentInParent<FogRevealVisibility>()
                : null;
            bool enemyHiddenByFog = enemyVisibility != null && !enemyVisibility.IsRevealed;

            bool visible = i < count && threats[i].Enemy != null && !enemyHiddenByFog;
            icons[i].enabled = visible;
            enemyTargets[i] = visible ? threats[i].Enemy.transform : null;
            if (!visible) continue;
            // AI가 확정한 의도만 읽는다. 이 View는 공격·추격 여부를 다시 계산하지 않는다.
            icons[i].sprite = threats[i].Intent == EnemyThreatIntent.Attack ? attackIcon : chaseIcon;
            FitIconToWorldSize(icons[i]);
        }
        RefreshTransforms();
    }

    public void RefreshTransforms()
    {
        Quaternion rotation = targetCamera != null ? targetCamera.transform.rotation : Quaternion.identity;
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null || !icons[i].enabled || enemyTargets[i] == null) continue;
            icons[i].transform.rotation = rotation;
            icons[i].transform.position = ResolveIconPosition(enemyTargets[i]);
        }
    }

    public void HideAll()
    {
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] != null) icons[i].enabled = false;
            enemyTargets[i] = null;
        }
    }

    private void EnsureIconCount(int requiredCount)
    {
        while (icons.Count < requiredCount)
        {
            GameObject child = new GameObject("Enemy Intent Icon");
            child.transform.SetParent(transform, false);
            SpriteRenderer icon = child.AddComponent<SpriteRenderer>();
            icon.sortingOrder = 100;
            icon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            icon.receiveShadows = false;
            icons.Add(icon);
            enemyTargets.Add(null);
        }
    }

    private void OnValidate()
    {
        // RequireComponent에 의해 Play Mode에서 동적으로 추가될 때는 아직 Inspector 값을
        // 받을 시점이 아니므로, 정상적인 초기화 과정을 설정 누락으로 경고하지 않는다.
        if (!Application.isPlaying && (attackIcon == null || chaseIcon == null))
            Debug.LogWarning("Enemy 위협 검·눈 아이콘을 Inspector에 직접 연결해야 합니다.", this);
    }

    private void LoadDefaultIconsIfNeeded()
    {
        if (attackIcon == null)
            attackIcon = Resources.Load<Sprite>(AttackIconResourcePath);
        if (chaseIcon == null)
            chaseIcon = Resources.Load<Sprite>(ChaseIconResourcePath);

        if (attackIcon == null || chaseIcon == null)
            Debug.LogError("Enemy 위협 아이콘 Resources를 찾지 못했습니다.", this);
    }

    private void FitIconToWorldSize(SpriteRenderer icon)
    {
        float spriteSize = icon.sprite != null
            ? Mathf.Max(icon.sprite.bounds.size.x, icon.sprite.bounds.size.y)
            : 1f;
        icon.transform.localScale = Vector3.one * (iconWorldSize / Mathf.Max(0.001f, spriteSize));
    }

    private Vector3 ResolveIconPosition(Transform enemy)
    {
        Vector3 cameraUp = targetCamera != null ? targetCamera.transform.up : Vector3.up;
        Transform healthBar = FindDescendantByName(enemy, "EnemyHPBar");
        if (healthBar == null) return enemy.position + cameraUp * fallbackIconHeight;

        float enemyProjection = Vector3.Dot(enemy.position, cameraUp);
        float highestProjection = enemyProjection + fallbackIconHeight;
        RectTransform[] rects = healthBar.GetComponentsInChildren<RectTransform>(true);
        Vector3[] corners = new Vector3[4];
        foreach (RectTransform rect in rects)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) continue;
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
                highestProjection = Mathf.Max(highestProjection, Vector3.Dot(corner, cameraUp));
        }
        return enemy.position + cameraUp *
            (highestProjection - enemyProjection + iconGapAboveHealthBar + iconWorldSize * 0.5f);
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDescendantByName(child, targetName);
            if (found != null) return found;
        }
        return null;
    }
}
