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
    private readonly Dictionary<Renderer, Color> cardRangeOriginalColors =
        new Dictionary<Renderer, Color>();
    private GameObject player;
    private BattleUnitMP playerMP;
    private BattleRangeVisualizer rangeVisualizer;
    private Color rangeColor;
    private Color effectAreaColor;
    private Func<Vector3, MapInfo> findClosestTile;
    private Camera battleCamera;
    private BattlePushPreviewView pushPreviewView;
    private CardUseWaitingForConfirmation pendingUse;
    private BattleCardDrawSystem drawSystem;
    private GameObject selectedTarget;
    private MapInfo selectedTargetTile;

    public bool IsSelectingTarget { get; private set; }
    public bool IsAwaitingConfirmation { get; private set; }
    /// <summary>카드 대상 선택 또는 사용 확인이 진행 중인지 반환한다.</summary>
    public bool IsActionActive => IsSelectingTarget || IsAwaitingConfirmation;
    public BattleCardTargetType TargetType => pendingUse != null
        ? pendingUse.CardData.targetType
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
        rangeVisualizer = visualizer;
        rangeColor = cardRangeColor;
        effectAreaColor = cardEffectAreaColor;
        findClosestTile = tileFinder;
        battleCamera = camera;
        pushPreviewView = previewView;
        pushPreviewView?.ConfigurePreviewDependencies(battleCamera);
    }

    /// <summary>손패 카드 요청을 검증하고 대상 선택 및 확인 대기 상태를 시작한다. 실제 효과는 아직 적용하지 않는다.</summary>
    public bool TryStartCardUse(CardUseWaitingForConfirmation cardUse, BattleCardDrawSystem cardDrawSystem, bool canUseCards)
    {
        if (cardUse == null || cardUse.CardData == null || cardDrawSystem == null || player == null ||
            IsSelectingTarget || IsAwaitingConfirmation || !canUseCards)
        {
            return false;
        }

        int cardCost = GetModifiedCardCost(cardUse.ActionRequest.MPCost, cardUse.CardData);
        if (playerMP == null || !playerMP.CanSpend(cardCost))
        {
            Debug.Log($"카드 사용 불가: 행동력이 {cardCost} 필요합니다.", this);
            return false;
        }

        pendingUse = cardUse;
        drawSystem = cardDrawSystem;
        if (BattleCardTargetSelectionRules.SelectsLowestHealthEnemyAutomatically(pendingUse.CardData))
        {
            if (!TrySelectLowestHealthEnemyInRange(out selectedTarget, out selectedTargetTile))
            {
                OpenTargetSelection();
                return true;
            }

            OpenConfirmation();
            return true;
        }

        BattleCardTargetType targetType = pendingUse.CardData.targetType;
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
            BattleCardTargetSelectionRules.SelectsLowestHealthEnemyAutomatically(pendingUse.CardData);
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
        if (!BattleCardEffectPipeline.TryPrepare(
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
        BattleActionRequest resultRequest = pendingUse.ActionRequest;
        // 사전 검증된 효과만 실행하고, 이후 카드 행동이 끝났음을 상위 입력 흐름에 알린다.
        BattleCardEffectPipeline.Execute(effectContext, preparedEffects);
        BattleActionResult result = new BattleActionResult(
            resultRequest, player, resultTarget, Array.Empty<MapInfo>(), 0, cardCost);
        ClearStateAndRange();
        Confirmed?.Invoke(result);
        return true;
    }

    /// <summary>확정 단계 진입에 필요한 카드, 드로우 시스템과 Player 참조가 남아 있는지 확인한다.</summary>
    private bool CanAttemptConfirmation()
    {
        return IsAwaitingConfirmation && pendingUse != null && drawSystem != null && player != null;
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
        cardCost = pendingUse != null
            ? GetModifiedCardCost(pendingUse.ActionRequest.MPCost, pendingUse.CardData)
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
            Card = pendingUse.CardData,
            CardIndex = pendingUse.CardIndex,
            Request = pendingUse.ActionRequest,
            FindClosestTile = findClosestTile,
            ApplyStatus = ApplyStatusToUnit,
            RangeVisualizer = rangeVisualizer,
            PersistentAreaColor = effectAreaColor,
            DrawSystem = drawSystem,
            ConsumedCardUse = pendingUse
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

        if (!drawSystem.MoveConfirmedCardToUsedPile(pendingUse))
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
        pushPreviewView?.HidePushPredictions();
        IsAwaitingConfirmation = false;
        IsSelectingTarget = true;
        selectedTarget = null;
        selectedTargetTile = null;
        CalculateAndShowCardTargetRange();
        TargetSelectionRequested?.Invoke(
            $"{pendingUse.ActionRequest.DisplayName}: 사거리 안의 대상을 우클릭하세요.");
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
        bool isSelfRecovery = pendingUse != null && pendingUse.CardData != null &&
            pendingUse.CardData.targetType == BattleCardTargetType.Self &&
            (BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Heal) ||
             BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Shield));
        ConfirmationRequested?.Invoke(isSelfRecovery
            ? $"{pendingUse.ActionRequest.DisplayName}을(를) 사용하시겠습니까?"
            : $"{pendingUse.ActionRequest.DisplayName} 카드를 사용하시겠습니까?");
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
        if (pendingUse == null || pendingUse.CardData == null || center == null ||
            pendingUse.CardData.areaType == BattleCardAreaType.Single)
            return;

        // CreateArea는 사용 즉시 끝나는 범위 효과가 아니라 여러 턴 남는 바닥 영역이다.
        // 현재 치유 영역 데이터가 정사각형 배치를 사용하므로 별도 분기하지만,
        // 표시 모양을 effectType으로 결정하는 결합은 areaType 확장 시 제거할 기술부채다.
        if (BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.CreateArea))
        {
            int squareRadius = Mathf.Max(1, pendingUse.CardData.areaSizeTiles);
            // TODO(CARD-RANGE-01): 등록된 맵 타일 목록을 직접 받아 매 Preview의 FindObjectsByType을 제거한다.
            foreach (MapInfo tile in FindObjectsByType<MapInfo>(FindObjectsSortMode.None))
            {
                if (tile == null) continue;
                Vector2Int offset = tile.Index - center.Index;
                if (Mathf.Abs(offset.x) > squareRadius || Mathf.Abs(offset.y) > squareRadius) continue;
                CacheCardRangeOriginalColor(tile);
                rangeVisualizer?.ShowRangeTile(tile, effectAreaColor);
            }
            RangeVisibilityChanged?.Invoke(true);
            return;
        }

        // Line 범위 방향을 계산하기 위해 Player가 서 있는 시작 타일을 함께 보관한다.
        MapInfo origin = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        int size = Mathf.Max(1, pendingUse.CardData.areaSizeTiles);
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
                CacheCardRangeOriginalColor(tile);
                rangeVisualizer?.ShowRangeTile(tile, effectAreaColor);
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
        switch (pendingUse.CardData.areaType)
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
        RestoreCardRangeColors();
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
            pendingUse.ActionRequest.RangeTiles);
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
            CacheCardRangeOriginalColor(tile);
            rangeVisualizer?.ShowRangeTile(tile, rangeColor);
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
        BattleStatusEffects status = player != null ? player.GetComponent<BattleStatusEffects>() : null;
        return status != null && card != null && card.category == BattleCardCategory.Attack
            ? status.ModifyAttackCost(baseCost)
            : baseCost;
    }

    private void ApplyStatusToUnit(GameObject unit, BattleStatusType type, int turns)
    {
        if (unit == null) return;
        BattleStatusEffects effects = BattleStatusEffects.GetOrAdd(unit);
        effects?.Apply(type, turns, player);
        unit.GetComponent<BattleEnemyStatusView>()?.BindStatusSource(effects);
        if (type == BattleStatusType.Stun || type == BattleStatusType.Root)
        {
            BattleEnemyControlState control = unit.GetComponent<BattleEnemyControlState>();
            if (control != null)
            {
                if (type == BattleStatusType.Stun) control.ApplyStun(turns);
                else control.ApplyRoot(turns);
            }
        }
    }

    /// <summary>Player 중심 카드 효과 범위 안에 있는 살아 있는 Enemy를 Push Preview 대상으로 수집한다.</summary>
    private List<GameObject> CollectPushTargetsInArea(MapInfo playerTile)
    {
        List<GameObject> targets = new List<GameObject>();
        foreach (EnemyTurnActor enemy in FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None))
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            BattleHealth health = enemy.GetComponent<BattleHealth>();
            MapInfo enemyTile = findClosestTile(enemy.transform.position);
            int distance = BattleTileRangeCalculator.GetDistance(
                playerTile,
                enemyTile,
                Mathf.Max(1, pendingUse.CardData.areaSizeTiles));
            if (health != null && !health.IsDead && distance > 0)
            {
                targets.Add(enemy.gameObject);
            }
        }

        targets.Sort((left, right) =>
            GetClockwiseAngle(player.transform.position, left.transform.position).CompareTo(
                GetClockwiseAngle(player.transform.position, right.transform.position)));
        return targets;
    }

    private static float GetClockwiseAngle(Vector3 center, Vector3 target)
    {
        Vector3 offset = target - center;
        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        return angle < 0f ? angle + 360f : angle;
    }

    /// <summary>카드 사거리 안의 살아 있는 Enemy 중 현재 HP가 가장 낮은 대상을 자동 선택한다.</summary>
    private bool TrySelectLowestHealthEnemyInRange(out GameObject target, out MapInfo targetTile)
    {
        target = null;
        targetTile = null;
        MapInfo playerTile = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        if (playerTile == null || pendingUse == null)
        {
            return false;
        }

        float lowestHealth = float.MaxValue;
        int closestDistance = int.MaxValue;
        EnemyTurnActor[] enemies = FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            BattleHealth health = enemy.GetComponent<BattleHealth>();
            MapInfo enemyTile = findClosestTile(enemy.transform.position);
            int distance = BattleTileRangeCalculator.GetDistance(
                playerTile,
                enemyTile,
                pendingUse.ActionRequest.RangeTiles);
            if (health == null || health.IsDead || distance < 0)
            {
                continue;
            }

            if (health.CurrentHealth < lowestHealth ||
                (Mathf.Approximately(health.CurrentHealth, lowestHealth) && distance < closestDistance))
            {
                lowestHealth = health.CurrentHealth;
                closestDistance = distance;
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
        pushPreviewView?.HidePushPredictions();
        IsSelectingTarget = false;
        IsAwaitingConfirmation = false;
        pendingUse = null;
        drawSystem = null;
        selectedTarget = null;
        selectedTargetTile = null;
        rangeTiles.Clear();
        RestoreCardRangeColors();
        RangeVisibilityChanged?.Invoke(false);
    }

    /// <summary>
    /// 확정 전 Push 효과의 예상 결과를 표시한다. Push가 아니면 기존 표시를 숨긴다.
    /// 돌진 후 밀치기는 돌진 도착 타일을 밀치기 시작점으로 계산하고,
    /// 회전 공격은 인접한 모든 대상의 밀치기 계획을 각각 만들어 동시에 표시한다.
    /// </summary>
    private void RefreshPushPreview()
    {
        if (pushPreviewView == null || pendingUse == null || selectedTarget == null ||
            !BattleCardEffectExecutor.TryGetEffect(
                pendingUse.CardData,
                BattleCardEffectType.Push,
                out BattleCardEffectData pushEffect))
        {
            pushPreviewView?.HidePushPredictions();
            return;
        }

        // Push 대상이 TargetsInArea면 특정 카드 이름을 추론하지 않고 Player 주변 전체를 Preview한다.
        if (pushEffect.effectTarget == BattleCardEffectTarget.TargetsInArea)
        {
            MapInfo playerTile = findClosestTile != null ? findClosestTile(player.transform.position) : null;
            List<BattleCardMovementService.PushPlan> plans = new List<BattleCardMovementService.PushPlan>();
            foreach (GameObject target in CollectPushTargetsInArea(playerTile))
            {
                if (BattleCardMovementService.TryCreatePushPlan(
                        player,
                        target,
                        Mathf.Max(0, pushEffect.distanceTiles),
                        Mathf.Max(1, pushEffect.pushForce),
                        out BattleCardMovementService.PushPlan plan))
                {
                    plans.Add(plan);
                }
            }
            pushPreviewView.ShowPushPredictions(plans);
            return;
        }

        MapInfo predictedSourceTile = null;
        if (BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Dash) &&
            BattleCardMovementService.TryCreateDashPlan(
                player,
                selectedTarget,
                BattleCardEffectExecutor.GetMaximumDistance(pendingUse.CardData, BattleCardEffectType.Dash),
                out BattleCardMovementService.MovementPlan dashPlan,
                out _))
        {
            predictedSourceTile = dashPlan.Destination;
        }

        bool planned = BattleCardMovementService.TryCreatePushPlan(
            player,
            selectedTarget,
            predictedSourceTile,
            BattleCardEffectExecutor.GetMaximumDistance(pendingUse.CardData, BattleCardEffectType.Push),
            BattleCardEffectExecutor.GetMaximumPushForce(pendingUse.CardData),
            out BattleCardMovementService.PushPlan pushPlan);
        if (planned)
        {
            pushPreviewView.ShowSinglePushPrediction(pushPlan);
        }
        else
        {
            pushPreviewView.HidePushPredictions();
        }
    }

    /// <summary>카드 사거리로 덮기 직전의 타일 색상을 카드 행동 전용으로 저장한다.</summary>
    private void CacheCardRangeOriginalColor(MapInfo tile)
    {
        if (tile == null)
        {
            return;
        }

        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.material.HasProperty("_Color") ||
                cardRangeOriginalColors.ContainsKey(renderer))
            {
                continue;
            }

            cardRangeOriginalColors[renderer] = renderer.material.color;
        }
    }

    /// <summary>카드 사거리 표시가 직접 변경한 Renderer만 표시 직전 색상으로 복구한다.</summary>
    private void RestoreCardRangeColors()
    {
        foreach (KeyValuePair<Renderer, Color> entry in cardRangeOriginalColors)
        {
            if (entry.Key != null && entry.Key.material.HasProperty("_Color"))
            {
                entry.Key.material.color = entry.Value;
            }
        }

        cardRangeOriginalColors.Clear();
    }
}

