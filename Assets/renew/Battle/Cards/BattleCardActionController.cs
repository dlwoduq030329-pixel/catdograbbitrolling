using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 사용 시작, 사거리 표시, 대상 선택, Confirm/Quit, MP와 손패 확정을 관리한다.
/// 원시 입력과 공용 확인 UI는 Coordinator에 이벤트로 요청한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCardActionController : MonoBehaviour
{
    private readonly HashSet<MapInfo> rangeTiles = new HashSet<MapInfo>();
    private GameObject player;
    private BattleUnitMP playerMP;
    private BattleStatusEffects playerStatusEffects;
    private BattleRangeVisualizer rangeVisualizer;
    private Color rangeColor;
    private Color effectAreaColor;
    private Func<Vector3, MapInfo> findClosestTile;
    private Camera battleCamera;
    private BattlePushPreviewView pushPreviewView;
    private SelectedCardUseInfo selectedCardInfo;
    private BattleCardDrawSystem drawSystem;
    private GameObject selectedTarget;
    private MapInfo selectedTargetTile;

    public bool IsSelectingTarget { get; private set; }
    public bool IsAwaitingConfirmation { get; private set; }
    /// <summary>카드 대상 선택 또는 사용 확인이 진행 중인지 반환한다.</summary>
    public bool IsActionActive => IsSelectingTarget || IsAwaitingConfirmation;
    public BattleCardTargetType TargetType => selectedCardInfo != null
        ? selectedCardInfo.CardData.targetType
        : BattleCardTargetType.None;

    public event Action<string> TargetSelectionRequested;
    public event Action<string> ConfirmationRequested;
    public event Action<BattleActionResult> Confirmed;
    public event Action Cancelled;
    public event Action<bool> RangeVisibilityChanged;

    /// <summary>카드 대상 판정에 필요한 Player, 맵, 범위 표시와 완료 이벤트를 연결한다.</summary>
    public void Configure(
        GameObject targetPlayer,
        BattleRangeVisualizer visualizer,
        Color cardRangeColor,
        Color cardEffectAreaColor,
        Func<Vector3, MapInfo> tileFinder,
        Camera camera,
        BattlePushPreviewView previewView)
    {
        player = targetPlayer;
        // 카드 선택·확정마다 GetComponent를 반복하지 않도록 Player 등록 시 한 번 보관한다.
        // MP 수치는 BattleUnitMP 인스턴스 내부에서 바뀌므로 같은 참조의 CurrentMP를 읽으면 항상 최신 값이다.
        playerMP = targetPlayer != null ? targetPlayer.GetComponent<BattleUnitMP>() : null;
        // 동상으로 카드 MP 비용을 보정할 때마다 GetComponent하지 않도록 Player 등록 시 함께 보관한다.
        playerStatusEffects = targetPlayer != null ? targetPlayer.GetComponent<BattleStatusEffects>() : null;
        rangeVisualizer = visualizer;
        rangeColor = cardRangeColor;
        effectAreaColor = cardEffectAreaColor;
        findClosestTile = tileFinder;
        battleCamera = camera;
        pushPreviewView = previewView;
        pushPreviewView?.ConfigurePreviewDependencies(battleCamera);
    }

    /// <summary>손패 카드 요청을 검증하고 대상 선택 및 확인 대기 상태를 시작한다. 실제 효과는 아직 적용하지 않는다.</summary>
    public bool TryStartCardUse(SelectedCardUseInfo cardUse, BattleCardDrawSystem cardDrawSystem, bool canUseCards)
    {
        if (cardUse == null || cardUse.CardData == null || cardDrawSystem == null || player == null ||
            IsSelectingTarget || IsAwaitingConfirmation || !canUseCards)
        {
            return false;
        }

        int cardCost = GetModifiedCardCost(cardUse.ActionInfo.MPCost, cardUse.CardData);
        if (playerMP == null || !playerMP.CanSpend(cardCost))
        {
            Debug.Log($"카드 사용 불가: 행동력이 {cardCost} 필요합니다.", this);
            return false;
        }

        selectedCardInfo = cardUse;
        drawSystem = cardDrawSystem;
        if (BattleCardTargetSelectionRules.UsesLowestHealthEnemyAutoTarget(selectedCardInfo.CardData))
        {
            if (!TrySelectLowestHealthEnemyInRange(out selectedTarget, out selectedTargetTile))
            {
                OpenTargetSelection();
                return true;
            }

            OpenConfirmation();
            return true;
        }

        BattleCardTargetType targetType = selectedCardInfo.CardData.targetType;
        if (targetType == BattleCardTargetType.None ||
            targetType == BattleCardTargetType.Self ||
            targetType == BattleCardTargetType.Ally ||
            targetType == BattleCardTargetType.AllEnemies)
        {
            selectedTarget = player;
            selectedTargetTile = findClosestTile(player.transform.position);
            ShowEffectAreaPreview(selectedTargetTile);
            OpenConfirmation();
        }
        else
        {
            OpenTargetSelection();
        }

        return true;
    }

    /// <summary>현재 카드의 대상 유형과 사거리 규칙에 맞는 대상인지 확인하고 유효한 선택만 보관한다.</summary>
    public bool TryStoreTargetAndOpenConfirmation(GameObject target, MapInfo targetTile)
    {
        if (!IsSelectingTarget || target == null || targetTile == null || !rangeTiles.Contains(targetTile))
        {
            return false;
        }

        selectedTarget = target;
        selectedTargetTile = targetTile;
        IsSelectingTarget = false;
        ShowEffectAreaPreview(selectedTargetTile);
        OpenConfirmation();
        return true;
    }

    /// <summary>
    /// 확인 입력을 받은 카드의 최종 사용 절차를 순서대로 실행한다.
    /// 1. 확인 대기 상태와 저장된 대상을 다시 검사한다.
    /// 2. 상태이상이 반영된 최종 MP 비용을 계산하고 카드 효과가 실행 가능한지 사전 검증한다.
    /// 3. MP와 손패 카드를 함께 소비한 뒤 준비된 효과를 실행한다.
    /// 4. 카드 선택 상태와 사거리 표시를 닫고 완료 이벤트를 호출한다.
    /// 어느 검증에서든 실패하면 효과는 실행하지 않으며, 손패 소비가 실패하면 먼저 차감한 MP도 복구한다.
    /// </summary>
    public bool TryConfirmCardUse()
    {
        if (!CanAttemptConfirmation())
        {
            return false;
        }

        bool targetWasSelectedAutomatically =
            BattleCardTargetSelectionRules.UsesLowestHealthEnemyAutoTarget(selectedCardInfo.CardData);
        if (!CanUseCurrentlySelectedTarget(targetWasSelectedAutomatically))
        {
            OpenTargetSelection();
            return false;
        }

        // 카드 선택 이후 동상 같은 상태가 변했을 수 있으므로 시작 때의 비용을 재사용하지 않고 다시 계산한다.
        if (!TryGetCurrentCardCost(out int cardCost))
        {
            Debug.LogWarning($"카드 사용 불가: MP {cardCost}이 필요합니다.", this);
            return false;
        }

        // 효과 파이프라인에는 Player, 대상, 타일, 카드 데이터와 외부 실행 함수를 하나의 문맥으로 전달한다.
        BattleCardEffectPipeline.Context effectContext = BuildEffectContext();
        if (!BattleCardEffectPipeline.TryPrepareCardEffects(
                effectContext,
                out BattleCardEffectPipeline.PreparedUse preparedEffects,
                out string failureReason))
        {
            Debug.LogWarning($"카드 사용 불가: {failureReason}", this);
            if (RequiresExternalTarget() && !targetWasSelectedAutomatically)
                OpenTargetSelection();
            return false;
        }

        // 효과 실행 전 자원 소비를 확정한다. 이 단계가 실패하면 실제 피해·회복·이동은 발생하지 않는다.
        if (!TrySpendMpAndMoveCardOutOfHand(cardCost))
        {
            return false;
        }

        GameObject resultTarget = selectedTarget;
        BattleActionRequest resultRequest = selectedCardInfo.ActionInfo;
        // 사전 검증된 효과만 실행하고, 이후 카드 행동이 끝났음을 상위 입력 흐름에 알린다.
        BattleCardEffectPipeline.ApplyPreparedCardEffects(effectContext, preparedEffects);
        BattleActionResult result = new BattleActionResult(
            resultRequest, player, resultTarget, Array.Empty<MapInfo>(), 0, cardCost);
        ClearStateAndRange();
        Confirmed?.Invoke(result);
        return true;
    }

    /// <summary>확정 단계 진입에 필요한 카드, 드로우 시스템과 Player 참조가 남아 있는지 확인한다.</summary>
    private bool CanAttemptConfirmation()
    {
        return IsAwaitingConfirmation && selectedCardInfo != null && drawSystem != null && player != null;
    }

    /// <summary>확정 직전에도 선택 대상이 살아 있고 기존 사거리 안에 있는지 다시 확인한다.</summary>
    private bool CanUseCurrentlySelectedTarget(bool targetWasSelectedAutomatically)
    {
        if (!RequiresExternalTarget() || targetWasSelectedAutomatically)
        {
            return true;
        }

        return selectedTarget != null && selectedTargetTile != null &&
               selectedTarget.activeInHierarchy && rangeTiles.Contains(selectedTargetTile);
    }

    /// <summary>
    /// 현재 적용 중인 동상(Frostbite)의 공격 비용 +1을 반영해 최종 카드 MP를 다시 계산한다.
    /// 카드 선택 후 확정 전까지 상태가 변경될 수 있으므로 확정 시점에 캐시된 Player MP로 다시 검사한다.
    /// </summary>
    private bool TryGetCurrentCardCost(out int cardCost)
    {
        cardCost = selectedCardInfo != null
            ? GetModifiedCardCost(selectedCardInfo.ActionInfo.MPCost, selectedCardInfo.CardData)
            : 0;
        return playerMP != null && playerMP.CanSpend(cardCost);
    }

    /// <summary>선택된 카드와 대상을 카드 효과 파이프라인이 읽을 실행 문맥으로 묶는다.</summary>
    private BattleCardEffectPipeline.Context BuildEffectContext()
    {
        return new BattleCardEffectPipeline.Context
        {
            Player = player,
            SelectedTarget = selectedTarget,
            SelectedTile = selectedTargetTile,
            Card = selectedCardInfo.CardData,
            CardIndex = selectedCardInfo.CardIndex,
            ActionInfo = selectedCardInfo.ActionInfo,
            FindNearestTileAtPosition = findClosestTile,
            ApplyStatusToTarget = ApplyStatusToUnit,
            PersistentAreaVisualizer = rangeVisualizer,
            PersistentAreaTileColor = effectAreaColor,
            CardDrawSystem = drawSystem,
            UsedCardInfo = selectedCardInfo
        };
    }

    /// <summary>
    /// 모든 카드가 공통으로 거치는 소비 단계다. MP를 차감하고 선택한 손패 카드를 사용 더미로 옮긴다.
    /// 버섯 카드만의 효과가 아니며, 이 처리가 없으면 일반 카드도 사용 후 손패에 그대로 남는다.
    /// MP 차감 뒤 손패 상태가 달라져 카드 이동이 실패하면 차감 전 MP를 즉시 복구한다.
    /// </summary>
    private bool TrySpendMpAndMoveCardOutOfHand(int cardCost)
    {
        int mpBeforeCardUse = playerMP.CurrentMP;
        if (!playerMP.TrySpend(cardCost))
        {
            Debug.LogWarning($"카드 사용 확정 중 MP {cardCost} 차감에 실패했습니다.", this);
            return false;
        }

        if (!drawSystem.TryMoveUsedCardToDiscardPile(selectedCardInfo))
        {
            playerMP.SetCurrentMP(mpBeforeCardUse);
            Debug.LogError("손패 상태가 변경되어 카드 소비에 실패했습니다. 차감된 MP를 복구했습니다.", this);
            CancelAll();
            return false;
        }

        return true;
    }

    /// <summary>카드를 소비하지 않고 선택 대상, 사거리 표시와 확인 대기 상태를 제거한다.</summary>
    public void Cancel()
    {
        CancelAll();
    }

    /// <summary>턴 전환 시 완료되지 않은 카드 행동과 표시 상태를 안전하게 초기화한다.</summary>
    public void ResetTurn()
    {
        ClearStateAndRange();
    }

    /// <summary>
    /// 외부 대상을 직접 선택해야 하는 카드의 대상 선택 단계를 연다.
    /// 최초 카드 선택 또는 확정 실패 후 재선택에 공통으로 사용하며, 이전 Push 미리보기와 저장 대상을
    /// 지운 뒤 카드 사거리를 다시 표시한다.
    /// </summary>
    private void OpenTargetSelection()
    {
        pushPreviewView?.HideAllPushPreviews();
        IsAwaitingConfirmation = false;
        IsSelectingTarget = true;
        selectedTarget = null;
        selectedTargetTile = null;
        CalculateAndShowCardTargetRange();
        TargetSelectionRequested?.Invoke(
            $"{selectedCardInfo.ActionInfo.DisplayName}: 사거리 안의 대상을 우클릭하세요.");
    }

    /// <summary>
    /// 대상 선택을 끝내고 카드 사용 확인 대기 상태로 전환한다.
    /// 밀치기·돌진 카드라면 확정 전에 예상 이동 결과를 갱신하고,
    /// 상위 UI가 확인 안내를 표시하도록 ConfirmationRequested 이벤트를 보낸다.
    /// </summary>
    private void OpenConfirmation()
    {
        // 대상은 이미 저장되었으므로 추가 클릭을 막고 확인 입력만 받는다.
        IsSelectingTarget = false;
        IsAwaitingConfirmation = true;

        // Push가 없는 카드는 기존 Preview를 숨기고, Push가 있으면 예상 충돌 위치를 표시한다.
        RefreshPushPreview();

        // Self 회복·보호막은 대상 이름 대신 자연스러운 자기 사용 확인 문구를 사용한다.
        bool isSelfRecovery = selectedCardInfo != null && selectedCardInfo.CardData != null &&
            selectedCardInfo.CardData.targetType == BattleCardTargetType.Self &&
            (BattleCardEffectDataQuery.ContainsEffect(selectedCardInfo.CardData, BattleCardEffectType.Heal) ||
             BattleCardEffectDataQuery.ContainsEffect(selectedCardInfo.CardData, BattleCardEffectType.Shield));
        ConfirmationRequested?.Invoke(isSelfRecovery
            ? $"{selectedCardInfo.ActionInfo.DisplayName}을(를) 사용하시겠습니까?"
            : $"{selectedCardInfo.ActionInfo.DisplayName} 카드를 사용하시겠습니까?");
    }

    /// <summary>
    /// 대상 선택이 끝난 뒤 선택 타일 또는 Player 타일을 중심으로 카드의 실제 영향 범위를 표시한다.
    /// 카드 사용 가능 사거리를 보여주는 CalculateAndShowCardTargetRange()와 달리, 이 함수는 카드가 확정됐을 때
    /// 피해·회복·지속 영역이 적용될 타일을 미리 보여준다.
    /// </summary>
    private void ShowEffectAreaPreview(MapInfo center)
    {
        // Single은 포션만 뜻하지 않는다. 단일 Enemy·Player·타일 하나에만 적용되어
        // 주변 타일 Preview가 필요 없는 모든 카드는 여기서 끝낸다.
        if (selectedCardInfo == null || selectedCardInfo.CardData == null || center == null ||
            selectedCardInfo.CardData.areaType == BattleCardAreaType.Single)
            return;

        // CreateArea는 사용 즉시 끝나는 범위 효과가 아니라 여러 턴 남는 바닥 영역이다.
        // 현재 치유 영역 데이터가 정사각형 배치를 사용하므로 별도 분기하지만,
        // 표시 모양을 effectType으로 결정하는 결합은 areaType 확장 시 제거할 기술부채다.
        if (BattleCardEffectDataQuery.ContainsEffect(selectedCardInfo.CardData, BattleCardEffectType.CreateArea))
        {
            int squareRadius = Mathf.Max(1, selectedCardInfo.CardData.areaSizeTiles);
            // TODO(CARD-RANGE-01): 등록된 맵 타일 목록을 직접 받아 매 Preview의 FindObjectsByType을 제거한다.
            foreach (MapInfo tile in FindObjectsByType<MapInfo>(FindObjectsSortMode.None))
            {
                if (tile == null) continue;
                Vector2Int offset = tile.Index - center.Index;
                if (Mathf.Abs(offset.x) > squareRadius || Mathf.Abs(offset.y) > squareRadius) continue;
                rangeVisualizer?.ShowCardRangeTile(tile, effectAreaColor);
            }
            RangeVisibilityChanged?.Invoke(true);
            return;
        }

        // Line 범위 방향을 계산하기 위해 Player가 서 있는 시작 타일을 함께 보관한다.
        MapInfo origin = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        int size = Mathf.Max(1, selectedCardInfo.CardData.areaSizeTiles);
        Queue<MapInfo> queue = new Queue<MapInfo>();
        Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
        queue.Enqueue(center);
        distances[center] = 0;

        // 중심 타일부터 상하좌우로 BFS 탐색하며 areaSizeTiles 거리 안의 후보를 수집한다.
        // 실제 표시 여부는 아래 IsEffectPreviewTile()이 Cross·Line·일반 범위 규칙으로 걸러낸다.
        while (queue.Count > 0)
        {
            MapInfo tile = queue.Dequeue();
            int distance = distances[tile];
            if (IsEffectPreviewTile(tile, center, origin, distance, size))
            {
                rangeVisualizer?.ShowCardRangeTile(tile, effectAreaColor);
            }

            if (distance >= size) continue;
            foreach (MapInfo neighbour in BattleTileRangeCalculator.GetNeighbours(tile))
            {
                if (neighbour == null || distances.ContainsKey(neighbour)) continue;
                distances[neighbour] = distance + 1;
                queue.Enqueue(neighbour);
            }
        }

        RangeVisibilityChanged?.Invoke(true);
    }

    private bool IsEffectPreviewTile(
        MapInfo tile, MapInfo center, MapInfo origin, int distance, int size)
    {
        if (tile == null || distance > size) return false;
        switch (selectedCardInfo.CardData.areaType)
        {
            case BattleCardAreaType.Cross:
                // 중심 타일과 같은 가로 또는 세로 축에 있는 타일만 남겨 십자 형태를 만든다.
                Vector2Int crossOffset = tile.Index - center.Index;
                return crossOffset.x == 0 || crossOffset.y == 0;
            case BattleCardAreaType.Line:
                // Player→선택 대상의 주축을 고른 뒤 그 축과 같은 직선상의 타일만 표시한다.
                if (origin == null) return false;
                Vector2Int direction = center.Index - origin.Index;
                Vector2Int lineOffset = tile.Index - center.Index;
                return Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                    ? lineOffset.y == 0
                    : lineOffset.x == 0;
            default:
                // 별도 모양 규칙이 없는 범위 카드는 BFS 거리 안의 모든 타일을 표시한다.
                return true;
        }
    }

    /// <summary>
    /// 현재 Player 타일에서 카드 사거리 안의 선택 가능 타일을 계산한 뒤 화면에 표시한다.
    /// 계산은 CalculateCardTargetableTiles(), 색상 표시는 ShowCardTargetableTiles()에 위임하며,
    /// 이 함수는 두 단계를 순서대로 연결하고 결과를 rangeTiles에 보관하는 역할만 한다.
    /// </summary>
    private void CalculateAndShowCardTargetRange()
    {
        // 이전 카드 Preview가 바꿨던 색상과 선택 가능 목록을 먼저 비운다.
        rangeVisualizer?.ClearCardRangeTiles();
        rangeTiles.Clear();

        MapInfo startTile = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        if (startTile == null)
        {
            Debug.LogError(
                "카드 사거리 계산 실패: Player가 서 있는 MapInfo 타일을 찾지 못했습니다. " +
                "Player 등록과 맵 타일 목록을 확인해야 합니다.",
                this);
            CancelAll();
            return;
        }

        HashSet<MapInfo> calculatedTargetableTiles = CalculateCardTargetableTiles(
            startTile,
            selectedCardInfo.ActionInfo.RangeTiles);
        rangeTiles.UnionWith(calculatedTargetableTiles);
        ShowCardTargetableTiles(rangeTiles);
        RangeVisibilityChanged?.Invoke(true);
    }

    /// <summary>
    /// 시작 타일에서 상하좌우로 이동한 칸 수가 카드 사거리 이하인 타일을 계산한다.
    /// Player가 서 있는 시작 타일은 일반 Enemy·Tile 대상 카드가 자신을 선택하지 않도록 결과에서 제외한다.
    /// 타일 색상이나 UI 상태는 변경하지 않는 순수 사거리 계산 단계다.
    /// </summary>
    private static HashSet<MapInfo> CalculateCardTargetableTiles(MapInfo startTile, int rangeInTiles)
    {
        HashSet<MapInfo> targetableTiles = new HashSet<MapInfo>();
        Queue<MapInfo> queue = new Queue<MapInfo>();
        Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
        queue.Enqueue(startTile);
        distances[startTile] = 0;

        while (queue.Count > 0)
        {
            MapInfo current = queue.Dequeue();
            int distance = distances[current];
            if (distance > 0)
            {
                targetableTiles.Add(current);
            }

            if (distance >= rangeInTiles)
                continue;

            foreach (MapInfo neighbour in BattleTileRangeCalculator.GetNeighbours(current))
            {
                if (neighbour == null || distances.ContainsKey(neighbour))
                    continue;
                distances[neighbour] = distance + 1;
                queue.Enqueue(neighbour);
            }
        }

        return targetableTiles;
    }

    /// <summary>
    /// 계산이 끝난 선택 가능 타일의 기존 색상을 저장하고 카드 사거리 색상을 적용한다.
    /// 대상 선택 판정 데이터는 변경하지 않으며 화면 표현만 담당한다.
    /// </summary>
    private void ShowCardTargetableTiles(IEnumerable<MapInfo> targetableTiles)
    {
        foreach (MapInfo tile in targetableTiles)
        {
            rangeVisualizer?.ShowCardRangeTile(tile, rangeColor);
        }
    }

    private bool RequiresExternalTarget()
    {
        BattleCardTargetType targetType = TargetType;
        return targetType == BattleCardTargetType.Enemy ||
               targetType == BattleCardTargetType.Character ||
               targetType == BattleCardTargetType.Tile;
    }

    private int GetModifiedCardCost(int baseCost, BattleCardData card)
    {
        // 현재는 동상(Frostbite)이 공격 카드와 기본 공격의 MP 비용을 1 증가시킨다.
        // 공격 카드가 아니거나 Player에게 상태이상 저장소가 없으면 데이터의 기본 비용을 그대로 사용한다.
        return playerStatusEffects != null && card != null && card.category == BattleCardCategory.Attack
            ? playerStatusEffects.ModifyAttackCost(baseCost)
            : baseCost;
    }

    /// <summary>
    /// 카드 Pipeline이 요청한 상태이상을 대상 Unit의 공용 상태 저장소에 적용한다.
    /// BattleStatusEffects는 독·화상·동상 같은 공용 상태를 저장하고, BattleEnemyControlState는
    /// 실제 행동을 막아야 하는 기절·속박을 별도로 적용한다. 두 저장소의 중복은 상태 시스템 통합 전 기술부채다.
    /// </summary>
    private void ApplyStatusToUnit(GameObject unit, BattleStatusType type, int turns)
    {
        // 대상이 효과 실행 전에 사망·파괴됐다면 상태를 적용할 GameObject가 없으므로 종료한다.
        if (unit == null) return;

        // 이전 Prefab에 BattleStatusEffects가 없을 수 있어 현재는 GetOrAdd로 호환한다.
        // Enemy Prefab 구성이 확정되면 직접 참조로 교체할 임시 호환 경로다.
        BattleStatusEffects effects = BattleStatusEffects.GetOrAdd(unit);
        effects?.Apply(type, turns, player);

        // 상태 아이콘 View가 새 저장소를 구독하게 한다. BindStatusSource는 기존 구독을 먼저 해제해
        // 같은 저장소가 다시 전달돼도 Changed 이벤트가 중복 등록되지 않는다.
        unit.GetComponent<BattleEnemyStatusView>()?.BindStatusSource(effects);

        // 기절·속박은 목록에 표시하는 것만으로 부족하고 Enemy 행동 자체를 막아야 한다.
        if (type == BattleStatusType.Stun || type == BattleStatusType.Root)
        {
            BattleEnemyControlState control = unit.GetComponent<BattleEnemyControlState>();
            if (control == null)
            {
                Debug.LogError(
                    $"{unit.name}에게 {type}을 적용할 수 없습니다: " +
                    "BattleEnemyControlState가 Enemy Prefab에 없습니다.",
                    unit);
                return;
            }

            if (type == BattleStatusType.Stun) control.ApplyStun(turns);
            else control.ApplyRoot(turns);
        }
    }

    /// <summary>
    /// Player 중심 카드 효과 범위 안에 있는 살아 있는 Enemy를 Push Preview 대상으로 수집한다.
    /// 실제 Push 적용이 아니라 확정 전 예상 결과를 여러 개 표시하기 위한 임시 List를 반환한다.
    /// 대상은 화면에서 시계 방향으로 정렬해 Preview 생성 순서가 매 실행마다 달라지지 않게 한다.
    /// </summary>
    private List<GameObject> CollectPushTargetsInArea(MapInfo playerTile)
    {
        List<GameObject> pushPreviewTargets = new List<GameObject>();

        // EnemyTurnActor가 붙은 활성 전투 개체만 찾는다. 단순 Enemy 태그가 아니라 실제 턴에 참여하는
        // Unit을 기준으로 하므로 장식용 Enemy 모델이나 비전투 오브젝트는 포함되지 않는다.
        foreach (EnemyTurnActor enemy in FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None))
        {
            // 검색 결과를 순회하기 전에 같은 프레임에 파괴될 수 있고, 풀링·연출 때문에 비활성인
            // Enemy는 현재 전투 대상이 아니므로 Preview 계산에서 제외한다.
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            // 사망 여부는 BattleHealth, 격자상 거리는 Enemy 월드 위치에 가장 가까운 MapInfo로 계산한다.
            BattleHealth health = enemy.GetComponent<BattleHealth>();
            MapInfo enemyTile = findClosestTile(enemy.transform.position);

            // areaSizeTiles가 0으로 잘못 저장돼도 광역 Push가 최소 인접 1칸은 검사하도록 보정한다.
            int previewAreaRadius = Mathf.Max(1, selectedCardInfo.CardData.areaSizeTiles);
            int distance = BattleTileRangeCalculator.GetDistance(
                playerTile,
                enemyTile,
                previewAreaRadius);

            // 거리 0은 Player 자신의 타일이고 음수는 범위 밖·경로 없음이므로 살아 있는 범위 내 Enemy만 추가한다.
            if (health != null && !health.IsDead && distance > 0)
            {
                pushPreviewTargets.Add(enemy.gameObject);
            }
        }

        // Hash/검색 순서는 보장되지 않으므로 Player 기준 시계 방향 각도로 정렬해 처리 순서를 고정한다.
        pushPreviewTargets.Sort((left, right) =>
            GetClockwiseAngle(player.transform.position, left.transform.position).CompareTo(
                GetClockwiseAngle(player.transform.position, right.transform.position)));
        return pushPreviewTargets;
    }

    /// <summary>
    /// 중심에서 대상까지의 XZ 방향을 0~360도 시계 방향 각도로 변환한다.
    /// 광역 Push 대상의 Preview 생성 순서를 공간상 일정하게 유지하는 정렬 기준이다.
    /// </summary>
    private static float GetClockwiseAngle(Vector3 center, Vector3 target)
    {
        Vector3 offset = target - center;
        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        return angle < 0f ? angle + 360f : angle;
    }

    /// <summary>
    /// 자동 대상 선택 카드가 사용할 Enemy를 찾는다. 카드 사거리 안의 살아 있는 Enemy 중 현재 HP가
    /// 가장 낮은 대상을 우선하며, HP가 같으면 Player와 더 가까운 대상을 선택한다.
    /// 성공하면 Enemy GameObject와 그 Enemy가 서 있는 MapInfo를 함께 반환한다.
    /// </summary>
    private bool TrySelectLowestHealthEnemyInRange(out GameObject target, out MapInfo targetTile)
    {
        // out 값은 실패 시에도 이전 호출 결과가 남지 않도록 항상 null에서 시작한다.
        target = null;
        targetTile = null;

        // 자동 대상 사거리 계산의 기준은 현재 Player가 서 있는 타일이다.
        MapInfo playerTile = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        if (playerTile == null || selectedCardInfo == null)
        {
            return false;
        }

        float lowestHealthFound = float.MaxValue;
        int closestDistanceAmongLowestHealth = int.MaxValue;
        EnemyTurnActor[] enemies = FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            // 파괴됐거나 비활성인 턴 참여자는 현재 자동 선택 후보가 아니다.
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            // 후보의 생존 상태와 Player로부터의 격자 거리를 계산한다.
            BattleHealth health = enemy.GetComponent<BattleHealth>();
            MapInfo enemyTile = findClosestTile(enemy.transform.position);
            int distance = BattleTileRangeCalculator.GetDistance(
                playerTile,
                enemyTile,
                selectedCardInfo.ActionInfo.RangeTiles);
            // HP 데이터가 없거나 사망했거나 카드 사거리 밖이면 후보에서 제외한다.
            if (health == null || health.IsDead || distance < 0)
            {
                continue;
            }

            // HP가 더 낮은 Enemy로 교체한다. HP가 같으면 가까운 Enemy를 선택해 결과를 결정적으로 만든다.
            if (health.CurrentHealth < lowestHealthFound ||
                (Mathf.Approximately(health.CurrentHealth, lowestHealthFound) &&
                 distance < closestDistanceAmongLowestHealth))
            {
                lowestHealthFound = health.CurrentHealth;
                closestDistanceAmongLowestHealth = distance;
                target = enemy.gameObject;
                targetTile = enemyTile;
            }
        }

        return target != null && targetTile != null;
    }

    /// <summary>카드 선택 상태를 초기화하고 상위 UI에도 사용 취소를 알린다.</summary>
    private void CancelAll()
    {
        ClearStateAndRange();
        Cancelled?.Invoke();
    }

    /// <summary>
    /// 카드 행동 중 보관한 대상, 카드 요청, DrawSystem 참조와 모든 Preview를 초기 상태로 되돌린다.
    /// 카드 사용 성공·사용자 취소·턴 초기화가 함께 사용한다. 취소 이벤트는 발생시키지 않지만,
    /// RangeVisibilityChanged(false)를 보내 상위 입력/UI가 사거리 표시 종료를 반영하게 한다.
    /// </summary>
    private void ClearStateAndRange()
    {
        pushPreviewView?.HideAllPushPreviews();
        IsSelectingTarget = false;
        IsAwaitingConfirmation = false;
        selectedCardInfo = null;
        drawSystem = null;
        selectedTarget = null;
        selectedTargetTile = null;
        rangeTiles.Clear();
        rangeVisualizer?.ClearCardRangeTiles();
        RangeVisibilityChanged?.Invoke(false);
    }

    /// <summary>
    /// 확정 전 Push 효과의 예상 결과를 표시한다. Push가 아니면 기존 표시를 숨긴다.
    /// 돌진 후 밀치기는 돌진 도착 타일을 밀치기 시작점으로 계산하고,
    /// 회전 공격은 인접한 모든 대상의 밀치기 계획을 각각 만들어 동시에 표시한다.
    /// </summary>
    private void RefreshPushPreview()
    {
        if (pushPreviewView == null || selectedCardInfo == null || selectedTarget == null ||
            !BattleCardEffectDataQuery.TryFindFirstEffect(
                selectedCardInfo.CardData,
                BattleCardEffectType.Push,
                out BattleCardEffectData pushEffect))
        {
            pushPreviewView?.HideAllPushPreviews();
            return;
        }

        // Push 대상이 TargetsInArea면 특정 카드 이름을 추론하지 않고 Player 주변 전체를 Preview한다.
        if (pushEffect.effectTarget == BattleCardEffectTarget.TargetsInArea)
        {
            MapInfo playerTile = findClosestTile != null ? findClosestTile(player.transform.position) : null;
            List<BattleCardMovementService.PushPlan> pushPlans = new List<BattleCardMovementService.PushPlan>();
            foreach (GameObject target in CollectPushTargetsInArea(playerTile))
            {
                // TryCreatePushPlan은 실제 이동시키지 않고 목적지·벽/Enemy 충돌·물 추락 결과만 계산한다.
                if (BattleCardMovementService.TryCreatePushPlan(
                        player,
                        target,
                        Mathf.Max(0, pushEffect.distanceTiles),
                        Mathf.Max(1, pushEffect.pushForce),
                        out BattleCardMovementService.PushPlan plan))
                {
                    pushPlans.Add(plan);
                }
            }
            pushPreviewView.ShowPushPredictions(pushPlans);
            return;
        }

        // 돌진 후 밀치기 카드는 현재 Player 위치가 아니라 돌진 완료 예상 타일에서 밀기 방향을 계산해야 한다.
        MapInfo predictedSourceTile = null;
        if (BattleCardEffectDataQuery.ContainsEffect(selectedCardInfo.CardData, BattleCardEffectType.Dash) &&
            BattleCardMovementService.TryCreateDashPlan(
                player,
                selectedTarget,
                BattleCardEffectDataQuery.FindLongestMovementDistance(selectedCardInfo.CardData, BattleCardEffectType.Dash),
                out BattleCardMovementService.MovementPlan dashPlan,
                out _))
        {
            predictedSourceTile = dashPlan.Destination;
        }

        // Dash가 없는 일반 밀치기 카드도 이 공통 경로로 들어오며 predictedSourceTile만 null이다.
        // TryCreatePushPlan이 null 시작점을 받으면 현재 Player 타일을 기준으로 Push 결과를 계산한다.
        bool pushPlanCreated = BattleCardMovementService.TryCreatePushPlan(
            player,
            selectedTarget,
            predictedSourceTile,
            BattleCardEffectDataQuery.FindLongestMovementDistance(selectedCardInfo.CardData, BattleCardEffectType.Push),
            BattleCardEffectDataQuery.FindStrongestPushForce(selectedCardInfo.CardData),
            out BattleCardMovementService.PushPlan pushPlan);
        if (pushPlanCreated)
        {
            pushPreviewView.ShowSinglePushPrediction(pushPlan);
        }
        else
        {
            pushPreviewView.HideAllPushPreviews();
        }
    }

}

