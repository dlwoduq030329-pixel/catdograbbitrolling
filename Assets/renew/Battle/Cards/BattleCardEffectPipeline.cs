using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 데이터의 effects를 순서대로 해석한다. 카드 번호나 카드 이름을 알지 못하며,
/// 대상 해석과 사전 검증이 끝난 뒤에만 실제 효과를 실행한다.
/// </summary>
internal static class BattleCardEffectPipeline
{
    /// <summary>
    /// 카드 한 번을 준비·실행하는 데 필요한 입력과 외부 기능을 묶는다.
    /// 현재 이름과 callback 의존성이 추상적이므로 추후 CardEffectExecutionInput과 직접 참조 구조로 정리한다.
    /// </summary>
    internal sealed class Context
    {
        public GameObject Player;
        public GameObject SelectedTarget;
        public MapInfo SelectedTile;
        public BattleCardData Card;
        public int CardIndex;
        public BattleActionRequest Request;
        public Func<Vector3, MapInfo> FindClosestTile;
        public Action<GameObject, BattleStatusType, int> ApplyStatus;
        public BattleCardDrawSystem DrawSystem;
        public SelectedCardUseInfo ConsumedCardUse;
        public BattleRangeVisualizer RangeVisualizer;
        public Color PersistentAreaColor;
    }

    /// <summary>Confirm 전에 검증과 대상 계산을 마친 실행 가능한 효과 단계 목록.</summary>
    internal sealed class PreparedUse
    {
        internal readonly List<PreparedEffect> Effects = new List<PreparedEffect>();
    }

    /// <summary>
    /// 효과 데이터 한 단계와 확정 대상, 이동·소환 계획을 함께 보관한다.
    /// Execute는 이 사전 계산 결과를 사용하므로 Preview와 실제 실행의 판정이 달라지는 것을 막는다.
    /// </summary>
    internal sealed class PreparedEffect
    {
        internal BattleCardEffectData Data;
        internal List<GameObject> Targets;
        internal BattleCardMovementService.MovementPlan MovementPlan;
        internal BattleScarecrowBridge.Plan ScarecrowPlan;
        internal bool IsLegacyFallback;
    }