/// <summary>
/// 카드 데이터에 따라 Player가 대상을 직접 고를지, 시스템이 자동 선택할지를 판정한다.
/// BattleCardActionController가 Teleport 같은 개별 효과 종류를 직접 비교하지 않도록 대상 선택 규칙을 분리한다.
/// </summary>
internal static class BattleCardTargetSelectionRules
{
    /// <summary>순간이동 카드는 사거리 안에서 현재 HP가 가장 낮은 Enemy를 자동 선택한다.</summary>
    internal static bool SelectsLowestHealthEnemyAutomatically(BattleCardData card)
    {
        return BattleCardEffectExecutor.HasEffect(card, BattleCardEffectType.Teleport);
    }
}

/// <summary>
/// 카드 효과 목록에서 현재 구현된 단순 효과를 검사하고 실행합니다.
/// 첫 MVP에서는 회복만 담당하며 보호막과 밀치기는 후속 단계에서 같은 진입점에 추가합니다.
/// </summary>
internal static class BattleCardEffectExecutor
{
    /// <summary>카드 데이터에서 지정한 종류의 첫 효과를 반환한다.</summary>
    public static bool TryGetEffect(
        BattleCardData card,
        BattleCardEffectType effectType,
        out BattleCardEffectData foundEffect)
    {
        foundEffect = card != null && card.effects != null
            ? card.effects.Find(effect => effect != null && effect.effectType == effectType)
            : null;
        return foundEffect != null;
    }