/// <summary>
/// 카드 데이터의 대상 선택 정책에 따라 Player가 대상을 직접 고를지 시스템이 자동 선택할지를 판정한다.
/// 이동·피해 같은 효과 종류와 대상 선택 방식을 분리해 같은 Teleport라도 수동 또는 자동 대상을 사용할 수 있다.
/// </summary>
internal static class BattleCardTargetSelectionRules
{
    /// <summary>이 카드가 사거리 안에서 현재 HP가 가장 낮은 Enemy를 자동 선택하도록 설정됐는지 반환한다.</summary>
    internal static bool UsesLowestHealthEnemyAutoTarget(BattleCardData card)
    {
        return card != null &&
               card.targetSelectionMode == BattleCardTargetSelectionMode.LowestHealthEnemyInRange;
    }
}

/// <summary>
/// <see cref="BattleCardData.effects"/>에 저장된 효과 설정을 읽는 조회 전용 도우미다.
/// 실제 피해·회복·보호막·이동 적용은 BattleCardEffectPipeline이 담당한다.
/// 이 클래스를 조회 전용으로 제한해 같은 효과가 서로 다른 경로에서 실행되는 문제를 막는다.
/// </summary>
internal static class BattleCardEffectDataQuery
{
    /// <summary>
    /// 카드에 같은 종류의 효과가 여러 개 있어도 목록에서 가장 먼저 등록된 효과 하나만 반환한다.
    /// 호출부가 개별 효과의 대상 규칙·거리·강도를 읽어야 할 때 사용한다.
    /// 반환값은 발견 여부이며, 실제 효과 데이터는 <paramref name="foundEffect"/>로 전달한다.
    /// </summary>
    public static bool TryFindFirstEffect(
        BattleCardData card,
        BattleCardEffectType effectType,
        out BattleCardEffectData foundEffect)
    {
        foundEffect = card != null && card.effects != null
            ? card.effects.Find(effect => effect != null && effect.effectType == effectType)
            : null;
        return foundEffect != null;
    }

