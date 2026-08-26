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
        /// <summary>카드 이름, 사거리, 비용과 기본 위력이 담긴 공통 행동 정보.</summary>
        public BattleActionRequest ActionInfo;
        /// <summary>월드 위치에서 가장 가까운 전투 타일을 찾는 함수.</summary>
        public Func<Vector3, MapInfo> FindNearestTileAtPosition;
        /// <summary>확정 대상에게 지정한 종류와 턴 수의 상태이상을 적용하는 함수.</summary>
        public Action<GameObject, BattleStatusType, int> ApplyStatusToTarget;
        /// <summary>임시 카드 생성 효과가 현재 손패를 변경할 때 사용하는 드로우 시스템.</summary>
        public BattleCardDrawSystem CardDrawSystem;
        /// <summary>방금 사용한 카드의 슬롯과 데이터. 버섯 임시 카드 생성 위치에 사용한다.</summary>
        public SelectedCardUseInfo UsedCardInfo;
        /// <summary>지속 영역 효과의 타일 표시를 등록하고 해제하는 시각화 컴포넌트.</summary>
        public BattleRangeVisualizer PersistentAreaVisualizer;
        /// <summary>성역화 같은 지속 영역 타일에 적용할 표시 색상.</summary>
        public Color PersistentAreaTileColor;
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
        /// <summary>카드의 effects 목록에서 읽은 현재 효과 한 단계의 원본 설정.</summary>
        internal BattleCardEffectData OriginalEffectData;
        /// <summary>준비 단계에서 검증을 마치고 실제 효과를 받을 대상으로 확정된 목록.</summary>
        internal List<GameObject> ConfirmedTargets;
        /// <summary>Dash 또는 Teleport 실행 전에 계산해 둔 이동 계획.</summary>
        internal BattleCardMovementService.MovementPlan PreparedMovement;
        /// <summary>허수아비 생성 전에 위치와 생성 가능 여부를 계산해 둔 소환 계획.</summary>
        internal BattleScarecrowBridge.ScarecrowSpawnPlan ScarecrowSpawnPlan;
    }

    /// <summary>
    /// 카드 효과를 목록 순서대로 읽어 대상과 이동 계획을 미리 계산한다.
    /// 실패한 단계가 cancelCardOnFailure이면 카드 전체를 거부하고, 아니면 해당 단계만 건너뛴다.
    /// 성공한 경우에만 Execute에 전달할 PreparedUse를 반환하며 이 단계에서는 HP·위치·상태를 변경하지 않는다.
    /// </summary>
    internal static bool TryPrepareCardEffects(
        Context context,
        out PreparedUse preparedCardEffects,
        out string failureReason)
    {
        // 실패하더라도 호출자가 이전 카드의 준비 결과를 재사용하지 않도록 새 결과 목록으로 시작한다.
        preparedCardEffects = new PreparedUse();
        failureReason = string.Empty;

        // 효과 준비에 반드시 필요한 실행 정보, 사용자, 카드 데이터가 없으면 대상과 효과를 계산할 수 없다.
        if (context == null || context.Player == null || context.Card == null)
        {
            failureReason = "카드 실행 정보가 올바르지 않습니다.";
            return false;
        }

        // 효과 데이터가 없는 카드를 임의 Damage/Heal로 바꾸지 않는다.
        // 미구현 카드는 잘못된 효과로 실행시키는 대신 카드 번호와 함께 명확히 거부한다.
        if (context.Card.effects == null || context.Card.effects.Count == 0)
        {
            failureReason = $"카드 {context.CardIndex}에 실행할 효과 데이터가 없습니다.";
            Debug.LogError(failureReason, context.Player);
            return false;
        }

        // 카드 데이터에 명시된 효과 목록만 등록 순서대로 준비한다.
        List<BattleCardEffectData> effectDataList = context.Card.effects;

        // PreviousEffectTargets 규칙을 사용하는 다음 효과에 직전 효과의 대상 목록을 전달하기 위한 저장소다.
        // 첫 효과 전에는 이전 대상이 없으므로 빈 목록으로 시작한다.
        List<GameObject> previousEffectTargets = new List<GameObject>();

        // 카드 데이터에 등록된 순서가 실제 효과 실행 순서이므로 같은 순서로 한 단계씩 준비한다.
        foreach (BattleCardEffectData effectData in effectDataList)
        {
            // effects 목록 안의 빈 항목은 실행할 정보가 없으므로 안전하게 건너뛴다.
            if (effectData == null)
            {
                continue;
            }

            // 효과의 대상 규칙(Self, SelectedTarget, PreviousEffectTargets 등)을 실제 GameObject 목록으로 변환한다.
            List<GameObject> confirmedTargets = ResolveTargets(
                context,
                effectData,
                previousEffectTargets);

            // 원본 효과 설정과 확정 대상을 한 단계로 묶는다.
            // 이동·소환 효과라면 아래 검증 함수가 사전 계산 계획도 이 객체에 추가한다.
            PreparedEffect preparedEffect = new PreparedEffect
            {
                OriginalEffectData = effectData,
                ConfirmedTargets = confirmedTargets
            };

            // 실제 HP·위치·상태를 바꾸지 않고 대상, 수치, 이동과 소환 가능 여부만 검사한다.
            bool canExecuteEffect = TryPrepareSingleCardEffect(
                context,
                preparedEffect,
                out string effectFailureReason);
            if (!canExecuteEffect)
            {
                // 이 효과가 필수 단계라면 한 단계의 실패로 카드 전체 사용을 취소한다.
                if (effectData.cancelCardOnFailure)
                {
                    failureReason = effectFailureReason;
                    return false;
                }

                // 선택 효과라면 카드 전체를 막지 않고 실패한 단계만 실행 목록에서 제외한다.
                Debug.LogWarning($"카드 효과 건너뜀: {effectFailureReason}", context.Player);
                continue;
            }

            // 검증을 통과한 단계만 Execute가 사용할 최종 실행 목록에 등록한다.
            preparedCardEffects.Effects.Add(preparedEffect);

            // 다음 효과가 PreviousEffectTargets를 요청할 수 있으므로 이번 확정 대상의 사본을 보관한다.
            // 새 List로 복사해 이후 대상 목록 변경이 이미 준비된 효과에 영향을 주지 않게 한다.
            previousEffectTargets = new List<GameObject>(confirmedTargets);
        }

        // 모든 효과가 null이거나 선택 효과 검증에 실패했다면 카드를 소비할 실제 작업이 없다.
        if (preparedCardEffects.Effects.Count == 0)
        {
            failureReason = "실행할 수 있는 카드 효과가 없습니다.";
            return false;
        }

        // 하나 이상의 효과가 대상과 사전 검증을 통과했으므로 실제 실행 단계로 넘길 수 있다.
        return true;
    }

    /// <summary>
    /// Confirm이 끝난 카드의 준비된 효과를 데이터 목록 순서대로 실제 적용한다.
    /// 이동, 피해, 회복, 상태, 소환을 여기서 분기하고 마지막에 Legacy 표현 브릿지로 VFX를 요청한다.
    /// </summary>
    internal static void ApplyPreparedCardEffects(Context context, PreparedUse preparedCardEffects)
    {
        // TryPrepareCardEffects가 검증한 순서를 그대로 사용해야 복합 카드의 효과 순서가 바뀌지 않는다.
        foreach (PreparedEffect preparedEffect in preparedCardEffects.Effects)
        {
            BattleCardEffectData effectData = preparedEffect.OriginalEffectData;
            // 데이터가 0이나 음수여도 효과가 완전히 사라지지 않도록 최소 한 번 실행한다.
            int repeatCount = Mathf.Max(1, effectData.repeatCount);

            // switch는 effectType과 실제 실행 코드를 직접 연결하는 분배기다.
            // Dictionary/Delegate 등록보다 각 효과의 실행 위치를 한눈에 추적하기 쉬우므로 유지한다.
            switch (effectData.effectType)
            {
                case BattleCardEffectType.Dash:
                case BattleCardEffectType.Teleport:
                    BattleCardMovementService.ApplyMovement(context.Player, preparedEffect.PreparedMovement);
                    break;

                case BattleCardEffectType.Damage:
                    if (IsCode(effectData, "CHAIN", "연쇄"))
                        ApplyChainDamageWithFalloff(
                            context,
                            preparedEffect.ConfirmedTargets,
                            ResolveDamageAmount(context, effectData));
                    else
                        ApplyRepeatedDamageEffect(
                            context,
                            preparedEffect.ConfirmedTargets,
                            ResolveDamageAmount(context, effectData),
                            repeatCount,
                            IsCode(effectData, "HOLY", "신성"));
                    break;

                case BattleCardEffectType.Heal:
                    foreach (GameObject target in preparedEffect.ConfirmedTargets)
                        for (int i = 0; i < repeatCount; i++) target.GetComponent<BattleHealth>()?.Heal(effectData.amount);
                    break;

                case BattleCardEffectType.Shield:
                    foreach (GameObject target in preparedEffect.ConfirmedTargets)
                        for (int i = 0; i < repeatCount; i++) target.GetComponent<BattleHealth>()?.AddShield(effectData.amount);
                    break;

                case BattleCardEffectType.Push:
                    foreach (GameObject target in preparedEffect.ConfirmedTargets)
                    {
                        if (!IsLiving(target)) continue;
                        for (int i = 0; i < repeatCount; i++)
                            BattleCardMovementService.TryPush(
                                context.Player, target, Mathf.Max(1, effectData.distanceTiles),
                                Mathf.Max(1, effectData.pushForce), out _);
                    }
                    break;

                case BattleCardEffectType.ApplyStatus:
                    if (BattleStatusEffectCodes.TryParse(effectData.effectCode, out BattleStatusType status))
                    {
                        foreach (GameObject target in preparedEffect.ConfirmedTargets)
                            context.ApplyStatusToTarget?.Invoke(target, status, Mathf.Max(1, effectData.durationTurns));
                    }
                    break;

                case BattleCardEffectType.Summon:
                    BattleScarecrowBridge.ApplyScarecrowSpawnPlan(context.Player, preparedEffect.ScarecrowSpawnPlan);
                    break;

                case BattleCardEffectType.CreateArea:
                    BattleHealingArea.Create(
                        context.Player,
                        context.SelectedTile,
                        context.FindNearestTileAtPosition,
                        context.Card.areaSizeTiles,
                        effectData.amount,
                        effectData.durationTurns,
                        context.PersistentAreaVisualizer,
                        context.PersistentAreaTileColor);
                    break;

                case BattleCardEffectType.Execute:
                    ApplyExecutionEffect(context, preparedEffect.ConfirmedTargets, effectData);
                    break;

                case BattleCardEffectType.ModifyStat:
                    break;

                case BattleCardEffectType.DrawRandomCard:
                    context.CardDrawSystem?.GenerateWeirdMushroomCard(context.UsedCardInfo);
                    break;

                case BattleCardEffectType.IncreaseBasicAttackDamage:
                    BattleComponentResolver.GetOrAdd<BattleBasicAttackBuff>(context.Player, null)
                        .Add(effectData.amount);
                    break;

                case BattleCardEffectType.Cleanse:
                    foreach (GameObject target in preparedEffect.ConfirmedTargets)
                        target?.GetComponent<BattleStatusEffects>()?.ClearAllNegativeStatuses();
                    break;
            }
        }

        BattleLegacyCardPresentationBridge.Play(
            context.Player,
            context.SelectedTarget,
            context.SelectedTile,
            context.Card);
    }

    /// <summary>
    /// 카드의 효과 한 단계를 실제로 소비·적용하기 전에 사용할 수 있는 상태로 준비한다.
    /// 피해·회복·상태 효과는 대상과 수치가 유효한지 확인하고,
    /// Dash·Teleport·Summon은 실행 시 판정이 달라지지 않도록 이동/소환 계획까지 미리 저장한다.
    /// 이 함수는 HP, 위치, 상태를 직접 변경하지 않는다.
    /// false이면 <paramref name="failureReason"/>에 카드 사용을 거부한 이유를 기록한다.
    /// </summary>
    private static bool TryPrepareSingleCardEffect(
        Context context,
        PreparedEffect preparedEffect,
        out string failureReason)
    {
        failureReason = string.Empty;
        BattleCardEffectData effectData = preparedEffect.OriginalEffectData;

        // 효과 종류별로 필요한 조건이 다르므로 한곳에서 분기한다.
        // 이 switch를 보면 각 EffectType이 어떤 준비 작업을 요구하는지 바로 추적할 수 있다.
        switch (effectData.effectType)
        {
            case BattleCardEffectType.Dash:
                return BattleCardMovementService.TryCreateDashPlan(
                    context.Player, context.SelectedTarget, Mathf.Max(0, effectData.distanceTiles),
                    out preparedEffect.PreparedMovement, out failureReason);
            case BattleCardEffectType.Teleport:
                return BattleCardMovementService.TryCreateTeleportPlan(
                    context.Player, context.SelectedTarget,
                    out preparedEffect.PreparedMovement, out failureReason);
            case BattleCardEffectType.Damage:
                return TryFindTargetThatCanReceiveHealthEffect(
                    preparedEffect.ConfirmedTargets,
                    ResolveDamageAmount(context, effectData), false, out failureReason);
            case BattleCardEffectType.Heal:
                return TryFindTargetThatCanReceiveHealthEffect(
                    preparedEffect.ConfirmedTargets, effectData.amount, true, out failureReason);
            case BattleCardEffectType.Shield:
                return TryFindTargetThatCanReceiveHealthEffect(
                    preparedEffect.ConfirmedTargets, effectData.amount, false, out failureReason);
            case BattleCardEffectType.Push:
                if (preparedEffect.ConfirmedTargets.Count == 0)
                { failureReason = "밀칠 대상이 없습니다."; return false; }
                return true;
            case BattleCardEffectType.ApplyStatus:
                if (!BattleStatusEffectCodes.TryParse(effectData.effectCode, out _))
                { failureReason = $"알 수 없는 상태이상 코드: {effectData.effectCode}"; return false; }
                if (preparedEffect.ConfirmedTargets.Count == 0)
                { failureReason = "상태이상을 적용할 대상이 없습니다."; return false; }
                return true;
            case BattleCardEffectType.Summon:
                if (!string.Equals(effectData.effectCode, "SCARECROW", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(effectData.effectCode, "허수아비", StringComparison.Ordinal))
                { failureReason = $"알 수 없는 소환 코드: {effectData.effectCode}"; return false; }
                return BattleScarecrowBridge.TryCreateScarecrowSpawnPlan(
                    context.Player, context.SelectedTile, effectData.summonPrefab,
                    out preparedEffect.ScarecrowSpawnPlan, out failureReason);
            case BattleCardEffectType.CreateArea:
                if (context.SelectedTile == null)
                { failureReason = "지속 영역을 생성할 타일이 없습니다."; return false; }
                if (effectData.amount <= 0f || effectData.durationTurns <= 0)
                { failureReason = "지속 영역의 회복량과 지속 턴이 필요합니다."; return false; }
                return true;
            case BattleCardEffectType.Execute:
                // 일반 적은 HP 비율 조건을 만족해야 처형할 수 있다.
                // 엘리트와 보스는 즉사를 막고 secondaryAmount만큼 피해를 주므로 HP 조건과 무관하게 유효하다.
                foreach (GameObject target in preparedEffect.ConfirmedTargets)
                {
                    BattleHealth targetHealth = target != null
                        ? target.GetComponent<BattleHealth>()
                        : null;
                    if (targetHealth == null || targetHealth.IsDead || effectData.amount <= 0f ||
                        effectData.secondaryAmount <= 0f)
                        continue;

                    BattleEnemyRank targetRank = GetEnemyRankOrNormal(target);
                    bool isProtectedFromInstantExecution =
                        targetRank == BattleEnemyRank.Elite || targetRank == BattleEnemyRank.Boss;
                    float executionHealthRatio = Mathf.Clamp01(effectData.amount / 100f);
                    bool isBelowExecutionHealth =
                        targetHealth.CurrentHealth / targetHealth.MaxHealth < executionHealthRatio;

                    if (isProtectedFromInstantExecution || isBelowExecutionHealth)
                        return true;
                }
                failureReason =
                    $"일반 적의 현재 HP가 최대 HP의 {effectData.amount:0.#}% 미만이어야 합니다.";
                return false;
            case BattleCardEffectType.ModifyStat:
                failureReason =
                    $"아직 실행기가 연결되지 않은 능력치 효과입니다: {effectData.effectCode}";
                return false;
            case BattleCardEffectType.DrawRandomCard:
                if (context.CardDrawSystem == null || context.UsedCardInfo == null)
                {
                    failureReason = "카드 드로우 시스템이 연결되지 않았습니다.";
                    return false;
                }
                return true;
            case BattleCardEffectType.IncreaseBasicAttackDamage:
                if (effectData.amount <= 0f)
                {
                    failureReason = "기본 공격 피해 증가량이 0 이하입니다.";
                    return false;
                }
                return true;
            case BattleCardEffectType.Cleanse:
                if (preparedEffect.ConfirmedTargets.Count == 0)
                { failureReason = "상태이상을 제거할 대상이 없습니다."; return false; }
                return true;
            default:
                failureReason = $"아직 실행기가 연결되지 않은 효과입니다: {effectData.effectType}";
                return false;
        }
    }

    /// <summary>
    /// HP가 낮은 일반 적은 즉시 처형하고, 즉사 면역 대상인 엘리트·보스에는 대체 물리 피해를 준다.
    /// <c>amount</c>는 일반 적의 처형 HP 기준(%), <c>secondaryAmount</c>는 엘리트·보스 피해량이다.
    /// 준비 단계에서 조건을 검사했더라도 복합 효과 앞 단계에서 대상의 HP가 달라질 수 있으므로
    /// 적용 순간의 HP와 적 등급을 다시 읽어 최종 결과를 결정한다.
    /// </summary>
    private static void ApplyExecutionEffect(
        Context context,
        List<GameObject> confirmedTargets,
        BattleCardEffectData executionEffectData)
    {
        // 카드 데이터의 백분율을 0~1 HP 비율로 바꾼다. 예: amount 30 -> 0.3(30%).
        float executionHealthRatio = Mathf.Clamp01(executionEffectData.amount / 100f);

        foreach (GameObject target in confirmedTargets)
        {
            // HP가 없거나 이미 죽은 오브젝트에는 처형 및 대체 피해를 적용하지 않는다.
            BattleHealth targetHealth = target != null
                ? target.GetComponent<BattleHealth>()
                : null;
            if (targetHealth == null || targetHealth.IsDead)
                continue;

            // 등급 데이터가 없는 대상은 일반 적으로 취급한다.
            // 엘리트와 보스는 처형으로 즉사하지 않고 secondaryAmount만큼 물리 피해만 받는다.
            BattleEnemyRank targetRank = GetEnemyRankOrNormal(target);
            bool isProtectedFromInstantExecution =
                targetRank == BattleEnemyRank.Elite || targetRank == BattleEnemyRank.Boss;
            if (isProtectedFromInstantExecution)
            {
                BattleDamageService.TryApplyDamage(
                    context.Player,
                    target,
                    executionEffectData.secondaryAmount,
                    BattleDamageType.Physical,
                    out _);
                continue;
            }

            // 일반 적은 현재 HP 비율이 기준보다 '미만'일 때만 처형된다.
            // 현재 HP와 보호막의 합보다 큰 피해를 주어 BattleHealth의 기존 사망 흐름을 그대로 통과시킨다.
            float currentHealthRatio = targetHealth.CurrentHealth / targetHealth.MaxHealth;
            if (currentHealthRatio < executionHealthRatio)
                targetHealth.TakeDamage(targetHealth.CurrentHealth + targetHealth.CurrentShield);
        }
    }

    /// <summary>적 데이터가 연결되어 있으면 실제 등급을, 없으면 일반 등급을 반환한다.</summary>
    private static BattleEnemyRank GetEnemyRankOrNormal(GameObject target)
    {
        BattleEnemyRuntimeData enemyRuntimeData = target.GetComponent<BattleEnemyRuntimeData>();
        return enemyRuntimeData != null && enemyRuntimeData.Data != null
            ? enemyRuntimeData.Data.rank
            : BattleEnemyRank.Normal;
    }

    /// <summary>
    /// 현재 피해 효과가 실제로 적용할 피해량을 반환한다.
    /// 일반 피해 효과는 카드 데이터의 amount를 그대로 사용한다.
    /// effectCode가 BASIC_ATTACK/기본공격이면 카드의 고정 수치 대신 PlayerCombatData의 평타 공격력과
    /// BattleBasicAttackBuff의 이번 전투 추가 피해를 합쳐 계산한다.
    /// </summary>
    private static float ResolveDamageAmount(Context context, BattleCardEffectData effectData)
    {
        // 기본 공격 연동 효과가 아니면 추가 계산 없이 카드에 기록된 피해량을 사용한다.
        if (!IsCode(effectData, "BASIC_ATTACK", "기본공격"))
            return effectData.amount;

        // 기본 공격 연동 카드는 플레이어의 현재 평타 수치가 바뀌면 카드 피해도 함께 바뀌어야 한다.
        PlayerCombatData playerCombatData = context.Player.GetComponent<PlayerCombatData>();
        BattleBasicAttackBuff basicAttackBuff = context.Player.GetComponent<BattleBasicAttackBuff>();
        float baseAttackDamage = playerCombatData != null
            ? playerCombatData.BasicAttackPower
            : 0f;
        float temporaryBonusDamage = basicAttackBuff != null
            ? basicAttackBuff.BonusDamage
            : 0f;
        return baseAttackDamage + temporaryBonusDamage;
    }

    /// <summary>
    /// 확정 대상 중 피해·회복·보호막 효과를 받을 수 있는 대상이 하나라도 있는지 확인한다.
    /// 회복은 살아 있으면서 현재 HP가 최대 HP보다 낮은 대상만 허용한다.
    /// 피해와 보호막은 살아 있고 BattleHealth를 가진 대상이면 허용한다.
    /// 실제 HP 변경은 하지 않으며 카드 사용 전 유효성만 검사한다.
    /// </summary>
    private static bool TryFindTargetThatCanReceiveHealthEffect(
        List<GameObject> confirmedTargets,
        float effectAmount,
        bool targetMustBeInjured,
        out string failureReason)
    {
        // out 매개변수는 모든 반환 경로에서 값이 필요하므로, 성공 시에는 빈 실패 사유를 반환한다.
        // 카드 데이터가 문자열이라서 검사하는 코드가 아니다.
        failureReason = string.Empty;

        if (effectAmount <= 0f)
        {
            failureReason = "효과 수치가 0 이하입니다.";
            return false;
        }

        foreach (GameObject target in confirmedTargets)
        {
            BattleHealth targetHealth = target != null
                ? target.GetComponent<BattleHealth>()
                : null;

            // HP 컴포넌트가 없거나 이미 사망한 대상은 모든 체력 계열 효과에서 제외한다.
            if (targetHealth == null || targetHealth.IsDead)
                continue;

            // 피해·보호막은 살아 있는 대상이면 통과한다.
            // 회복은 불필요한 카드 소비를 막기 위해 실제로 HP가 부족한 대상만 통과한다.
            bool canReceiveEffect = !targetMustBeInjured ||
                                    targetHealth.CurrentHealth < targetHealth.MaxHealth;
            if (canReceiveEffect)
                return true;
        }

        failureReason = targetMustBeInjured
            ? "회복 가능한 대상이 없습니다."
            : "유효한 대상이 없습니다.";
        return false;
    }

    /// <summary>
    /// 카드 효과 데이터의 effectTarget 규칙을 실제 GameObject 대상 목록으로 변환한다.
    /// TryPrepareCardEffects가 모든 효과 단계마다 한 번 호출하며, 여기서 반환한 목록이
    /// PreparedEffect.ConfirmedTargets에 저장되어 사전 검증과 실제 효과 적용에서 동일하게 사용된다.
    /// 따라서 이 함수는 대상을 공격하거나 회복하지 않고, "누가 효과를 받을지"만 결정한다.
    /// </summary>
    /// <param name="context">
    /// 플레이어, 사용자가 선택한 적·타일, 맵 검색 함수처럼 대상 결정에 필요한 현재 카드 사용 정보.
    /// </param>
    /// <param name="effectData">현재 순서에서 대상을 계산할 카드 효과 한 단계의 원본 데이터.</param>
    /// <param name="previousEffectTargets">
    /// 바로 앞 효과 단계에서 확정된 대상 목록. PreviousEffectTargets 또는 연쇄 효과가 이어받아 사용한다.
    /// 첫 효과 단계에서는 빈 목록이 전달된다.
    /// </param>
    /// <returns>
    /// 현재 효과를 받을 후보 목록. 지정 대상이 없거나 지원하지 않는 규칙이면 빈 목록을 반환하며,
    /// 실제 사용 가능 여부는 이후 TryPrepareSingleCardEffect에서 효과 종류에 맞게 검사한다.
    /// </returns>
    private static List<GameObject> ResolveTargets(
        Context context,
        BattleCardEffectData effectData,
        List<GameObject> previousEffectTargets)
    {
        // 연쇄 피해는 일반 effectTarget보다 고유한 대상 선정 규칙이 우선한다.
        // 직전 효과의 대상을 시작점으로 삼고 repeatCount만큼 다음 적을 거리순으로 연결한다.
        if (effectData.effectType == BattleCardEffectType.Damage &&
            IsCode(effectData, "CHAIN", "연쇄"))
        {
            return FindNearestTargetsForChainDamage(
                context,
                previousEffectTargets,
                Mathf.Max(1, effectData.repeatCount));
        }

        // 일반 효과는 카드 데이터에 기록된 대상 종류를 실제 오브젝트 목록으로 변환한다.
        switch (effectData.effectTarget)
        {
            case BattleCardEffectTarget.Self:
                // 사용자 자신에게 적용되는 회복, 보호막, 정화 등의 효과다.
                return new List<GameObject> { context.Player };

            case BattleCardEffectTarget.SelectedTarget:
                // 카드 사용자가 직접 지정한 적 또는 유닛 하나를 대상으로 한다.
                // 아직 대상을 선택하지 않았다면 빈 목록을 반환해 준비 단계에서 사용을 거부하게 한다.
                return context.SelectedTarget != null
                    ? new List<GameObject> { context.SelectedTarget }
                    : new List<GameObject>();

            case BattleCardEffectTarget.SelectedTile:
                // 유닛이 아니라 사용자가 선택한 MapInfo 타일 오브젝트 자체를 전달한다.
                // 지속 영역 생성처럼 위치가 필요한 효과가 이 규칙을 사용한다.
                return context.SelectedTile != null
                    ? new List<GameObject> { context.SelectedTile.gameObject }
                    : new List<GameObject>();

            case BattleCardEffectTarget.PreviousEffectTargets:
                // 복합 카드의 앞 효과가 맞힌 대상을 다음 효과가 그대로 이어받는다.
                // 앞 단계 이후 사망한 적은 제외하되 플레이어 자신은 생존 검사와 관계없이 유지한다.
                return FilterLivingOrSelf(previousEffectTargets, context.Player);

            case BattleCardEffectTarget.AllEnemies:
                // 현재 활성화된 모든 적을 수집하고 플레이어 위치 기준으로 정렬한다.
                return CollectAllEnemies(context.Player.transform.position);

            case BattleCardEffectTarget.TargetsInArea:
                // 선택 타일과 카드의 범위 설정을 기준으로 범위 안의 적만 수집한다.
                return CollectEnemiesInArea(context);

            default:
                // 새 대상 enum이 추가됐지만 이 분기가 구현되지 않은 경우 효과를 임의 대상에게 적용하지 않는다.
                return new List<GameObject>();
        }
    }

    /// <summary>
    /// 현재 Scene에서 EnemyTurnActor를 가진 활성 생존 적을 모두 찾아 반환한다.
    /// AllEnemies 대상 효과를 준비할 때 직접 호출되며, 연쇄 피해의 다음 후보를 만들 때도 호출된다.
    /// Update/LateUpdate에서 매 프레임 호출되지는 않지만 카드 사용을 준비할 때마다 Scene 전체 검색이 발생한다.
    /// 교체 시에는 BattleSceneInstaller가 이미 소유한 BattleUnitRegistry를 Player 행동 흐름에 직접 전달하고,
    /// BuildEffectContext가 Registry.Enemies를 Context에 넣은 뒤 이 함수와 CollectEnemiesInArea가 그 목록만 순회한다.
    /// 죽었지만 해제 Queue에 남은 적은 IsLiving으로 제외하고, 원본 Registry 순서를 바꾸지 않도록 별도 결과 List만 정렬한다.
    /// </summary>
    /// <param name="sortCenter">
    /// 반환 순서를 결정할 중심 위치. 대상은 이 위치를 기준으로 시계 방향 정렬된다.
    /// </param>
    private static List<GameObject> CollectAllEnemies(Vector3 sortCenter)
    {
        List<GameObject> livingEnemies = new List<GameObject>();

        // 호출 시점의 Scene 전체에서 EnemyTurnActor 컴포넌트를 검색한다.
        // 비활성 오브젝트는 기본 검색 결과에서 제외되며, IsLiving으로 활성 상태·HP·사망 여부를 다시 확인한다.
        EnemyTurnActor[] enemyTurnActors =
            UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemyTurnActor in enemyTurnActors)
        {
            if (enemyTurnActor != null && IsLiving(enemyTurnActor.gameObject))
                livingEnemies.Add(enemyTurnActor.gameObject);
        }

        // FindObjectsByType(None)은 순서를 보장하지 않는다.
        // 광역/연쇄 결과와 VFX 순서를 실행할 때마다 동일하게 만들기 위해 중심 기준 시계 방향으로 정렬한다.
        SortClockwise(livingEnemies, sortCenter);
        return livingEnemies;
    }

    /// <summary>
    /// 카드의 범위 중심과 areaSizeTiles를 기준으로 범위 안에 있는 생존 적을 수집한다.
    /// TargetsInArea 효과를 준비할 때 호출되며, 현재는 호출할 때마다 Scene 전체의 EnemyTurnActor를 검색한다.
    /// 또한 검색된 적마다 FindNearestTileAtPosition을 호출해 현재 타일을 계산하므로 CollectAllEnemies보다 작업량이 크다.
    /// 한 효과 단계에서는 AllEnemies/연쇄 분기와 동시에 실행되지 않지만, 복합 카드가 서로 다른 대상 규칙의
    /// 효과를 여러 개 가지면 같은 카드 Confirm 과정에서 두 Scene 검색이 연속으로 발생할 수 있다.
    /// TD-CARD-012에서 BattleUnitRegistry.Enemies를 전달받는 방식으로 CollectAllEnemies와 함께 교체한다.
    /// </summary>
    private static List<GameObject> CollectEnemiesInArea(Context context)
    {
        // Self 범위 카드 또는 선택 타일이 없는 카드는 플레이어의 현재 타일을 범위 중심으로 사용한다.
        // 그 외 카드는 사용자가 확정한 SelectedTile을 중심으로 사용한다.
        MapInfo center = context.Card.targetType == BattleCardTargetType.Self || context.SelectedTile == null
            ? context.FindNearestTileAtPosition(context.Player.transform.position)
            : context.SelectedTile;

        List<GameObject> enemiesInsideArea = new List<GameObject>();
        int areaSizeInTiles = Mathf.Max(1, context.Card.areaSizeTiles);

        // 현재 Scene 전체 검색이다. 비활성 적은 검색 결과에서 제외되고, IsLiving이 HP와 사망 상태를 추가 검사한다.
        EnemyTurnActor[] enemyTurnActors =
            UnityEngine.Object.FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.None);
        foreach (EnemyTurnActor enemyTurnActor in enemyTurnActors)
        {
            if (enemyTurnActor == null || !IsLiving(enemyTurnActor.gameObject))
                continue;

            // 적의 월드 좌표를 전투 타일로 변환한 뒤 중심 타일과의 거리·범위 형태를 검사한다.
            MapInfo enemyCurrentTile =
                context.FindNearestTileAtPosition(enemyTurnActor.transform.position);
            if (IsTileInArea(context, center, enemyCurrentTile, areaSizeInTiles))
                enemiesInsideArea.Add(enemyTurnActor.gameObject);
        }

        // FindObjectsByType 결과 순서는 보장되지 않으므로 VFX와 적용 순서를 고정하기 위해 시계 방향으로 정렬한다.
        Vector3 sortCenter = center != null
            ? center.transform.position
            : context.Player.transform.position;
        SortClockwise(enemiesInsideArea, sortCenter);
        return enemiesInsideArea;
    }

    /// <summary>
    /// 검사할 타일이 카드의 효과 범위 안에 포함되는지 판정한다.
    /// 주변 타일 목록을 새로 수집하는 함수가 아니라, 이미 전달받은 중심 타일과 대상 타일의 거리 및
    /// 카드 areaType을 비교해 현재 대상 하나를 포함할지 true/false로 반환한다.
    /// </summary>
    /// <param name="context">카드 범위 형태와 플레이어 현재 위치를 제공하는 카드 사용 정보.</param>
    /// <param name="center">범위 효과가 펼쳐지는 중심 타일.</param>
    /// <param name="tile">현재 범위 안인지 검사할 적의 위치 타일.</param>
    /// <param name="size">중심에서 허용할 최대 타일 거리.</param>
    private static bool IsTileInArea(Context context, MapInfo center, MapInfo tile, int size)
    {
        // 중심 또는 검사 대상의 타일 정보가 없으면 거리를 계산할 수 없으므로 범위 밖으로 처리한다.
        if (center == null || tile == null)
            return false;

        // 맵의 실제 이동 연결을 기준으로 중심과 대상 사이의 타일 거리를 구한다.
        // 단순 월드 좌표 거리가 아니므로 끊긴 타일이나 도달 불가능한 위치는 음수로 반환될 수 있다.
        int distance = BattleTileRangeCalculator.GetDistance(center, tile, size);
        if (distance < 0 || distance > size)
            return false;

        // Line이 아닌 원형·십자형 등의 일반 범위는 거리 조건을 통과한 것만으로 포함한다.
        // 세부 범위 형태가 추가되면 이 조기 반환을 타입별 분기로 확장해야 한다.
        if (context.Card.areaType != BattleCardAreaType.Line)
            return true;

        // Line 범위는 플레이어가 어느 방향으로 중심 타일을 지정했는지 알아야 가로/세로 축을 결정할 수 있다.
        MapInfo origin = context.FindNearestTileAtPosition(context.Player.transform.position);
        if (origin == null)
            return false;

        // direction: 플레이어 타일에서 선택한 중심 타일까지 향하는 방향.
        // offset: 선택한 중심 타일에서 현재 검사 중인 적 타일까지의 상대 위치.
        Vector2Int direction = center.Index - origin.Index;
        Vector2Int offset = tile.Index - center.Index;

        // 플레이어→중심 이동이 가로 방향에 더 가까우면 중심과 같은 행(offset.y == 0)만 포함한다.
        // 세로 방향에 더 가까우면 중심과 같은 열(offset.x == 0)만 포함한다.
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return offset.y == 0;
        return offset.x == 0;
    }

    /// <summary>
    /// 직전 효과가 맞힌 첫 대상을 연쇄 시작점으로 삼아, 아직 맞지 않은 생존 적 중 가까운 순서대로
    /// 최대 <paramref name="maximumChainTargets"/>명을 선택한다.
    /// 이전에 수집한 전체 적 목록을 재사용하는 구조가 아니라 내부에서 CollectAllEnemies를 다시 호출하므로
    /// 연쇄 효과를 준비할 때마다 Scene 전체 EnemyTurnActor 검색이 한 번 발생한다.
    /// </summary>
    /// <param name="context">플레이어 위치와 월드 좌표→타일 변환 함수를 제공하는 카드 사용 정보.</param>
    /// <param name="targetsHitByPreviousEffect">
    /// 바로 앞 효과 단계에서 확정된 대상 목록. 첫 번째 대상을 연쇄 거리 계산의 시작점으로 사용하며,
    /// 목록에 포함된 모든 대상은 중복 타격 후보에서 제거한다.
    /// </param>
    /// <param name="maximumChainTargets">연쇄 효과가 새로 선택할 수 있는 최대 적 수.</param>
    /// <returns>연쇄 시작점에서 타일 거리가 가까운 순서로 정렬된 새 대상 목록.</returns>
    private static List<GameObject> FindNearestTargetsForChainDamage(
        Context context,
        List<GameObject> targetsHitByPreviousEffect,
        int maximumChainTargets)
    {
        // 현재 Scene의 생존 적을 다시 수집한다. TD-CARD-012에서 Registry 목록 순회로 교체할 대상이다.
        List<GameObject> chainTargetCandidates =
            CollectAllEnemies(context.Player.transform.position);

        // 직전 효과가 이미 맞힌 대상은 같은 연쇄 단계에서 다시 선택하지 않는다.
        foreach (GameObject previouslyHitTarget in targetsHitByPreviousEffect)
            chainTargetCandidates.Remove(previouslyHitTarget);

        // 연쇄를 시작할 직전 대상이 없으면 거리 기준을 만들 수 없으므로 빈 목록을 반환한다.
        if (targetsHitByPreviousEffect.Count == 0 || targetsHitByPreviousEffect[0] == null)
            return new List<GameObject>();

        // 복합 카드의 바로 앞 효과가 확정한 첫 대상을 연쇄가 튀기 시작하는 기준 타일로 사용한다.
        MapInfo chainOriginTile = context.FindNearestTileAtPosition(
            targetsHitByPreviousEffect[0].transform.position);

        chainTargetCandidates.Sort((firstCandidate, secondCandidate) =>
        {
            // first/second는 화면의 좌우 방향이 아니라 Sort가 비교 중인 적 A와 적 B다.
            MapInfo firstCandidateTile = context.FindNearestTileAtPosition(
                firstCandidate.transform.position);
            MapInfo secondCandidateTile = context.FindNearestTileAtPosition(
                secondCandidate.transform.position);

            int firstCandidateDistance = BattleTileRangeCalculator.GetDistance(
                chainOriginTile, firstCandidateTile, int.MaxValue);
            int secondCandidateDistance = BattleTileRangeCalculator.GetDistance(
                chainOriginTile, secondCandidateTile, int.MaxValue);

            // GetDistance가 음수를 반환하면 타일 연결상 도달할 수 없는 대상이다.
            // int.MaxValue로 바꿔 정렬의 맨 뒤로 보내며, 값이 큰 적을 선택하려는 계산이 아니다.
            if (firstCandidateDistance < 0)
                firstCandidateDistance = int.MaxValue;
            if (secondCandidateDistance < 0)
                secondCandidateDistance = int.MaxValue;

            // 오름차순 비교이므로 연쇄 시작점에서 가까운 적이 목록 앞쪽으로 이동한다.
            return firstCandidateDistance.CompareTo(secondCandidateDistance);
        });

        // 정렬된 목록 앞에서 허용 개수만 남기고 나머지 먼 적을 제거한다.
        if (chainTargetCandidates.Count > maximumChainTargets)
        {
            chainTargetCandidates.RemoveRange(
                maximumChainTargets,
                chainTargetCandidates.Count - maximumChainTargets);
        }

        return chainTargetCandidates;
    }

    /// <summary>
    /// 준비 단계에서 확정된 대상들에게 계산 완료된 피해량을 반복 적용한다.
    /// 카드가 마법 피해 카드이거나 forceMagicDamage가 true이면 마법 피해로,
    /// 그 외에는 물리 피해로 BattleDamageService에 전달한다.
    /// 피해 적용이 끝나면 플레이어 모델이 첫 번째 대상을 바라보도록 방향을 조정한다.
    /// </summary>
    private static void ApplyRepeatedDamageEffect(
        Context context,
        List<GameObject> confirmedTargets,
        float damagePerHit,
        int numberOfHits,
        bool forceMagicDamage)
    {
        // 신성처럼 카드 분류와 무관하게 마법 피해로 처리할 효과는 forceMagicDamage를 사용한다.
        // 그 외에는 카드 데이터의 cardType을 기준으로 최종 피해 타입을 정한다.
        BattleDamageType finalDamageType =
            forceMagicDamage || context.Card.cardType == BattleCardType.MagicDamage
                ? BattleDamageType.Magic
                : BattleDamageType.Physical;

        foreach (GameObject confirmedTarget in confirmedTargets)
        {
            // 다단 공격 도중 대상이 죽으면 남은 타격은 적용하지 않는다.
            // 각 타격은 공용 BattleDamageService를 통과하므로 상태이상 보정, 보호막, 사망 이벤트가 동일하게 처리된다.
            for (int hitIndex = 0;
                 hitIndex < numberOfHits && IsLiving(confirmedTarget);
                 hitIndex++)
            {
                BattleDamageService.TryApplyDamage(
                    context.Player,
                    confirmedTarget,
                    damagePerHit,
                    finalDamageType,
                    out _);
            }
        }

        // 광역 효과라도 연출 방향은 ConfirmedTargets의 첫 대상 하나를 기준으로 정한다.
        // 대상 목록은 앞 단계에서 결정·정렬되므로 여기서는 다시 대상을 검색하지 않는다.
        if (confirmedTargets.Count > 0 && confirmedTargets[0] != null)
        {
            BattleUnitMotionAnimator.FaceTowards(
                context.Player.transform,
                confirmedTargets[0].transform.position);
        }
    }

    /// <summary>
    /// 거리순으로 확정된 연쇄 대상에게 순서가 뒤로 갈수록 0.5씩 감소하는 마법 피해를 적용한다.
    /// 현재 공식은 기본 피해 - (0.5 × (대상 순번 + 1))이므로 첫 연쇄 대상도 기본 피해보다 0.5 낮다.
    /// 계산 결과가 0 이하가 되면 이후 대상도 더 낮은 피해만 나오므로 남은 연쇄를 즉시 중단한다.
    /// </summary>
    private static void ApplyChainDamageWithFalloff(
        Context context,
        List<GameObject> chainTargetsInOrder,
        float baseChainDamage)
    {
        for (int chainTargetIndex = 0;
             chainTargetIndex < chainTargetsInOrder.Count;
             chainTargetIndex++)
        {
            // 첫 대상은 -0.5, 두 번째는 -1.0, 세 번째는 -1.5 방식으로 피해가 감소한다.
            float damageReductionForChainOrder = 0.5f * (chainTargetIndex + 1);
            float damageForCurrentTarget = Mathf.Max(
                0f,
                baseChainDamage - damageReductionForChainOrder);
            if (damageForCurrentTarget <= 0f)
                break;

            // 연쇄 피해는 현재 데이터 규칙상 항상 마법 피해로 공용 피해 서비스를 통과한다.
            BattleDamageService.TryApplyDamage(
                context.Player,
                chainTargetsInOrder[chainTargetIndex],
                damageForCurrentTarget,
                BattleDamageType.Magic,
                out _);
        }
    }

    /// <summary>
    /// 직전 효과 대상 중 다음 효과로 넘길 수 있는 대상만 새 목록으로 만든다.
    /// 플레이어 자신은 생존 검사 없이 유지하고, 그 외 대상은 활성 상태이며 BattleHealth가 있고
    /// 사망하지 않았을 때만 유지한다. 복합 카드의 앞 효과로 적이 죽은 뒤 다음 효과가 죽은 적에게
    /// 다시 적용되는 것을 막는 안전장치다.
    /// </summary>
    private static List<GameObject> FilterLivingOrSelf(
        List<GameObject> sourceTargets,
        GameObject player)
    {
        List<GameObject> targetsAvailableForNextEffect = new List<GameObject>();
        foreach (GameObject previousTarget in sourceTargets)
        {
            // Self 대상 효과는 Player에 BattleHealth가 없거나 사망 처리 중이어도 대상 연결을 유지한다.
            // 적과 다른 유닛은 IsLiving을 통과해야만 다음 효과 대상으로 전달된다.
            if (previousTarget == player || IsLiving(previousTarget))
                targetsAvailableForNextEffect.Add(previousTarget);
        }

        return targetsAvailableForNextEffect;
    }

    private static bool IsLiving(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;
        BattleHealth health = target.GetComponent<BattleHealth>();
        return health != null && !health.IsDead;
    }

    /// <summary>
    /// 대상 목록을 중심 위치의 정면(+Z, 0도)부터 시계 방향 각도가 작은 순서로 정렬한다.
    /// FindObjectsByType처럼 반환 순서가 보장되지 않는 검색 결과를 광역 효과와 VFX에서
    /// 매번 동일한 순서로 처리하기 위한 결정적 순서 규칙이다.
    /// 이 함수는 전달받은 원본 List 자체의 순서를 변경한다.
    /// </summary>
    private static void SortClockwise(
        List<GameObject> targetsToSort,
        Vector3 centerPosition)
    {
        targetsToSort.Sort((firstTarget, secondTarget) =>
            ClockwiseAngle(centerPosition, firstTarget.transform.position)
                .CompareTo(ClockwiseAngle(centerPosition, secondTarget.transform.position)));
    }

    /// <summary>
    /// 중심에서 대상까지의 XZ 평면 방향을 0~360도 시계 방향 각도로 변환한다.
    /// +Z 방향이 0도, +X가 90도, -Z가 180도, -X가 270도가 된다.
    /// 높이(Y)는 탑뷰 전투의 대상 순서에 영향을 주지 않도록 계산에서 제외한다.
    /// </summary>
    private static float ClockwiseAngle(
        Vector3 centerPosition,
        Vector3 targetPosition)
    {
        Vector3 directionFromCenter = targetPosition - centerPosition;

        // Atan2(X, Z)를 사용하면 Unity 정면인 +Z를 0도로 두고 +X 방향으로 각도가 증가한다.
        float clockwiseAngleInDegrees =
            Mathf.Atan2(directionFromCenter.x, directionFromCenter.z) * Mathf.Rad2Deg;

        // Atan2의 -180~180도 결과 중 음수를 0~360도 범위로 변환한다.
        return clockwiseAngleInDegrees < 0f
            ? clockwiseAngleInDegrees + 360f
            : clockwiseAngleInDegrees;
    }

    /// <summary>
    /// 카드 효과의 effectCode가 영문 식별자 또는 이전 한글 식별자와 같은지 확인한다.
    /// 예: CHAIN/연쇄, HOLY/신성을 같은 효과로 인식해 기존 카드 데이터와 현재 코드를 함께 지원한다.
    /// 영문은 대소문자를 무시하지만 한글은 정확히 일치해야 한다.
    /// 효과 데이터의 코드 체계를 하나로 통합하면 제거할 수 있는 Legacy 호환 함수다.
    /// </summary>
    private static bool IsCode(
        BattleCardEffectData effectData,
        string englishEffectCode,
        string koreanLegacyEffectCode)
    {
        if (effectData == null)
            return false;

        return string.Equals(
                   effectData.effectCode,
                   englishEffectCode,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   effectData.effectCode,
                   koreanLegacyEffectCode,
                   StringComparison.Ordinal);
    }
}