    public static bool HasEffect(BattleCardData card, BattleCardEffectType effectType)
    {
        if (card == null || card.effects == null)
        {
            return false;
        }

        return card.effects.Exists(effect => effect != null && effect.effectType == effectType);
    }

    public static bool CanApplyHealing(
        BattleCardData card,
        GameObject target,
        out string failureReason)
    {
        failureReason = string.Empty;
        BattleHealth health = target != null ? target.GetComponent<BattleHealth>() : null;
        if (health == null)
        {
            failureReason = "대상에게 체력 데이터가 없습니다.";
            return false;
        }

        if (health.IsDead)
        {
            failureReason = "사망한 대상은 회복할 수 없습니다.";
            return false;
        }

        if (health.CurrentHealth >= health.MaxHealth)
        {
            failureReason = "이미 최대 체력입니다.";
            return false;
        }

        if (GetTotalAmount(card, BattleCardEffectType.Heal) <= 0f)
        {
            failureReason = "회복 수치가 0 이하입니다.";
            return false;
        }

        return true;
    }

    public static bool TryApplyHealing(
        BattleCardData card,
        GameObject target,
        out float appliedHealing)
    {
        appliedHealing = 0f;
        if (!CanApplyHealing(card, target, out _))
        {
            return false;
        }

        BattleHealth health = target.GetComponent<BattleHealth>();
        appliedHealing = health.Heal(GetTotalAmount(card, BattleCardEffectType.Heal));
        return appliedHealing > 0f;
    }