    /// <summary>카드 효과 목록에 지정한 효과 종류가 하나라도 포함되어 있는지 확인한다.</summary>
    public static bool ContainsEffect(BattleCardData card, BattleCardEffectType effectType)
    {
        if (card == null || card.effects == null)
        {
            return false;
        }

        return card.effects.Exists(effect => effect != null && effect.effectType == effectType);
    }

    /// <summary>
    /// 지정한 이동 효과가 여러 개 등록된 경우 가장 긴 타일 거리를 반환한다.
    /// 현재는 실제 효과 실행이 아니라 Dash/Push 예상 위치를 그리는 미리보기 계산에서만 사용한다.
    /// 여러 이동 효과를 순서대로 실행하는 카드라면 최대값 하나로는 실제 최종 위치를 표현할 수 없으므로
    /// 향후 Pipeline이 만든 이동 계획 자체를 Preview에 전달하는 방향이 더 정확하다.
    /// </summary>
    public static int FindLongestMovementDistance(BattleCardData card, BattleCardEffectType effectType)
    {
        int longestDistanceInTiles = 0;
        if (card == null || card.effects == null)
        {
            return longestDistanceInTiles;
        }

        foreach (BattleCardEffectData effect in card.effects)
        {
            if (effect != null && effect.effectType == effectType)
            {
                longestDistanceInTiles = Mathf.Max(longestDistanceInTiles, effect.distanceTiles);
            }
        }

        return longestDistanceInTiles;
    }

    /// <summary>
    /// 카드에 Push 효과가 여러 개 있으면 가장 큰 밀치기 강도를 반환한다.
    /// pushForce는 피해량이 아니라 대상을 실제로 몇 칸 밀지 계산할 때 사용하는 값이다.
    /// Push가 없거나 데이터가 비어 있으면 기존 이동 서비스의 최소 규칙에 맞춰 1을 반환한다.
    /// </summary>
    public static int FindStrongestPushForce(BattleCardData card)
    {
        int strongestPushForce = 1;
        if (card == null || card.effects == null)
        {
            return strongestPushForce;
        }

        foreach (BattleCardEffectData effect in card.effects)
        {
            if (effect != null && effect.effectType == BattleCardEffectType.Push)
            {
                strongestPushForce = Mathf.Max(strongestPushForce, effect.pushForce);
            }
        }

        return strongestPushForce;
    }
}
