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
    private BattleRangeVisualizer rangeVisualizer;
    private Color rangeColor;
    private Color effectAreaColor;
    private Func<Vector3, MapInfo> findClosestTile;
    private Camera battleCamera;
    private BattlePushPreviewView pushPreviewView;
    private PendingBattleCardUse pendingUse;
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
        rangeVisualizer = visualizer;
        rangeColor = cardRangeColor;
        effectAreaColor = cardEffectAreaColor;
        findClosestTile = tileFinder;
        battleCamera = camera;
        pushPreviewView = previewView;
        pushPreviewView?.ConfigurePreviewDependencies(battleCamera);
    }

    /// <summary>손패 카드 요청을 검증하고 대상 선택 및 확인 대기 상태를 시작한다. 실제 효과는 아직 적용하지 않는다.</summary>
    public bool Begin(PendingBattleCardUse cardUse, BattleCardDrawSystem cardDrawSystem, bool canUseCards)
    {
        if (cardUse == null || cardUse.CardData == null || cardDrawSystem == null || player == null ||
            IsSelectingTarget || IsAwaitingConfirmation || !canUseCards)
        {
            return false;
        }

        BattleUnitMP playerMP = player.GetComponent<BattleUnitMP>();
        int cardCost = GetModifiedCardCost(cardUse.ActionRequest.MPCost, cardUse.CardData);
        if (playerMP == null || !playerMP.CanSpend(cardCost))
        {
            Debug.Log($"카드 사용 불가: 행동력이 {cardCost} 필요합니다.", this);
            return false;
        }

        pendingUse = cardUse;
        drawSystem = cardDrawSystem;
        if (BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Teleport))
        {
            if (!TrySelectLowestHealthEnemyInRange(out selectedTarget, out selectedTargetTile))
            {
                ReturnToTargetSelection();
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
            ReturnToTargetSelection();
        }

        return true;
    }

    /// <summary>현재 카드의 대상 유형과 사거리 규칙에 맞는 대상인지 확인하고 유효한 선택만 보관한다.</summary>
    public bool TrySelectTarget(GameObject target, MapInfo targetTile)
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

    /// <summary>MP와 손패 보유 상태를 다시 검사한 뒤 카드 소비를 확정한다. 피해 효과 연결은 후속 시스템 책임이다.</summary>
    public void Confirm()
    {
        if (!IsAwaitingConfirmation || pendingUse == null || drawSystem == null || player == null)
        {
            return;
        }

        bool automaticTeleport = BattleCardEffectExecutor.HasEffect(
            pendingUse.CardData, BattleCardEffectType.Teleport);
        if (RequiresExternalTarget() && !automaticTeleport &&
            (selectedTarget == null || selectedTargetTile == null ||
             !selectedTarget.activeInHierarchy || !rangeTiles.Contains(selectedTargetTile)))
        {
            ReturnToTargetSelection();
            return;
        }

        BattleUnitMP playerMP = player.GetComponent<BattleUnitMP>();
        int cardCost = GetModifiedCardCost(pendingUse.ActionRequest.MPCost, pendingUse.CardData);
        if (playerMP == null || !playerMP.CanSpend(cardCost))
        {
            Debug.LogWarning($"카드 사용 불가: MP {cardCost}이 필요합니다.", this);
            return;
        }

        BattleCardEffectPipeline.Context context = new BattleCardEffectPipeline.Context
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
            ExecuteSpecial = effect =>
            {
                string code = effect.effectCode;
                if (string.Equals(code, "WEIRD_MUSHROOM", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(code, "이상한버섯", StringComparison.Ordinal))
                {
                    drawSystem.GenerateWeirdMushroomCard(pendingUse);
                }
                else if (string.Equals(code, "BASIC_ATTACK_DAMAGE", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(code, "기본공격피해증가", StringComparison.Ordinal))
                {
                    BattleComponentResolver.GetOrAdd<BattleBasicAttackBuff>(player, null)
                        .Add(effect.amount);
                }
            }
        };
        if (!BattleCardEffectPipeline.TryPrepare(
                context, out BattleCardEffectPipeline.PreparedUse prepared, out string failureReason))
        {
            Debug.LogWarning($"카드 사용 불가: {failureReason}", this);
            if (RequiresExternalTarget() && !automaticTeleport) ReturnToTargetSelection();
            return;
        }

        // 모든 효과의 사전 검증이 끝난 뒤에만 손패와 MP를 확정한다.
        if (!drawSystem.ConfirmCardUse(pendingUse))
        {
            CancelAll();
            return;
        }
        if (!playerMP.TrySpend(cardCost))
        {
            Debug.LogError("카드 제거 후 MP 차감에 실패했습니다. 사전 검증과 실제 상태가 달라졌습니다.", this);
            CancelAll();
            return;
        }

        GameObject resultTarget = selectedTarget;
        BattleActionRequest resultRequest = pendingUse.ActionRequest;
        BattleCardEffectPipeline.Execute(context, prepared);
        BattleActionResult result = new BattleActionResult(
            resultRequest, player, resultTarget, Array.Empty<MapInfo>(), 0, cardCost);
        ClearStateAndRange();
        Confirmed?.Invoke(result);
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

    private void ReturnToTargetSelection()
    {
        pushPreviewView?.HidePushPredictions();
        IsAwaitingConfirmation = false;
        IsSelectingTarget = true;
        selectedTarget = null;
        selectedTargetTile = null;
        BuildAndShowRange();
        TargetSelectionRequested?.Invoke(
            $"{pendingUse.ActionRequest.DisplayName}: 사거리 안의 대상을 우클릭하세요.");
    }

    private void OpenConfirmation()
    {
        IsSelectingTarget = false;
        IsAwaitingConfirmation = true;
        RefreshPushPreview();
        bool isSelfRecovery = pendingUse != null && pendingUse.CardData != null &&
            pendingUse.CardData.targetType == BattleCardTargetType.Self &&
            (BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Heal) ||
             BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Shield));
        ConfirmationRequested?.Invoke(isSelfRecovery
            ? $"{pendingUse.ActionRequest.DisplayName}을(를) 사용하시겠습니까?"
            : $"{pendingUse.ActionRequest.DisplayName} 카드를 사용하시겠습니까?");
    }

    /// <summary>선택한 타일 또는 사용자 자신을 중심으로 실제 효과 범위를 표시한다.</summary>
    private void ShowEffectAreaPreview(MapInfo center)
    {
        if (pendingUse == null || pendingUse.CardData == null || center == null ||
            pendingUse.CardData.areaType == BattleCardAreaType.Single)
            return;

        if (BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.CreateArea))
        {
            int squareRadius = Mathf.Max(1, pendingUse.CardData.areaSizeTiles);
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

        MapInfo origin = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        int size = Mathf.Max(1, pendingUse.CardData.areaSizeTiles);
        Queue<MapInfo> queue = new Queue<MapInfo>();
        Dictionary<MapInfo, int> distances = new Dictionary<MapInfo, int>();
        queue.Enqueue(center);
        distances[center] = 0;

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
                Vector2Int crossOffset = tile.Index - center.Index;
                return crossOffset.x == 0 || crossOffset.y == 0;
            case BattleCardAreaType.Line:
                if (origin == null) return false;
                Vector2Int direction = center.Index - origin.Index;
                Vector2Int lineOffset = tile.Index - center.Index;
                return Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                    ? lineOffset.y == 0
                    : lineOffset.x == 0;
            default:
                return true;
        }
    }

    private void BuildAndShowRange()
    {
        RestoreCardRangeColors();
        rangeTiles.Clear();
        MapInfo startTile = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        if (startTile == null)
        {
            CancelAll();
            return;
        }

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
                rangeTiles.Add(current);
                CacheCardRangeOriginalColor(current);
                rangeVisualizer?.ShowRangeTile(current, rangeColor);
            }

            if (distance >= pendingUse.ActionRequest.RangeTiles)
                continue;

            foreach (MapInfo neighbour in BattleTileRangeCalculator.GetNeighbours(current))
            {
                if (neighbour == null || distances.ContainsKey(neighbour))
                    continue;
                distances[neighbour] = distance + 1;
                queue.Enqueue(neighbour);
            }
        }

        RangeVisibilityChanged?.Invoke(true);
    }

    private bool RequiresExternalTarget()
    {
        BattleCardTargetType targetType = TargetType;
        return targetType == BattleCardTargetType.Enemy ||
               targetType == BattleCardTargetType.Character ||
               targetType == BattleCardTargetType.Tile;
    }

    private bool IsWhirlwindCard()
    {
        return pendingUse != null && pendingUse.CardData != null &&
               pendingUse.CardData.targetType == BattleCardTargetType.Self &&
               pendingUse.CardData.areaType == BattleCardAreaType.Cross &&
               BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Damage) &&
               BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Push);
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

    private List<GameObject> CollectWhirlwindTargets(MapInfo playerTile)
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
            if (health != null && !health.IsDead &&
                BattleTileRangeCalculator.GetDistance(playerTile, enemyTile, 1) == 1)
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

    private void CancelAll()
    {
        ClearStateAndRange();
        Cancelled?.Invoke();
    }

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

    /// <summary>현재 선택과 돌진 후 예상 위치를 기준으로 Confirm 전 밀치기 결과를 표시한다.</summary>
    private void RefreshPushPreview()
    {
        if (pushPreviewView == null || pendingUse == null || selectedTarget == null ||
            !BattleCardEffectExecutor.HasEffect(pendingUse.CardData, BattleCardEffectType.Push))
        {
            pushPreviewView?.HidePushPredictions();
            return;
        }

        if (IsWhirlwindCard())
        {
            MapInfo playerTile = findClosestTile != null ? findClosestTile(player.transform.position) : null;
            List<BattleCardMovementService.PushPlan> plans = new List<BattleCardMovementService.PushPlan>();
            foreach (GameObject target in CollectWhirlwindTargets(playerTile))
            {
                if (BattleCardMovementService.TryCreatePushPlan(
                        player,
                        target,
                        BattleCardEffectExecutor.GetMaximumDistance(pendingUse.CardData, BattleCardEffectType.Push),
                        BattleCardEffectExecutor.GetMaximumPushForce(pendingUse.CardData),
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
/// 카드 효과 목록에서 현재 구현된 단순 효과를 검사하고 실행합니다.
/// 첫 MVP에서는 회복만 담당하며 보호막과 밀치기는 후속 단계에서 같은 진입점에 추가합니다.
/// </summary>
internal static class BattleCardEffectExecutor
{
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