    public static bool CanApplyShield(
        BattleCardData card,
        GameObject target,
        out string failureReason)
    {
        failureReason = string.Empty;
        BattleHealth health = target != null ? target.GetComponent<BattleHealth>() : null;
        if (health == null)
        {
            failureReason = "대상에게 체력 데이터가 없습니다.";
            return false;
        }

        if (health.IsDead)
        {
            failureReason = "사망한 대상에게 보호막을 부여할 수 없습니다.";
            return false;
        }

        if (GetTotalAmount(card, BattleCardEffectType.Shield) <= 0f)
        {
            failureReason = "보호막 수치가 0 이하입니다.";
            return false;
        }

        return true;
    }

    public static bool TryApplyShield(
        BattleCardData card,
        GameObject target,
        out float appliedShield)
    {
        appliedShield = 0f;
        if (!CanApplyShield(card, target, out _))
        {
            return false;
        }

        BattleHealth health = target.GetComponent<BattleHealth>();
        appliedShield = health.AddShield(GetTotalAmount(card, BattleCardEffectType.Shield));
        return appliedShield > 0f;
    }

    public static float GetTotalAmount(BattleCardData card, BattleCardEffectType effectType)
    {
        float total = 0f;
        foreach (BattleCardEffectData effect in card.effects)
        {
            if (effect != null && effect.effectType == effectType)
            {
                total += Mathf.Max(0f, effect.amount) * Mathf.Max(1, effect.repeatCount);
            }
        }

        return total;
    }

    public static int GetMaximumDistance(BattleCardData card, BattleCardEffectType effectType)
    {
        int distance = 0;
        if (card == null || card.effects == null)
        {
            return distance;
        }

        foreach (BattleCardEffectData effect in card.effects)
        {
            if (effect != null && effect.effectType == effectType)
            {
                distance = Mathf.Max(distance, effect.distanceTiles);
            }
        }

        return distance;
    }

    public static int GetMaximumPushForce(BattleCardData card)
    {
        int force = 1;
        if (card == null || card.effects == null)
        {
            return force;
        }

        foreach (BattleCardEffectData effect in card.effects)
        {
            if (effect != null && effect.effectType == BattleCardEffectType.Push)
            {
                force = Mathf.Max(force, effect.pushForce);
            }
        }

        return force;
    }
}
