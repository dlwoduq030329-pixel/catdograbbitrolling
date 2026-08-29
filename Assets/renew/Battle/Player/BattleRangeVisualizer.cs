using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 범위와 선택 결과를 MapInfo 타일의 색상으로 표시하고 원본 색상으로 복구한다.
/// 범위 계산과 입력 상태는 처리하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleRangeVisualizer : MonoBehaviour
{
    private enum TileColorLayer
    {
        // 아래 숫자 순서가 그대로 색상 합성 순서다.
        // 뒤에 있는 레이어일수록 앞에서 계산된 색 위에 다시 섞이므로 화면에서 더 강하게 보인다.
        GeneralRange = 0,
        CardRange = 1,
        Selected = 2,
        Landed = 3
    }

    private readonly struct TileColorOverlay
    {
        // Color는 덧씌울 강조색이고, BlendStrength는 원본/이전 결과와 이 색을 섞는 비율이다.
        // 0이면 이전 색을 그대로 유지하고, 1이면 이 강조색으로 완전히 바뀐다.
        public readonly Color Color;
        public readonly float BlendStrength;

        public TileColorOverlay(Color color, float blendStrength)
        {
            Color = color;
            BlendStrength = blendStrength;
        }
    }

    // 강조 전 Renderer의 진짜 원본 색상을 저장하는 캐시. SetTileColor()가 여기 값을 기준으로
    // Color.Lerp를 하고, RestoreTileColor()/RestoreAllTileColors()가 여기 값으로 되돌린다.
    // CacheOriginalColors()가 먼저 호출되지 않은 Renderer는 여기 없어서, 그 경우 SetTileColor()가
    // "그 순간의 현재 색"을 원본인 것처럼 대신 쓴다(이미 강조된 상태였다면 그 강조색이 새 원본으로 오염될 수 있음).
    private readonly Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();
    // Renderer마다 현재 활성화된 강조 레이어를 보관한다. 한 시스템이 닫힐 때 Material 색을 직접
    // 과거 값으로 되감지 않고 자기 레이어만 제거한 뒤 남은 레이어를 우선순위대로 다시 합성한다.
    private readonly Dictionary<Renderer, Dictionary<TileColorLayer, TileColorOverlay>> activeColorLayers =
        new Dictionary<Renderer, Dictionary<TileColorLayer, TileColorOverlay>>();
    // UnityEngine.Object 계열 리소스인 MaterialPropertyBlock은 MonoBehaviour 생성자/필드 초기화 시
    // 만들 수 없다. 실제 타일 색상을 적용하는 시점(Awake 이후)에 한 번만 지연 생성한다.
    private MaterialPropertyBlock colorPropertyBlock;

    // 이동/공격/카드 범위 표시, 확정 전 선택 타일, 착지 타일 각각 다른 강도로 원본 색과 섞는다(0=원본 유지, 1=강조색으로 완전 교체).
    private float rangeBlendStrength = 0.3f;
    private float selectedBlendStrength = 0.65f;
    private float landedBlendStrength = 0.5f;
    private Coroutine landedHighlightRoutine;
    private MapInfo landedTile;

    /// <summary>범위·선택·착지 강조가 원본 Material 색상과 섞이는 강도(0~1)를 설정한다.</summary>
    public void SetBlendStrengths(float rangeColorBlend, float selectedColorBlend, float landedColorBlend)
    {
        rangeBlendStrength = Mathf.Clamp01(rangeColorBlend);
        selectedBlendStrength = Mathf.Clamp01(selectedColorBlend);
        landedBlendStrength = Mathf.Clamp01(landedColorBlend);
    }

    /// <summary>
    /// 강조가 시작되기 전에 각 타일과 자식 Renderer의 sharedMaterial 원본 색상을 한 번만 저장한다.
    /// 같은 Renderer를 다시 전달해도 최초 색상을 덮어쓰지 않으므로 여러 Preview가 겹쳐도 복구 기준이 유지된다.
    /// Scene 타일 생성이 끝난 직후 호출하는 것이 가장 안전하다.
    /// </summary>
    public void CacheOriginalColors(IEnumerable<MapInfo> tiles)
    {
        if (tiles == null)
        {
            return;
        }

        foreach (MapInfo tile in tiles)
        {
            if (tile == null)
            {
                continue;
            }

            foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material != null &&
                    !originalColors.ContainsKey(renderer) &&
                    TryReadMaterialColor(material, out Color originalColor))
                {
                    originalColors[renderer] = originalColor;
                }
            }
        }
    }

    /// <summary>
    /// 이동 범위·기본 공격 범위처럼 카드 이외의 공용 범위를 GeneralRange 레이어에 표시한다.
    /// 같은 타일에 카드·선택·착지 레이어가 있으면 지우지 않고 우선순위에 따라 함께 합성한다.
    /// </summary>
    public void ShowRangeTile(MapInfo tile, Color color)
    {
        SetTileColorForLayer(tile, color, rangeBlendStrength, TileColorLayer.GeneralRange);
    }

    /// <summary>
    /// 카드 선택 가능 범위와 실제 효과 범위를 카드 전용 레이어로 표시한다.
    /// 카드 표시를 닫을 때 일반 이동·위협 범위까지 지우지 않기 위해 별도 레이어를 사용한다.
    /// </summary>
    public void ShowCardRangeTile(MapInfo tile, Color color)
    {
        SetTileColorForLayer(tile, color, rangeBlendStrength, TileColorLayer.CardRange);
    }

    /// <summary>
    /// 카드 사거리 계산기가 반환한 여러 타일을 카드 전용 색상 레이어로 표시한다.
    /// 어떤 타일이 범위에 포함되는지는 판단하지 않고 전달받은 계산 결과만 화면에 반영한다.
    /// ShowCardRangeTile()이 타일 하나를 처리하는 함수라면 이 함수는 그 함수를 목록 전체에 반복 호출하는
    /// 편의 함수다. 별도의 사거리 계산이나 색상 규칙은 포함하지 않는다.
    /// </summary>
    public void ShowCardRangeTiles(IEnumerable<MapInfo> tiles, Color color)
    {
        // 계산 책임은 호출자에게 있다. 이 함수는 받은 타일을 순회해 같은 카드 레이어를 적용할 뿐이다.
        if (tiles == null) return;
        foreach (MapInfo tile in tiles)
        {
            ShowCardRangeTile(tile, color);
        }
    }

    /// <summary>
    /// 마우스로 선택했거나 행동 확정을 기다리는 타일을 Selected 레이어로 표시한다.
    /// 일반/카드 범위보다 나중에 합성되므로 선택 위치가 범위 색상에 가려지지 않는다.
    /// 타일 색상만 담당하며 경로선이나 화살표 GameObject를 생성하는 함수는 아니다.
    /// </summary>
    public void ShowSelectedTile(MapInfo tile, Color color)
    {
        SetTileColorForLayer(tile, color, selectedBlendStrength, TileColorLayer.Selected);
    }

    /// <summary>
    /// 실제 이동이 끝난 타일을 Landed 레이어로 표시한다.
    /// 가장 높은 우선순위 레이어이므로 다른 범위와 겹쳐도 착지 피드백이 선명하게 남는다.
    /// 자동 해제가 필요하면 ShowLandedTileForDuration을 사용한다.
    /// </summary>
    public void ShowLandedTile(MapInfo tile, Color color)
    {
        SetTileColorForLayer(tile, color, landedBlendStrength, TileColorLayer.Landed);
    }

    /// <summary>
    /// 착지 타일을 지정 시간 동안 강조하고 Coroutine 종료 시 Landed 레이어만 제거한다.
    /// 이전 착지 강조가 실행 중이면 Coroutine을 중단하고 이전 타일의 Landed 레이어를 먼저 지운다.
    /// 이동·카드 등 다른 레이어는 보존되므로 원본색으로 무조건 덮어쓰지 않는다.
    /// </summary>
    public void ShowLandedTileForDuration(MapInfo tile, Color color, float duration)
    {
        // 새 이동이 끝나기 전에 이전 착지 강조 시간이 남아 있으면 이전 예약 복구를 취소한다.
        // 두 Coroutine이 서로 다른 시점에 landedTile을 지우는 경쟁 상태를 막기 위한 처리다.
        if (landedHighlightRoutine != null)
        {
            StopCoroutine(landedHighlightRoutine);
        }

        // 이전과 다른 타일에 도착했다면 이전 타일의 착지 레이어만 즉시 제거한다.
        // 이동/카드/선택 레이어는 ClearTileLayer가 보존한다.
        if (landedTile != null && landedTile != tile)
        {
            ClearTileLayer(landedTile, TileColorLayer.Landed);
        }

        // 현재 도착 타일을 기록하고, 즉시 강조한 뒤 duration초 후 제거하는 Coroutine을 예약한다.
        landedTile = tile;
        ShowLandedTile(tile, color);
        landedHighlightRoutine = StartCoroutine(RestoreLandedTileAfterDelay(Mathf.Max(0f, duration)));
    }

    /// <summary>
    /// 외부 코드가 별도 레이어 구분 없이 타일 하나를 강조할 때 사용하는 공용 진입점이다.
    /// 내부에서는 GeneralRange 레이어로 등록되며, 실제 최종 색상 계산은 ApplyActiveLayers()가 담당한다.
    /// </summary>
    public void SetTileColor(MapInfo tile, Color color, float blendStrength)
    {
        SetTileColorForLayer(tile, color, blendStrength, TileColorLayer.GeneralRange);
    }

    /// <summary>
    /// 타일과 모든 자식 Renderer에 지정 색상 레이어를 등록한 뒤 최종 표시색을 다시 계산한다.
    /// Renderer별 최초 원본색을 보관하고 같은 종류의 레이어가 다시 들어오면 그 레이어 값만 교체한다.
    /// sharedMaterial은 읽기만 하고 실제 표시는 MaterialPropertyBlock으로 적용해 Material 복제와 공용색 변경을 막는다.
    /// </summary>
    private void SetTileColorForLayer(
        MapInfo tile,
        Color color,
        float blendStrength,
        TileColorLayer layer)
    {
        if (tile == null)
        {
            return;
        }

        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
        {
            // MapInfo 자체뿐 아니라 자식 Mesh까지 한 타일의 외형으로 취급한다.
            // 기존 Standard 계열의 _Color와 Yeop 타일 Shader의 _BaseColor 중 하나도 없으면 건너뛴다.
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material == null || !TryReadMaterialColor(material, out Color materialColor))
            {
                continue;
            }

            // 사전 Cache가 누락된 런타임 타일도 표시할 수 있게 현재 sharedMaterial 색을 최초 원본으로 등록한다.
            if (!originalColors.ContainsKey(renderer))
            {
                originalColors[renderer] = materialColor;
            }

            if (!activeColorLayers.TryGetValue(
                    renderer,
                    out Dictionary<TileColorLayer, TileColorOverlay> rendererLayers))
            {
                // 이 Renderer가 처음 강조될 때만 레이어 보관용 내부 Dictionary를 만든다.
                rendererLayers = new Dictionary<TileColorLayer, TileColorOverlay>();
                activeColorLayers.Add(renderer, rendererLayers);
            }

            // Dictionary indexer는 같은 레이어의 이전 요청을 최신 색과 강도로 교체한다.
            rendererLayers[layer] = new TileColorOverlay(color, Mathf.Clamp01(blendStrength));
            ApplyActiveLayers(renderer);
        }
    }

    /// <summary>
    /// 한 타일에 등록된 모든 강조 레이어를 제거하고 캐시된 원본 색상으로 복구한다.
    /// 특정 레이어만 지우는 함수가 아니므로 이동·카드·선택·착지 표시가 모두 사라진다.
    /// 카드 범위만 지우려면 ClearCardRangeTiles()를 사용해야 한다.
    /// </summary>
    public void RestoreTileColor(MapInfo tile)
    {
        if (tile == null)
        {
            return;
        }

        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
            {
                // 이 Renderer에 겹쳐 있던 모든 표시 목적을 제거한다.
                // 레이어가 사라진 상태에서 다시 계산하면 ApplyActiveLayers가 원본색을 적용한다.
                activeColorLayers.Remove(renderer);
                ApplyActiveLayers(renderer);
            }
        }
    }

    /// <summary>
    /// 카드가 등록한 색상 레이어만 제거한다. 같은 타일에 이동·위협·선택 레이어가 남아 있으면
    /// 원본색으로 바로 복구하지 않고 남은 레이어를 다시 합성한다.
    /// </summary>
    public void ClearCardRangeTiles()
    {
        ClearLayerFromAllRenderers(TileColorLayer.CardRange);
    }

    /// <summary>
    /// 이 모듈에 등록된 모든 색상 레이어를 한꺼번에 제거하고 캐시된 원본색을 다시 적용한다.
    /// 현재 실행 중인 착지 Coroutine 자체는 중단하지 않지만, 레이어 목록을 먼저 비우므로 화면색은 즉시 복구된다.
    /// </summary>
    public void RestoreAllTileColors()
    {
        activeColorLayers.Clear();
        foreach (Renderer renderer in originalColors.Keys)
        {
            ApplyActiveLayers(renderer);
        }
    }

    private IEnumerator RestoreLandedTileAfterDelay(float duration)
    {
        // 이동 완료 피드백을 플레이어가 인지할 수 있는 시간만큼 유지한다.
        // WaitForSeconds는 게임 시간 배율의 영향을 받으므로 Time.timeScale이 0이면 복구도 대기한다.
        yield return new WaitForSeconds(duration);

        if (landedTile != null)
        {
            // 착지 표시만 제거한다. 같은 타일의 이동/카드 범위는 남긴 뒤 최종 색을 다시 계산한다.
            ClearTileLayer(landedTile, TileColorLayer.Landed);
        }

        // 현재 착지 표시가 끝났음을 기록해 다음 이동이 새 Coroutine을 정상적으로 시작하게 한다.
        landedTile = null;
        landedHighlightRoutine = null;
    }

    /// <summary>
    /// 지정 타일에서 한 종류의 색상 레이어만 제거한다.
    /// 다른 레이어가 남아 있으면 원본색으로 바로 돌아가지 않고 남은 색들을 다시 합성한다.
    /// </summary>
    private void ClearTileLayer(MapInfo tile, TileColorLayer layer)
    {
        if (tile == null) return;
        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !activeColorLayers.TryGetValue(renderer, out var layers)) continue;
            // 제거 후 아무 레이어도 남지 않은 Renderer는 바깥 Dictionary에서도 정리한다.
            layers.Remove(layer);
            if (layers.Count == 0) activeColorLayers.Remove(renderer);
            // 남은 레이어 또는 원본색을 즉시 화면에 반영한다.
            ApplyActiveLayers(renderer);
        }
    }

    /// <summary>
    /// 현재 관리 중인 모든 Renderer에서 지정 종류의 레이어만 제거한다.
    /// ClearCardRangeTiles()처럼 특정 표시 시스템을 전체 종료할 때 사용한다.
    /// 순회 중 Dictionary를 수정해야 하므로 Key 목록을 별도 List로 복사한 뒤 처리한다.
    /// </summary>
    private void ClearLayerFromAllRenderers(TileColorLayer layer)
    {
        // activeColorLayers.Keys를 직접 순회하며 항목을 삭제하면 컬렉션 변경 예외가 발생한다.
        List<Renderer> renderers = new List<Renderer>(activeColorLayers.Keys);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !activeColorLayers.TryGetValue(renderer, out var layers)) continue;
            if (!layers.Remove(layer)) continue;
            if (layers.Count == 0) activeColorLayers.Remove(renderer);
            ApplyActiveLayers(renderer);
        }
    }

    /// <summary>
    /// 저장된 원본색에서 시작해 일반 범위→카드 범위→선택→착지 순으로 색을 혼합한다.
    /// MaterialPropertyBlock을 사용하므로 renderer.material 접근으로 Material 복사본을 만들지 않는다.
    /// SetTileColorForLayer()가 "어떤 레이어가 활성 상태인지 저장"하는 함수라면,
    /// 이 함수는 저장된 레이어 전체를 읽어 "지금 화면에 보여줄 최종 색 하나를 계산하고 적용"한다.
    /// </summary>
    private void ApplyActiveLayers(Renderer renderer)
    {
        if (renderer == null || !originalColors.TryGetValue(renderer, out Color finalColor)) return;

        if (activeColorLayers.TryGetValue(renderer, out var layers))
        {
            // enum 숫자 순서가 곧 합성 우선순위다. 일반 범위 위에 카드, 선택, 착지색을 차례대로 섞는다.
            for (TileColorLayer layer = TileColorLayer.GeneralRange;
                 layer <= TileColorLayer.Landed;
                 layer++)
            {
                if (layers.TryGetValue(layer, out TileColorOverlay overlay))
                    finalColor = Color.Lerp(finalColor, overlay.Color, overlay.BlendStrength);
            }
        }

        // Renderer에 이미 설정된 다른 PropertyBlock 값은 보존하고 현재 Shader가 지원하는 색상 항목만 교체한다.
        // sharedMaterial을 변경하지 않으므로 같은 Material을 쓰는 다른 타일까지 함께 변하지 않는다.
        if (colorPropertyBlock == null)
        {
            colorPropertyBlock = new MaterialPropertyBlock();
        }

        renderer.GetPropertyBlock(colorPropertyBlock);
        Material material = renderer.sharedMaterial;
        // Yeop 타일용 Shader는 실제 표면색에 _BaseColor를 사용한다. 일부 Material은 호환을 위해
        // _Color도 함께 갖고 있으므로 존재하는 두 속성을 모두 갱신해 Shader 종류에 관계없이 범위가 보이게 한다.
        if (material != null && material.HasProperty("_BaseColor"))
            colorPropertyBlock.SetColor("_BaseColor", finalColor);
        if (material != null && material.HasProperty("_Color"))
            colorPropertyBlock.SetColor("_Color", finalColor);
        renderer.SetPropertyBlock(colorPropertyBlock);
        colorPropertyBlock.Clear();
    }

    /// <summary>
    /// Yeop 타일 Shader의 _BaseColor를 우선 읽고, 기존 Standard 타일은 _Color를 읽는다.
    /// 반환값이 false면 이 Renderer는 색상 PropertyBlock 방식의 범위 표시에 사용할 수 없다.
    /// </summary>
    private static bool TryReadMaterialColor(Material material, out Color color)
    {
        color = Color.white;
        if (material == null)
        {
            return false;
        }

        if (material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            return true;
        }

        if (material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
            return true;
        }

        return false;
    }
}
