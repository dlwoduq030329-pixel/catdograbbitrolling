using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>카드의 대상 사거리, 효과 범위와 밀치기 미리보기를 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleCardTargetController : MonoBehaviour
{
    private readonly HashSet<MapInfo> targetTiles = new HashSet<MapInfo>();
    private GameObject player;
    private BattleRangeVisualizer rangeView;
    private BattlePushPreviewView pushView;
    private Color targetColor;
    private Color effectColor;
    private Func<Vector3, MapInfo> findTile;
    private IReadOnlyList<MapInfo> allTiles;

    public Func<Vector3, MapInfo> FindTile => findTile;
    public BattleRangeVisualizer RangeView => rangeView;
    public Color EffectColor => effectColor;

    public void Setup(
        GameObject targetPlayer,
        BattleRangeVisualizer targetRangeView,
        Color cardTargetColor,
        Color cardEffectColor,
        Func<Vector3, MapInfo> tileFinder,
        IReadOnlyList<MapInfo> mapTiles,
        BattlePushPreviewView targetPushView)
    {
        player = targetPlayer;
        rangeView = targetRangeView;
        targetColor = cardTargetColor;
        effectColor = cardEffectColor;
        findTile = tileFinder;
        allTiles = mapTiles;
        pushView = targetPushView;
    }

    public bool IsInTargetRange(MapInfo tile)
    {
        return tile != null && targetTiles.Contains(tile);
    }

    public bool IsTargetValid(GameObject target, MapInfo tile)
    {
        return target != null && target.activeInHierarchy && IsInTargetRange(tile);
    }

    /// <summary>Player 위치를 기준으로 카드의 대상 선택 범위를 표시합니다.</summary>
    public bool ShowTargetRange(SelectedCardUseInfo card)
    {
        rangeView?.ClearCardRangeTiles();
        targetTiles.Clear();

        MapInfo playerTile = findTile != null && player != null
            ? findTile(player.transform.position)
            : null;
        if (playerTile == null || card == null)
        {
            Debug.LogError("카드 사거리 계산에 필요한 Player 타일이나 카드가 없습니다.", this);
            return false;
        }

        targetTiles.UnionWith(BattleTileRangeCalculator.FindCardTargetTiles(
            playerTile,
            card.ActionInfo.RangeTiles));
        rangeView?.ShowCardRangeTiles(targetTiles, targetColor);
        return true;
    }

    /// <summary>선택한 타일을 기준으로 카드가 실제 영향을 주는 범위를 표시합니다.</summary>
    public bool ShowEffectRange(SelectedCardUseInfo card, MapInfo centerTile)
    {
        if (card == null || card.CardData == null || centerTile == null ||
            card.CardData.areaType == BattleCardAreaType.Single)
            return false;

        MapInfo playerTile = findTile != null && player != null
            ? findTile(player.transform.position)
            : null;
        bool isPersistentArea = BattleCardEffectDataQuery.ContainsEffect(
            card.CardData,
            BattleCardEffectType.CreateArea);
        HashSet<MapInfo> effectTiles = BattleTileRangeCalculator.FindCardEffectAreaTiles(
            centerTile,
            playerTile,
            card.CardData.areaType,
            card.CardData.areaSizeTiles,
            isPersistentArea,
            isPersistentArea ? allTiles : null);

        rangeView?.ShowCardRangeTiles(effectTiles, effectColor);
        return effectTiles.Count > 0;
    }

    /// <summary>현재 카드의 밀치기 결과를 미리 표시합니다.</summary>
    public void ShowPushPreview(SelectedCardUseInfo card, GameObject target)
    {
        if (pushView == null || card == null || target == null)
        {
            pushView?.HideAllPushPreviews();
            return;
        }

        List<BattleCardMovementService.PushPlan> plans =
            BattleCardPushPreviewPlanner.BuildPushPlans(player, target, card.CardData, findTile);
        pushView.ShowPushPredictions(plans);
    }

    public void Clear()
    {
        pushView?.HideAllPushPreviews();
        targetTiles.Clear();
        rangeView?.ClearCardRangeTiles();
    }
}
