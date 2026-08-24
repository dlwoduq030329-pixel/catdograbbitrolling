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
        GeneralRange = 0,
        CardRange = 1,
        Selected = 2,
        Landed = 3
    }

    private readonly struct TileColorOverlay
    {
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
    private readonly MaterialPropertyBlock colorPropertyBlock = new MaterialPropertyBlock();

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

    /// <summary>강조 전에 각 타일 Renderer의 원본 색상을 저장하여 이후 정확히 복구할 수 있게 한다.</summary>
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
                    material.HasProperty("_Color"))
                {
                    originalColors[renderer] = material.GetColor("_Color");
                }
            }
        }
    }

    /// <summary>이동·공격·카드 범위용 혼합 강도로 지정 타일을 표시한다.</summary>
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

    /// <summary>확정 전 선택 타일을 범위 색상보다 높은 선택 강조 강도로 표시한다.</summary>
    public void ShowSelectedTile(MapInfo tile, Color color)
    {
        SetTileColorForLayer(tile, color, selectedBlendStrength, TileColorLayer.Selected);
    }

    /// <summary>이동 완료 타일을 착지 전용 강조 강도로 표시한다.</summary>
    public void ShowLandedTile(MapInfo tile, Color color)
    {
        SetTileColorForLayer(tile, color, landedBlendStrength, TileColorLayer.Landed);
    }

    /// <summary>착지 타일을 지정 시간 동안 강조한 뒤 저장된 원본 색상으로 자동 복구한다.</summary>
    public void ShowLandedTileForDuration(MapInfo tile, Color color, float duration)
    {
        if (landedHighlightRoutine != null)
        {
            StopCoroutine(landedHighlightRoutine);
        }

        if (landedTile != null && landedTile != tile)
        {
            ClearTileLayer(landedTile, TileColorLayer.Landed);
        }

        landedTile = tile;
        ShowLandedTile(tile, color);
        landedHighlightRoutine = StartCoroutine(RestoreLandedTileAfterDelay(Mathf.Max(0f, duration)));
    }

    /// <summary>원본 색상을 보존한 채 지정 비율로 강조 색상을 혼합해 적용한다.</summary>
    public void SetTileColor(MapInfo tile, Color color, float blendStrength)
    {
        SetTileColorForLayer(tile, color, blendStrength, TileColorLayer.GeneralRange);
    }

    /// <summary>지정 타일 Renderer에 한 색상 레이어를 등록하고 모든 활성 레이어를 다시 합성한다.</summary>
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
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material == null || !material.HasProperty("_Color"))
            {
                continue;
            }

            if (!originalColors.ContainsKey(renderer))
            {
                originalColors[renderer] = material.GetColor("_Color");
            }

            if (!activeColorLayers.TryGetValue(
                    renderer,
                    out Dictionary<TileColorLayer, TileColorOverlay> rendererLayers))
            {
                rendererLayers = new Dictionary<TileColorLayer, TileColorOverlay>();
                activeColorLayers.Add(renderer, rendererLayers);
            }

            rendererLayers[layer] = new TileColorOverlay(color, Mathf.Clamp01(blendStrength));
            ApplyActiveLayers(renderer);
        }
    }

    /// <summary>한 타일의 Renderer 색상을 캐시된 원본 값으로 복구한다.</summary>
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

    /// <summary>이 모듈이 변경한 모든 타일을 원본 색상으로 되돌리고 임시 강조를 종료한다.</summary>
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
        yield return new WaitForSeconds(duration);

        if (landedTile != null)
        {
            ClearTileLayer(landedTile, TileColorLayer.Landed);
        }

        landedTile = null;
        landedHighlightRoutine = null;
    }

    private void ClearTileLayer(MapInfo tile, TileColorLayer layer)
    {
        if (tile == null) return;
        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !activeColorLayers.TryGetValue(renderer, out var layers)) continue;
            layers.Remove(layer);
            if (layers.Count == 0) activeColorLayers.Remove(renderer);
            ApplyActiveLayers(renderer);
        }
    }

    private void ClearLayerFromAllRenderers(TileColorLayer layer)
    {
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
    /// </summary>
    private void ApplyActiveLayers(Renderer renderer)
    {
        if (renderer == null || !originalColors.TryGetValue(renderer, out Color finalColor)) return;

        if (activeColorLayers.TryGetValue(renderer, out var layers))
        {
            for (TileColorLayer layer = TileColorLayer.GeneralRange;
                 layer <= TileColorLayer.Landed;
                 layer++)
            {
                if (layers.TryGetValue(layer, out TileColorOverlay overlay))
                    finalColor = Color.Lerp(finalColor, overlay.Color, overlay.BlendStrength);
            }
        }

        renderer.GetPropertyBlock(colorPropertyBlock);
        colorPropertyBlock.SetColor("_Color", finalColor);
        renderer.SetPropertyBlock(colorPropertyBlock);
        colorPropertyBlock.Clear();
    }
}