    /// <summary>
    /// 카드 효과를 목록 순서대로 읽어 대상과 이동 계획을 미리 계산한다.
    /// 실패한 단계가 cancelCardOnFailure이면 카드 전체를 거부하고, 아니면 해당 단계만 건너뛴다.
    /// 성공한 경우에만 Execute에 전달할 PreparedUse를 반환하며 이 단계에서는 HP·위치·상태를 변경하지 않는다.
    /// </summary>
    internal static bool TryPrepare(Context context, out PreparedUse prepared, out string failureReason)
    {
        prepared = new PreparedUse();
        failureReason = string.Empty;
        if (context == null || context.Player == null || context.Card == null)
        {
            failureReason = "카드 실행 정보가 올바르지 않습니다.";
            return false;
        }

        List<BattleCardEffectData> effects = BuildEffectiveList(context);
        List<GameObject> previousTargets = new List<GameObject>();
        foreach (BattleCardEffectData effect in effects)
        {
            if (effect == null) continue;

            List<GameObject> targets = ResolveTargets(context, effect, previousTargets);
            PreparedEffect step = new PreparedEffect { Data = effect, Targets = targets };
            bool valid = ValidateAndPrepareStep(context, step, out string stepFailure);
            if (!valid)
            {
                if (effect.cancelCardOnFailure)
                {
                    failureReason = stepFailure;
                    return false;
                }

                Debug.LogWarning($"카드 효과 건너뜀: {stepFailure}", context.Player);
                continue;
            }

            prepared.Effects.Add(step);
            previousTargets = new List<GameObject>(targets);
        }

        if (prepared.Effects.Count == 0)
        {
            failureReason = "실행할 수 있는 카드 효과가 없습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Confirm이 끝난 카드의 준비된 효과를 데이터 목록 순서대로 실제 적용한다.
    /// 이동, 피해, 회복, 상태, 소환을 여기서 분기하고 마지막에 Legacy 표현 브릿지로 VFX를 요청한다.
    /// </summary>
    internal static void Execute(Context context, PreparedUse prepared)
    {
        foreach (PreparedEffect step in prepared.Effects)
        {
            BattleCardEffectData effect = step.Data;
            int repeat = Mathf.Max(1, effect.repeatCount);
            switch (effect.effectType)
            {
                case BattleCardEffectType.Dash:
                case BattleCardEffectType.Teleport:
                    BattleCardMovementService.ApplyMovement(context.Player, step.MovementPlan);
                    break;

                case BattleCardEffectType.Damage:
                    if (IsCode(effect, "CHAIN", "연쇄"))
                        ExecuteChainDamage(context, step.Targets, ResolveDamageAmount(context, effect));
                    else
                        ExecuteDamage(
                            context, step.Targets, ResolveDamageAmount(context, effect), repeat,
                            IsCode(effect, "HOLY", "신성"));
                    break;

                case BattleCardEffectType.Heal:
                    foreach (GameObject target in step.Targets)
                        for (int i = 0; i < repeat; i++) target.GetComponent<BattleHealth>()?.Heal(effect.amount);
                    break;

                case BattleCardEffectType.Shield:
                    foreach (GameObject target in step.Targets)
                        for (int i = 0; i < repeat; i++) target.GetComponent<BattleHealth>()?.AddShield(effect.amount);
                    break;

                case BattleCardEffectType.Push:
                    foreach (GameObject target in step.Targets)
                    {
                        if (!IsLiving(target)) continue;
                        for (int i = 0; i < repeat; i++)
                            BattleCardMovementService.TryPush(
                                context.Player, target, Mathf.Max(1, effect.distanceTiles),
                                Mathf.Max(1, effect.pushForce), out _);
                    }
                    break;

                case BattleCardEffectType.ApplyStatus:
                    if (BattleStatusEffectCodes.TryParse(effect.effectCode, out BattleStatusType status))
                    {
                        foreach (GameObject target in step.Targets)
                            context.ApplyStatus?.Invoke(target, status, Mathf.Max(1, effect.durationTurns));
                    }
                    break;

                case BattleCardEffectType.Summon:
                    BattleScarecrowBridge.Execute(context.Player, step.ScarecrowPlan);
                    break;

                case BattleCardEffectType.CreateArea:
                    BattleHealingArea.Create(
                        context.Player,
                        context.SelectedTile,
                        context.FindClosestTile,
                        context.Card.areaSizeTiles,
                        effect.amount,
                        effect.durationTurns,
                        context.RangeVisualizer,
                        context.PersistentAreaColor);
                    break;

                case BattleCardEffectType.Execute:
                    foreach (GameObject target in step.Targets)
                    {
                        BattleHealth health = target != null ? target.GetComponent<BattleHealth>() : null;
                        if (health == null || health.IsDead) continue;
                        BattleEnemyRuntimeData enemyData = target.GetComponent<BattleEnemyRuntimeData>();
                        BattleEnemyRank rank = enemyData != null && enemyData.Data != null
                            ? enemyData.Data.rank : BattleEnemyRank.Normal;
                        bool protectedRank = rank == BattleEnemyRank.Elite || rank == BattleEnemyRank.Boss;
                        float threshold = Mathf.Clamp01(effect.amount / 100f);
                        if (!protectedRank && health.CurrentHealth / health.MaxHealth < threshold)
                            health.TakeDamage(health.CurrentHealth + health.CurrentShield);
                        else if (protectedRank)
                            BattleDamageService.TryApplyDamage(
                                context.Player, target, effect.secondaryAmount,
                                BattleDamageType.Physical, out _);
                    }
                    break;

                case BattleCardEffectType.ModifyStat:
                    break;

                case BattleCardEffectType.DrawRandomCard:
                    context.DrawSystem?.GenerateWeirdMushroomCard(context.ConsumedCardUse);
                    break;

                case BattleCardEffectType.IncreaseBasicAttackDamage:
                    BattleComponentResolver.GetOrAdd<BattleBasicAttackBuff>(context.Player, null)
                        .Add(effect.amount);
                    break;

                case BattleCardEffectType.Cleanse:
                    foreach (GameObject target in step.Targets)
                        target?.GetComponent<BattleStatusEffects>()?.ClearAllNegativeStatuses();
                    break;
            }
        }

        BattleLegacyCardPresentationBridge.Play(
            context.Player,
            context.SelectedTarget,
            context.SelectedTile,
            BattleLegacyCardPresentationBridge.ResolveCardCode(context.CardIndex),
            context.Card.category);
    }

    /// <summary>
    /// 새 effects 목록이 있으면 그대로 사용하고, 비어 있으면 기존 카드의 category와 Request.Power로 임시 효과 한 개를 만든다.
    /// 이 fallback은 이전 카드 데이터 호환용이며 데이터 이전 완료 후 삭제 대상이다.
    /// </summary>
    private static List<BattleCardEffectData> BuildEffectiveList(Context context)
    {
        if (context.Card.effects != null && context.Card.effects.Count > 0)
            return context.Card.effects;

        // 아직 effects가 입력되지 않은 기존 카드도 이전 동작을 잃지 않게 한 단계로 변환한다.
        BattleCardEffectData fallback = new BattleCardEffectData
        {
            effectTarget = context.Card.targetType == BattleCardTargetType.Self
                ? BattleCardEffectTarget.Self
                : BattleCardEffectTarget.SelectedTarget,
            repeatCount = 1,
            cancelCardOnFailure = true
        };
        if (context.Card.category == BattleCardCategory.Attack)
        {
            fallback.effectType = BattleCardEffectType.Damage;
            fallback.amount = context.Request != null ? context.Request.Power : 0f;
        }
        else
        {
            fallback.effectType = BattleCardEffectType.Heal;
            fallback.amount = context.Request != null ? context.Request.Power : 0f;
        }
        return new List<BattleCardEffectData> { fallback };
    }

    /// <summary>
    /// 효과 종류별 필수 대상·수치·이동 가능 여부를 검사하고 필요한 Movement/Summon 계획을 저장한다.
    /// false이면 failure에 사용자가 이해할 수 있는 실패 이유를 기록한다.
    /// </summary>
    private static bool ValidateAndPrepareStep(Context context, PreparedEffect step, out string failure)
    {
        failure = string.Empty;
        BattleCardEffectData effect = step.Data;
        switch (effect.effectType)
        {
            case BattleCardEffectType.Dash:
                return BattleCardMovementService.TryCreateDashPlan(
                    context.Player, context.SelectedTarget, Mathf.Max(0, effect.distanceTiles),
                    out step.MovementPlan, out failure);
            case BattleCardEffectType.Teleport:
                return BattleCardMovementService.TryCreateTeleportPlan(
                    context.Player, context.SelectedTarget, out step.MovementPlan, out failure);
            case BattleCardEffectType.Damage:
                return ValidateHealthTargets(
                    step.Targets, ResolveDamageAmount(context, effect), false, out failure);
            case BattleCardEffectType.Heal:
                return ValidateHealthTargets(step.Targets, effect.amount, true, out failure);
            case BattleCardEffectType.Shield:
                return ValidateHealthTargets(step.Targets, effect.amount, false, out failure);
            case BattleCardEffectType.Push:
                if (step.Targets.Count == 0) { failure = "밀칠 대상이 없습니다."; return false; }
                return true;
            case BattleCardEffectType.ApplyStatus:
                if (!BattleStatusEffectCodes.TryParse(effect.effectCode, out _))
                { failure = $"알 수 없는 상태이상 코드: {effect.effectCode}"; return false; }
                if (step.Targets.Count == 0) { failure = "상태이상을 적용할 대상이 없습니다."; return false; }
                return true;
            case BattleCardEffectType.Summon:
                if (!string.Equals(effect.effectCode, "SCARECROW", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(effect.effectCode, "허수아비", StringComparison.Ordinal))
                { failure = $"알 수 없는 소환 코드: {effect.effectCode}"; return false; }
                return BattleScarecrowBridge.TryCreatePlan(
                    context.Player, context.SelectedTile, out step.ScarecrowPlan, out failure);
            case BattleCardEffectType.CreateArea:
                if (context.SelectedTile == null)
                { failure = "지속 영역을 생성할 타일이 없습니다."; return false; }
                if (effect.amount <= 0f || effect.durationTurns <= 0)
                { failure = "지속 영역의 회복량과 지속 턴이 필요합니다."; return false; }
                return true;
            case BattleCardEffectType.Execute:
                foreach (GameObject target in step.Targets)
                {
                    BattleHealth health = target != null ? target.GetComponent<BattleHealth>() : null;
                    if (health == null || health.IsDead || effect.amount <= 0f ||
                        effect.secondaryAmount <= 0f) continue;
                    BattleEnemyRuntimeData enemyData = target.GetComponent<BattleEnemyRuntimeData>();
                    BattleEnemyRank rank = enemyData != null && enemyData.Data != null
                        ? enemyData.Data.rank : BattleEnemyRank.Normal;
                    if (rank == BattleEnemyRank.Elite || rank == BattleEnemyRank.Boss ||
                        health.CurrentHealth / health.MaxHealth < Mathf.Clamp01(effect.amount / 100f))
                        return true;
                }
                failure = $"일반 적의 현재 HP가 최대 HP의 {effect.amount:0.#}% 미만이어야 합니다.";
                return false;
            case BattleCardEffectType.ModifyStat:
                failure = $"아직 실행기가 연결되지 않은 능력치 효과입니다: {effect.effectCode}";
                return false;
            case BattleCardEffectType.DrawRandomCard:
                if (context.DrawSystem == null || context.ConsumedCardUse == null)
                {
                    failure = "카드 드로우 시스템이 연결되지 않았습니다.";
                    return false;
                }
                return true;
            case BattleCardEffectType.IncreaseBasicAttackDamage:
                if (effect.amount <= 0f)
                {
                    failure = "기본 공격 피해 증가량이 0 이하입니다.";
                    return false;
                }
                return true;
            case BattleCardEffectType.Cleanse:
                if (step.Targets.Count == 0) { failure = "상태이상을 제거할 대상이 없습니다."; return false; }
                return true;
            default:
                failure = $"아직 실행기가 연결되지 않은 효과입니다: {effect.effectType}";
                return false;
        }
    }

    private static float ResolveDamageAmount(Context context, BattleCardEffectData effect)
    {
        if (!IsCode(effect, "BASIC_ATTACK", "기본공격")) return effect.amount;
        PlayerCombatData combat = context.Player.GetComponent<PlayerCombatData>();
        BattleBasicAttackBuff buff = context.Player.GetComponent<BattleBasicAttackBuff>();
        return (combat != null ? combat.BasicAttackPower : 0f) +
               (buff != null ? buff.BonusDamage : 0f);
    }

    private static bool ValidateHealthTargets(
        List<GameObject> targets, float amount, bool mustBeInjured, out string failure)
    {
        failure = string.Empty;
        if (amount <= 0f) { failure = "효과 수치가 0 이하입니다."; return false; }
        foreach (GameObject target in targets)
        {
            BattleHealth health = target != null ? target.GetComponent<BattleHealth>() : null;
            if (health != null && !health.IsDead && (!mustBeInjured || health.CurrentHealth < health.MaxHealth))
                return true;
        }
        failure = mustBeInjured ? "회복 가능한 대상이 없습니다." : "유효한 대상이 없습니다.";
        return false;
    }

    private static List<GameObject> ResolveTargets(
        Context context, BattleCardEffectData effect, List<GameObject> previousTargets)
    {
        if (effect.effectType == BattleCardEffectType.Damage &&
            IsCode(effect, "CHAIN", "연쇄"))
        {
            return CollectChainTargets(context, previousTargets, Mathf.Max(1, effect.repeatCount));
        }

        switch (effect.effectTarget)
        {
            case BattleCardEffectTarget.Self:
                return new List<GameObject> { context.Player };
            case BattleCardEffectTarget.SelectedTarget:
                return context.SelectedTarget != null
                    ? new List<GameObject> { context.SelectedTarget }
                    : new List<GameObject>();
            case BattleCardEffectTarget.SelectedTile:
                return context.SelectedTile != null
                    ? new List<GameObject> { context.SelectedTile.gameObject }
                    : new List<GameObject>();
            case BattleCardEffectTarget.PreviousEffectTargets:
                return FilterLivingOrSelf(previousTargets, context.Player);
            case BattleCardEffectTarget.AllEnemies:
                return CollectAllEnemies(context.Player.transform.position);
            case BattleCardEffectTarget.TargetsInArea:
                return CollectEnemiesInArea(context);
            default:
                return new List<GameObject>();
        }
    }

    private static List<GameObject> CollectAllEnemies(Vector3 sortCenter)
    {
        List<GameObject> targets = new List<GameObject>();
        foreach (EnemyTurnActor enemy in UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None))
            if (enemy != null && IsLiving(enemy.gameObject)) targets.Add(enemy.gameObject);
        SortClockwise(targets, sortCenter);
        return targets;
    }

    private static List<GameObject> CollectEnemiesInArea(Context context)
    {
        MapInfo center = context.Card.targetType == BattleCardTargetType.Self || context.SelectedTile == null
            ? context.FindClosestTile(context.Player.transform.position)
            : context.SelectedTile;
        List<GameObject> targets = new List<GameObject>();
        int size = Mathf.Max(1, context.Card.areaSizeTiles);
        foreach (EnemyTurnActor enemy in UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None))
        {
            if (enemy == null || !IsLiving(enemy.gameObject)) continue;
            MapInfo tile = context.FindClosestTile(enemy.transform.position);
            if (IsTileInArea(context, center, tile, size)) targets.Add(enemy.gameObject);
        }
        SortClockwise(targets, center != null ? center.transform.position : context.Player.transform.position);
        return targets;
    }

    private static bool IsTileInArea(Context context, MapInfo center, MapInfo tile, int size)
    {
        if (center == null || tile == null) return false;
        int distance = BattleTileRangeCalculator.GetDistance(center, tile, size);
        if (distance < 0 || distance > size) return false;
        if (context.Card.areaType != BattleCardAreaType.Line) return true;

        MapInfo origin = context.FindClosestTile(context.Player.transform.position);
        if (origin == null) return false;
        Vector2Int direction = center.Index - origin.Index;
        Vector2Int offset = tile.Index - center.Index;
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)) return offset.y == 0;
        return offset.x == 0;
    }

    private static List<GameObject> CollectChainTargets(
        Context context, List<GameObject> previousTargets, int maximumTargets)
    {
        List<GameObject> candidates = CollectAllEnemies(context.Player.transform.position);
        foreach (GameObject previous in previousTargets) candidates.Remove(previous);
        if (previousTargets.Count == 0 || previousTargets[0] == null) return new List<GameObject>();

        MapInfo origin = context.FindClosestTile(previousTargets[0].transform.position);
        candidates.Sort((left, right) =>
        {
            int leftDistance = BattleTileRangeCalculator.GetDistance(
                origin, context.FindClosestTile(left.transform.position), int.MaxValue);
            int rightDistance = BattleTileRangeCalculator.GetDistance(
                origin, context.FindClosestTile(right.transform.position), int.MaxValue);
            if (leftDistance < 0) leftDistance = int.MaxValue;
            if (rightDistance < 0) rightDistance = int.MaxValue;
            return leftDistance.CompareTo(rightDistance);
        });
        if (candidates.Count > maximumTargets)
            candidates.RemoveRange(maximumTargets, candidates.Count - maximumTargets);
        return candidates;
    }

    private static void ExecuteDamage(
        Context context, List<GameObject> targets, float amount, int repeat, bool forceMagic)
    {
        BattleDamageType type = forceMagic || context.Card.cardType == BattleCardType.MagicDamage
            ? BattleDamageType.Magic : BattleDamageType.Physical;
        foreach (GameObject target in targets)
        {
            for (int i = 0; i < repeat && IsLiving(target); i++)
                BattleDamageService.TryApplyDamage(context.Player, target, amount, type, out _);
        }
        if (targets.Count > 0 && targets[0] != null)
            BattleUnitMotionAnimator.FaceTowards(context.Player.transform, targets[0].transform.position);
    }

    private static void ExecuteChainDamage(Context context, List<GameObject> targets, float amount)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            float attenuated = Mathf.Max(0f, amount - (0.5f * (i + 1)));
            if (attenuated <= 0f) break;
            BattleDamageService.TryApplyDamage(
                context.Player, targets[i], attenuated, BattleDamageType.Magic, out _);
        }
    }

    private static List<GameObject> FilterLivingOrSelf(List<GameObject> source, GameObject player)
    {
        List<GameObject> result = new List<GameObject>();
        foreach (GameObject target in source)
            if (target == player || IsLiving(target)) result.Add(target);
        return result;
    }

    private static bool IsLiving(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;
        BattleHealth health = target.GetComponent<BattleHealth>();
        return health != null && !health.IsDead;
    }

    private static void SortClockwise(List<GameObject> targets, Vector3 center)
    {
        targets.Sort((left, right) => ClockwiseAngle(center, left.transform.position)
            .CompareTo(ClockwiseAngle(center, right.transform.position)));
    }

    private static float ClockwiseAngle(Vector3 center, Vector3 target)
    {
        Vector3 offset = target - center;
        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        return angle < 0f ? angle + 360f : angle;
    }

    private static bool IsCode(BattleCardEffectData effect, string asciiCode, string koreanCode)
    {
        return effect != null &&
               (string.Equals(effect.effectCode, asciiCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(effect.effectCode, koreanCode, StringComparison.Ordinal));
    }
}
