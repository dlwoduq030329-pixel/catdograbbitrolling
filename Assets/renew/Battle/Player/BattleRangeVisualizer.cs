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
    // 강조 전 Renderer의 진짜 원본 색상을 저장하는 캐시. SetTileColor()가 여기 값을 기준으로
    // Color.Lerp를 하고, RestoreTileColor()/RestoreAllTileColors()가 여기 값으로 되돌린다.
    // CacheOriginalColors()가 먼저 호출되지 않은 Renderer는 여기 없어서, 그 경우 SetTileColor()가
    // "그 순간의 현재 색"을 원본인 것처럼 대신 쓴다(이미 강조된 상태였다면 그 강조색이 새 원본으로 오염될 수 있음).
    private readonly Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

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
                if (renderer != null &&
                    !originalColors.ContainsKey(renderer) &&
                    renderer.material.HasProperty("_Color"))
                {
                    originalColors[renderer] = renderer.material.color;
                }
            }
        }
    }

    /// <summary>이동·공격·카드 범위용 혼합 강도로 지정 타일을 표시한다.</summary>
    public void ShowRangeTile(MapInfo tile, Color color)
    {
        SetTileColor(tile, color, rangeBlendStrength);
    }

    /// <summary>확정 전 선택 타일을 범위 색상보다 높은 선택 강조 강도로 표시한다.</summary>
    public void ShowSelectedTile(MapInfo tile, Color color)
    {
        SetTileColor(tile, color, selectedBlendStrength);
    }

    /// <summary>이동 완료 타일을 착지 전용 강조 강도로 표시한다.</summary>
    public void ShowLandedTile(MapInfo tile, Color color)
    {
        SetTileColor(tile, color, landedBlendStrength);
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
            RestoreTileColor(landedTile);
        }

        landedTile = tile;
        ShowLandedTile(tile, color);
        landedHighlightRoutine = StartCoroutine(RestoreLandedTileAfterDelay(Mathf.Max(0f, duration)));
    }

    /// <summary>원본 색상을 보존한 채 지정 비율로 강조 색상을 혼합해 적용한다.</summary>
    public void SetTileColor(MapInfo tile, Color color, float blendStrength)
    {
        if (tile == null)
        {
            return;
        }

        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.material.HasProperty("_Color"))
            {
                Color baseColor = originalColors.TryGetValue(renderer, out Color originalColor)
                    ? originalColor
                    : renderer.material.color;
                renderer.material.color = Color.Lerp(baseColor, color, Mathf.Clamp01(blendStrength));
            }
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
            if (renderer != null && originalColors.TryGetValue(renderer, out Color color))
            {
                renderer.material.color = color;
            }
        }
    }

    /// <summary>이 모듈이 변경한 모든 타일을 원본 색상으로 되돌리고 임시 강조를 종료한다.</summary>
    public void RestoreAllTileColors()
    {
        foreach (KeyValuePair<Renderer, Color> entry in originalColors)
        {
            if (entry.Key != null && entry.Key.material.HasProperty("_Color"))
            {
                entry.Key.material.color = entry.Value;
            }
        }
    }

    private IEnumerator RestoreLandedTileAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (landedTile != null)
        {
            RestoreTileColor(landedTile);
        }

        landedTile = null;
        landedHighlightRoutine = null;
    }
}
