using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마우스 아래의 Player 또는 Enemy Collider를 감지해 캐릭터 Renderer에 진영별 색 강조를 적용한다.
/// 선택·이동 규칙에는 관여하지 않으며 포인터가 벗어나면 기존 MaterialPropertyBlock을 복구한다.
///
/// BattleRaycaster와는 별개로 이 클래스 자신만의 Physics.RaycastAll 호출(FindHoveredUnit)을 갖고
/// 있다 — BattleRaycaster.TryGetPlayer/TryGetEnemy를 재사용하지 않는 중복 구현이라, Player 폴더 이름
/// 정리를 마무리하는 통합 작업에서 함께 검토할 후보다(단, 여기는 히트를 거리순 정렬까지 하는 차이가 있음).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleUnitHoverHighlighter : MonoBehaviour
{
    // 강조를 적용하기 직전 한 Renderer의 원래 MaterialPropertyBlock들을 통째로 기억해두는 스냅샷.
    // ApplyHighlight()가 강조를 걸 때마다 하나씩 만들어 highlightedRenderers에 쌓아두고,
    // RestoreHighlight()가 여기 저장된 값 그대로 되돌리는 데(RestoreRendererState) 사용한다.
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

    /// <summary>강조 판정에 사용할 카메라와 Player 참조를 연결한다.</summary>
    public void AttachReferences(Camera camera, GameObject targetPlayer)
    {
        raycastCamera = camera;
        player = targetPlayer;
    }

    /// <summary>
    /// 매 프레임 "지금 마우스 아래 있는 유닛"을 다시 판정해서 강조 상태를 갱신한다. 처리 순서:
    /// 1. 카메라가 없거나 꺼져 있으면 강조 전부 지우고 끝(RestoreHighlight).
    /// 2. 모달(상점 등)이 열려 있으면 마찬가지로 강조를 지우고 끝 — Physics.RaycastAll은 화면 위 UI를
    ///    무시하고 그 뒤 3D 오브젝트를 그대로 맞히기 때문에, 모달 뒤 유닛이 강조되는 걸 막으려면 직접 잠가야 한다.
    /// 3. FindHoveredUnit()으로 이번 프레임에 마우스가 어떤 유닛 위에 있는지(Player/Enemy/없음) 찾는다.
    /// 4. 지난 프레임과 같은 유닛이면 아무것도 안 하고 끝(매 프레임 색을 다시 계산하지 않기 위한 최적화).
    /// 5. 다르면 이전 강조부터 지우고(RestoreHighlight), 새로 마우스 위에 있는 유닛이 있다면 그 유닛에
    ///    새 강조를 건다(ApplyHighlight) — 즉 "이전 걸 지우고, 있으면 새 걸 건다"의 교체 패턴.
    /// </summary>
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

    /// <summary>
    /// pointerPosition에서 Ray를 쏴서 맞은 Collider들을 카메라와 가까운 순서로 정렬한 뒤, 가장 가까운
    /// Player 또는 활성 Enemy를 반환한다("색상 돌리는 거"가 아니라 "어떤 유닛을 찾았는지 + 그 유닛에
    /// 쓸 강조색을 같이 돌려주는" 함수 — Player를 찾으면 playerHoverColor, Enemy를 찾으면 enemyHoverColor를
    /// out 파라미터로 함께 반환한다). 아무것도 못 찾으면 null과 Color.clear를 반환한다.
    /// </summary>
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

    /// <summary>
    /// unit의 모든 Renderer 중 강조 대상(ShouldHighlight)에 대해 "적용"(강조색 입히기)을 실제로 수행한다.
    /// Renderer 하나당 머티리얼 슬롯마다: 1) 현재 MaterialPropertyBlock을 그대로 저장(원본 스냅샷),
    /// 2) 셰이더의 `_BaseColor`(URP/HDRP 계열) 또는 `_Color`(빌트인 계열) 프로퍼티가 있으면 그 원본 색과
    /// hoverColor를 colorBlend 비율로 섞은 값을 새 PropertyBlock에 설정해서 적용한다. 두 프로퍼티 중
    /// 실제로 존재하는 것만 바뀌므로 셰이더 종류에 상관없이 동작한다. 아무 프로퍼티도 없어서 실제로
    /// 바뀐 게 없으면(changed == false) 저장했던 스냅샷을 바로 되돌리고 강조 목록에 넣지 않는다
    /// (나중에 RestoreHighlight가 불필요하게 이 Renderer까지 건드리지 않도록 하기 위함).
    /// </summary>
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

    /// <summary>
    /// 이 Renderer가 마우스 오버 강조 대상인지 판정한다. LineRenderer/ParticleSystemRenderer(이펙트·연결선류)와
    /// BattleHealthBarView의 자식 Renderer(체력바 UI)는 캐릭터 색 강조와 무관하므로 제외한다.
    /// </summary>
    private static bool ShouldHighlight(Renderer renderer)
    {
        if (renderer == null || renderer is LineRenderer || renderer is ParticleSystemRenderer)
        {
            return false;
        }

        return renderer.GetComponentInParent<BattleHealthBarView>() == null;
    }

    /// <summary>
    /// 현재 강조 중인 모든 Renderer(highlightedRenderers)를 ApplyHighlight 이전 원본 색으로 되돌리고
    /// 강조 상태를 완전히 비운다. Update()가 카메라 무효/모달 오픈/호버 대상 변경 때마다 부르고,
    /// 컴포넌트가 비활성화되거나(OnDisable) 파괴될 때(OnDestroy)도 강조가 남지 않도록 호출한다.
    /// </summary>
    private void RestoreHighlight()
    {
        foreach (RendererState state in highlightedRenderers)
        {
            RestoreRendererState(state);
        }

        highlightedRenderers.Clear();
        highlightedUnit = null;
    }

    /// <summary>
    /// 실제로 "색을 원래대로 되돌리는" 본체 코드. state에 저장해둔 원본 MaterialPropertyBlock을
    /// 머티리얼 슬롯별로 그대로 다시 SetPropertyBlock해서, ApplyHighlight가 계산해서 덮어썼던
    /// 강조색을 지우고 원본 렌더링 상태로 완전히 되돌린다.
    /// </summary>
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
