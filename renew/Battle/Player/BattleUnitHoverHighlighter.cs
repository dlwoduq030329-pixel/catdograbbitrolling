using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마우스 아래의 Player 또는 Enemy Collider를 감지해 캐릭터 Renderer에 진영별 색 강조를 적용한다.
/// 선택·이동 규칙에는 관여하지 않으며 포인터가 벗어나면 기존 MaterialPropertyBlock을 복구한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleUnitHoverHighlighter : MonoBehaviour
{
    private sealed class RendererState
    {
        public Renderer Renderer;
        public readonly List<MaterialPropertyBlock> OriginalBlocks = new List<MaterialPropertyBlock>();
    }

    [Header("마우스 오버 강조")]
    [InspectorName("플레이어 강조색")]
    [SerializeField] private Color playerHoverColor = new Color(0.15f, 1f, 0.25f, 1f);
    [InspectorName("적 강조색")]
    [SerializeField] private Color enemyHoverColor = new Color(1f, 0.12f, 0.12f, 1f);
    [InspectorName("강조 혼합 강도")]
    [SerializeField, Range(0f, 1f)] private float colorBlend = 0.65f;
    [InspectorName("마우스 감지 거리")]
    [SerializeField, Min(1f)] private float rayDistance = 500f;

    private readonly List<RendererState> highlightedRenderers = new List<RendererState>();
    private Camera raycastCamera;
    private GameObject player;
    private GameObject highlightedUnit;

    public void Configure(Camera camera, GameObject targetPlayer)
    {
        raycastCamera = camera;
        player = targetPlayer;
    }

    private void Update()
    {
        if (raycastCamera == null || !raycastCamera.isActiveAndEnabled)
        {
            RestoreHighlight();
            return;
        }

        // 상점 등 모달 UI가 열려 있는 동안에는 뒤쪽 유닛에 마우스 오버 강조가 비치면 안 된다.
        // Physics.RaycastAll은 화면 위 UI와 무관하게 항상 맞기 때문에 별도로 잠가야 한다.
        if (BattleGameManager.Instance != null && BattleGameManager.Instance.IsModalInteractionOpen)
        {
            RestoreHighlight();
            return;
        }

        GameObject hoveredUnit = FindHoveredUnit(Input.mousePosition, out Color hoverColor);
        if (hoveredUnit == highlightedUnit)
        {
            return;
        }

        RestoreHighlight();
        if (hoveredUnit != null)
        {
            ApplyHighlight(hoveredUnit, hoverColor);
        }
    }

    private void OnDisable()
    {
        RestoreHighlight();
    }

    private void OnDestroy()
    {
        RestoreHighlight();
    }

    private GameObject FindHoveredUnit(Vector2 pointerPosition, out Color hoverColor)
    {
        hoverColor = Color.clear;
        Ray ray = raycastCamera.ScreenPointToRay(pointerPosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            rayDistance,
            ~0,
            QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider != null ? hit.collider.transform : null;
            if (hitTransform == null)
            {
                continue;
            }

            if (player != null &&
                (hitTransform == player.transform || hitTransform.IsChildOf(player.transform)))
            {
                hoverColor = playerHoverColor;
                return player;
            }

            EnemyTurnActor enemy = hitTransform.GetComponentInParent<EnemyTurnActor>();
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                hoverColor = enemyHoverColor;
                return enemy.gameObject;
            }
        }

        return null;
    }

    private void ApplyHighlight(GameObject unit, Color hoverColor)
    {
        highlightedUnit = unit;
        foreach (Renderer renderer in unit.GetComponentsInChildren<Renderer>(true))
        {
            if (!ShouldHighlight(renderer))
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            RendererState state = new RendererState { Renderer = renderer };
            bool changed = false;

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock, materialIndex);
                state.OriginalBlocks.Add(originalBlock);

                if (material == null)
                {
                    continue;
                }

                MaterialPropertyBlock highlightedBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(highlightedBlock, materialIndex);

                if (material.HasProperty("_BaseColor"))
                {
                    Color originalColor = material.GetColor("_BaseColor");
                    highlightedBlock.SetColor("_BaseColor", Color.Lerp(originalColor, hoverColor, colorBlend));
                    changed = true;
                }

                if (material.HasProperty("_Color"))
                {
                    Color originalColor = material.GetColor("_Color");
                    highlightedBlock.SetColor("_Color", Color.Lerp(originalColor, hoverColor, colorBlend));
                    changed = true;
                }

                renderer.SetPropertyBlock(highlightedBlock, materialIndex);
            }

            if (changed)
            {
                highlightedRenderers.Add(state);
            }
            else
            {
                RestoreRendererState(state);
            }
        }
    }

    private static bool ShouldHighlight(Renderer renderer)
    {
        if (renderer == null || renderer is LineRenderer || renderer is ParticleSystemRenderer)
        {
            return false;
        }

        return renderer.GetComponentInParent<BattleHealthBarView>() == null;
    }

    private void RestoreHighlight()
    {
        foreach (RendererState state in highlightedRenderers)
        {
            RestoreRendererState(state);
        }

        highlightedRenderers.Clear();
        highlightedUnit = null;
    }

    private static void RestoreRendererState(RendererState state)
    {
        if (state == null || state.Renderer == null)
        {
            return;
        }

        for (int materialIndex = 0; materialIndex < state.OriginalBlocks.Count; materialIndex++)
        {
            state.Renderer.SetPropertyBlock(state.OriginalBlocks[materialIndex], materialIndex);
        }
    }
}
