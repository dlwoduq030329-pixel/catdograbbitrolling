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
    // 기능 인덱스는 아래 함수 리뷰에서도 동일하게 사용한다.
    // [1] 카드 행동 진행 상태      [2] Player 전투 자원
    // [3] 대상 사거리 계산·표시   [4] 화면 입력과 대상 탐색
    // [5] 카드 결과 미리보기      [6] 손패·덱 소비
    // [7] 외부 UI·Coordinator 통지

    // [1] 카드 행동 진행 상태
    // 현재 사용하려는 카드와 선택된 대상을 보관한다. 선택 → 확인 → 실행 → 초기화 흐름의 중심 상태다.
    private SelectedCardUseInfo selectedCardInfo;
    private GameObject selectedTarget;
    private MapInfo selectedTargetTile;

    public bool IsSelectingTarget { get; private set; }
    public bool IsAwaitingConfirmation { get; private set; }
    /// <summary>카드 대상 선택 또는 사용 확인이 진행 중인지 반환한다.</summary>
    public bool IsActionActive => IsSelectingTarget || IsAwaitingConfirmation;
    /// <summary>현재 선택한 카드가 요구하는 대상 종류를 반환한다. 진행 중인 카드가 없으면 None이다.</summary>
    public BattleCardTargetType TargetType => selectedCardInfo != null
        ? selectedCardInfo.CardData.targetType
        : BattleCardTargetType.None;

    // [2] Player 전투 자원
    // 카드 사용 주체와 MP·상태이상 참조다. MP 비용 검사와 상태이상에 따른 비용 보정에 사용한다.
    private GameObject player;
    private BattleUnitMP playerMP;
    private BattleStatusEffects playerStatusEffects;

    // [3] 대상 사거리 계산·표시
    // 계산된 유효 타일을 저장하고, 일반 사거리와 효과 범위의 색상 표시·복구에 사용한다.
    private readonly HashSet<MapInfo> rangeTiles = new HashSet<MapInfo>();
    private BattleRangeVisualizer rangeVisualizer;
    private Color rangeColor;
    private Color effectAreaColor;
    private Func<Vector3, MapInfo> findClosestTile;
    private IReadOnlyList<MapInfo> allMapTiles;

    // [5] 카드 결과 미리보기
    // 밀치기·충돌·낙사처럼 카드 확정 전에 보여줘야 하는 결과를 표시한다.
    private BattlePushPreviewView pushPreviewView;

    // [6] 손패·덱 소비
    // 카드 사용이 확정되면 사용한 손패 카드를 버린 카드 더미로 이동하도록 요청한다.
    private BattleCardDrawSystem drawSystem;

    // [7] 외부 UI·Coordinator 통지
    // Controller가 UI를 직접 만들거나 조작하지 않고 단계 변화와 결과만 외부에 알린다.
    public event Action<string> TargetSelectionRequested;
    /// <summary>대상 선택이 끝나 Player 클릭(확인 버튼)으로 확정할 수 있는 상태가 됐을 때 안내 문구와 함께 알린다.
    /// 예전에는 확인 버튼 UI를 직접 켜지 않고 재클릭으로만 확정하도록 바꿨었는데, 실제로는 재클릭 입력 경로가
    /// 연결돼 있지 않아 대상 선택 뒤 확정할 방법이 아예 없어지는 문제가 있었다. 기본 공격(BattleUnitAttackFlow)과
    /// 같은 방식으로 다시 확인 버튼을 띄우도록 이 이벤트를 되살렸다.</summary>
    public event Action<string> ConfirmationRequested;
    public event Action<BattleActionResult> Confirmed;
    public event Action Cancelled;
    public event Action<bool> RangeVisibilityChanged;

    /// <summary>
    /// 카드 사용 흐름에 필요한 외부 참조를 Player 등록 시 한 번 연결한다.
    /// [2] Player와 전투 자원, [3] 등록된 맵 타일과 사거리 표시,
    /// [5] 밀치기 결과 Preview를 받아 이후 카드 선택·확정 과정에서 재사용한다.
    /// 이 함수는 카드 사용을 시작하거나 UI를 표시하지 않고 참조 연결만 수행한다.
    /// </summary>
    public void Configure(
        GameObject targetPlayer,
        BattleRangeVisualizer visualizer,
        Color cardRangeColor,
        Color cardEffectAreaColor,
        Func<Vector3, MapInfo> tileFinder,
        IReadOnlyList<MapInfo> registeredMapTiles,
        BattlePushPreviewView previewView)
    {
        // [2] 카드 사용 주체를 보관한다. 이후 Self 대상 지정과 효과 실행 Context의 Player로 사용한다.
        player = targetPlayer;

        // [2] 카드 선택·확정마다 GetComponent를 반복하지 않도록 Player 등록 시 한 번 보관한다.
        // MP 값은 같은 BattleUnitMP 인스턴스에서 변경되므로 CurrentMP를 읽으면 항상 최신 수치다.
        playerMP = targetPlayer != null ? targetPlayer.GetComponent<BattleUnitMP>() : null;

        // [2] 동상처럼 카드 MP 비용을 변경하는 상태이상을 계산하기 위해 함께 보관한다.
        playerStatusEffects = targetPlayer != null ? targetPlayer.GetComponent<BattleStatusEffects>() : null;

        // [3] 계산된 카드 사거리와 효과 범위를 실제 타일 색상으로 표시하는 전용 컴포넌트다.
        rangeVisualizer = visualizer;

        // [3] 일반 대상 선택 사거리와 광역 효과 예상 범위를 서로 다른 색으로 구분한다.
        rangeColor = cardRangeColor;
        effectAreaColor = cardEffectAreaColor;

        // [3] Player·Enemy의 월드 위치를 현재 MapInfo로 변환할 때 호출하는 타일 검색 함수다.
        // Controller가 맵 보관 방식을 직접 알지 않도록 함수 참조만 전달받는다.
        findClosestTile = tileFinder;
        // 지속 영역 Preview도 Scene 검색 없이 공식 Map 목록만 계산기에 전달한다.
        allMapTiles = registeredMapTiles;

        // [5] 밀치기 카드의 이동·충돌·낙사 예상 결과를 카드 확정 전에 표시한다.
        pushPreviewView = previewView;
    }

    /// <summary>
    /// 손패에서 선택한 카드가 현재 사용을 시작할 수 있는지 검사하고 대상 선택 또는 확인 단계로 진입한다.
    /// [1] 진행 중인 다른 카드 행동과 기본 참조를 검사하고 선택 카드를 저장한다.
    /// [2] 상태이상이 반영된 MP 비용을 계산해 현재 MP로 사용할 수 있는지 검사한다.
    /// [3] 카드의 대상 선택 규칙에 따라 자동 대상을 찾거나 수동 대상 사거리를 표시한다.
    /// [6] 확정 후 정확한 손패 카드를 소비할 DrawSystem을 함께 저장한다.
    /// 이 단계에서는 MP·손패를 소비하거나 카드 효과를 실행하지 않는다.
    /// </summary>
    public bool TryBeginSelectedCardFlow(
        SelectedCardUseInfo selectedHandCard,
        BattleCardDrawSystem sourceCardDrawSystem,
        bool cardsCanBeUsedThisTurn)
    {
        // [1][6] 요청 데이터와 카드 출처가 없거나 다른 카드 행동이 진행 중이면 새 흐름을 시작하지 않는다.
        if (selectedHandCard == null || selectedHandCard.CardData == null ||
            sourceCardDrawSystem == null || player == null ||
            IsSelectingTarget || IsAwaitingConfirmation || !cardsCanBeUsedThisTurn)
        {
            return false;
        }

        // [2] MP가 부족한 카드의 대상 선택과 Preview를 열지 않도록 시작 시점에 먼저 검사한다.
        // 확정 시점에는 선택 도중 상태이상이나 MP가 바뀌었을 가능성 때문에 비용을 다시 검사한다.
        int requiredMp = CalculateCardMpCostAfterStatusEffects(
            selectedHandCard.ActionInfo.MPCost,
            selectedHandCard.CardData);
        if (playerMP == null || !playerMP.CanSpend(requiredMp))
        {
            Debug.Log($"카드 사용 불가: 행동력이 {requiredMp} 필요합니다.", this);
            return false;
        }

        // [1] 확정·취소까지 유지해야 하는 현재 카드 사용 정보를 저장한다.
        selectedCardInfo = selectedHandCard;
        // [6] 확정 성공 시 같은 손패 슬롯의 카드를 사용 완료 처리할 원본 DrawSystem이다.
        drawSystem = sourceCardDrawSystem;

        // [3] 자동 대상 카드라면 사거리 안에서 HP가 가장 낮은 Enemy를 즉시 선택한다.
        if (BattleCardTargetSelectionRules.UsesAutomaticLowestHealthEnemyTarget(selectedCardInfo.CardData))
        {
            if (!BattleCardTargetSelector.TryFindLowestHealthEnemyInRange(
                    player,
                    selectedCardInfo.ActionInfo.RangeTiles,
                    findClosestTile,
                    out selectedTarget,
                    out selectedTargetTile))
            {
                // 자동으로 찾을 Enemy가 없어도 카드 선택은 취소하지 않는다.
                // 수동 대상 선택 상태로 들어가 사거리 정보는 Player에게 계속 보여준다.
                EnterTargetSelectionState();
                return true;
            }

            // 유효한 자동 대상이 정해진 경우에만 카드 사용 확인 단계로 이동한다.
            EnterCardUseConfirmationState();
            return true;
        }

        // [1][3] 외부 대상이 필요 없는 카드는 Player 자신을 대상으로 저장하고 바로 확인 단계로 이동한다.
        BattleCardTargetType targetType = selectedCardInfo.CardData.targetType;
        if (targetType == BattleCardTargetType.None ||
            targetType == BattleCardTargetType.Self ||
            targetType == BattleCardTargetType.Ally ||
            targetType == BattleCardTargetType.AllEnemies)
        {
            selectedTarget = player;
            selectedTargetTile = findClosestTile(player.transform.position);
            ShowEffectAreaPreview(selectedTargetTile);
            EnterCardUseConfirmationState();
        }
        else
        {
            // Enemy 또는 Tile처럼 Player가 직접 골라야 하는 카드는 사거리 표시와 대상 선택 상태를 연다.
            EnterTargetSelectionState();
        }

        return true;
    }

    /// <summary>
    /// Player가 선택한 대상이 현재 카드의 표시 사거리 안에 있는지 검사하고 카드 사용 확인 단계로 전환한다.
    /// [1] 선택 대상과 대상 타일을 진행 상태에 저장하고 대상 선택 단계를 종료한다.
    /// [3] 단일 범위가 아닌 카드라면 선택 타일을 중심으로 실제 효과 범위를 추가 표시한다.
    /// [5] 확인 단계 진입 과정에서 밀치기 결과 Preview를 갱신한다.
    /// 확인 버튼 UI는 제거했으므로 별도 UI 이벤트를 보내지 않는다. 이후 Player 클릭이
    /// BattlePlayerActionController → BattlePlayerCardFlow를 거쳐 실제 카드 사용을 확정한다.
    /// </summary>
    public bool TrySelectTargetAndEnterConfirmation(GameObject chosenTarget, MapInfo chosenTargetTile)
    {
        // [1][3] 대상 선택 중이 아니거나 선택한 대상이 계산된 카드 사거리 밖이면 상태를 변경하지 않는다.
        if (!IsSelectingTarget || chosenTarget == null || chosenTargetTile == null ||
            !rangeTiles.Contains(chosenTargetTile))
        {
            return false;
        }

        // [1] 확정과 효과 실행 단계가 같은 대상을 사용하도록 GameObject와 MapInfo를 함께 저장한다.
        selectedTarget = chosenTarget;
        selectedTargetTile = chosenTargetTile;
        IsSelectingTarget = false;

        // [3] 카드 사용 가능 사거리가 아니라, 확정 시 실제 효과가 퍼질 영역을 별도 색상으로 표시한다.
        ShowEffectAreaPreview(selectedTargetTile);

        // [1][5][7] 선택을 끝내고 Preview와 외부 확인 입력을 준비한다.
        EnterCardUseConfirmationState();
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
        if (!HasRequiredStateForCardConfirmation())
        {
            return false;
        }

        // [1][3] 자동 대상 카드는 시스템이 이미 유효한 Enemy를 골랐으므로 수동 선택용 rangeTiles 검사를 생략한다.
        bool cardUsesAutomaticEnemyTargeting =
            BattleCardTargetSelectionRules.UsesAutomaticLowestHealthEnemyTarget(selectedCardInfo.CardData);
        if (!IsSelectedCardTargetStillValid(cardUsesAutomaticEnemyTargeting))
        {
            EnterTargetSelectionState();
            return false;
        }

        // [2] 카드 선택 이후 동상 같은 상태가 변했을 수 있으므로 시작 때의 비용을 재사용하지 않고 다시 계산한다.
        if (!TryCalculateAffordableCardMpCost(out int requiredMp))
        {
            Debug.LogWarning($"카드 사용 불가: MP {requiredMp}이 필요합니다.", this);
            return false;
        }

        // 효과 파이프라인에는 Player, 대상, 타일, 카드 데이터와 외부 실행 함수를 하나의 문맥으로 전달한다.
        BattleCardEffectPipeline.Context effectContext = CreateCardEffectExecutionContext();
        if (!BattleCardEffectPipeline.TryPrepareCardEffects(
                effectContext,
                out BattleCardEffectPipeline.PreparedUse preparedCardEffects,
                out string failureReason))
        {
            Debug.LogWarning($"카드 사용 불가: {failureReason}", this);
            // 수동 대상 카드의 실패는 다른 대상을 고르면 해결될 수 있으므로 대상 선택 상태로 되돌린다.
            if (CurrentCardNeedsWorldTargetSelection() && !cardUsesAutomaticEnemyTargeting)
                EnterTargetSelectionState();
            return false;
        }

        // [2][6] 효과 실행 전 MP와 손패 소비를 확정한다. 실패하면 피해·회복·이동은 발생하지 않는다.
        if (!TrySpendMpAndDiscardUsedCard(requiredMp))
        {
            return false;
        }

        // 상태 초기화 전에 완료 결과에 필요한 카드 대상과 행동 정보를 별도 지역 변수로 보존한다.
        GameObject usedCardTarget = selectedTarget;
        BattleActionRequest completedCardAction = selectedCardInfo.ActionInfo;
        // 사전 검증된 효과만 실행하고, 이후 카드 행동이 끝났음을 상위 입력 흐름에 알린다.
        BattleCardEffectPipeline.ApplyPreparedCardEffects(effectContext, preparedCardEffects);
        BattleActionResult completedCardResult = new BattleActionResult(
            completedCardAction, player, usedCardTarget, Array.Empty<MapInfo>(), 0, requiredMp);
        ClearStateAndRange();
        Confirmed?.Invoke(completedCardResult);
        return true;
    }

    /// <summary>
    /// [1][6] Player 클릭으로 카드 사용을 확정하기 위한 최소 진행 상태와 참조가 모두 남아 있는지 확인한다.
    /// 카드 효과나 대상 생존 여부를 검사하는 함수가 아니다. 확인 대기 상태, 선택 카드, 카드가 나온
    /// DrawSystem, 카드 사용자 Player 중 하나라도 없으면 이후 비용 소비와 결과 생성이 불가능하므로 중단한다.
    /// </summary>
    private bool HasRequiredStateForCardConfirmation()
    {
        return IsAwaitingConfirmation && selectedCardInfo != null && drawSystem != null && player != null;
    }

    /// <summary>
    /// [1][3] 수동으로 선택한 외부 대상이 확정 시점에도 선택 정보·활성 상태·카드 사거리를 유지하는지 검사한다.
    /// Self·AllEnemies처럼 외부 대상이 필요 없는 카드와 시스템이 자동 대상을 정한 카드는 이 검사를 생략한다.
    /// 여기서는 모든 카드에 공통인 GameObject 활성 상태만 검사한다. 실제 BattleHealth 존재 여부와 사망 여부,
    /// 회복 가능 여부처럼 효과 종류마다 다른 조건은 EffectPipeline의 준비 단계에서 한 번만 검사한다.
    /// </summary>
    private bool IsSelectedCardTargetStillValid(bool cardUsesAutomaticEnemyTargeting)
    {
        if (!CurrentCardNeedsWorldTargetSelection() || cardUsesAutomaticEnemyTargeting)
        {
            return true;
        }

        // 선택 대상과 타일이 삭제되지 않았고, 대상 GameObject가 Scene에서 활성 상태이며,
        // 처음 계산한 카드 대상 사거리 안에 남아 있어야 수동 선택 결과를 확정할 수 있다.
        return selectedTarget != null &&
               selectedTargetTile != null &&
               selectedTarget.activeInHierarchy &&
               rangeTiles.Contains(selectedTargetTile);
    }

    /// <summary>
    /// 현재 적용 중인 동상(Frostbite)의 공격 비용 +1을 반영해 최종 카드 MP를 다시 계산한다.
    /// 카드 선택 후 확정 전까지 상태가 변경될 수 있으므로 확정 시점에 캐시된 Player MP로 다시 검사한다.
    /// </summary>
    private bool TryCalculateAffordableCardMpCost(out int requiredMp)
    {
        requiredMp = selectedCardInfo != null
            ? CalculateCardMpCostAfterStatusEffects(
                selectedCardInfo.ActionInfo.MPCost,
                selectedCardInfo.CardData)
            : 0;
        return playerMP != null && playerMP.CanSpend(requiredMp);
    }

    /// <summary>
    /// 선택된 카드, Player, 대상, 타일과 카드 효과 실행에 필요한 외부 기능을
    /// EffectPipeline이 한 번에 읽을 수 있는 실행 입력 객체로 묶는다.
    /// 이 함수는 효과를 검증하거나 실행하지 않고 현재 카드 사용 상태를 복사해 전달할 뿐이다.
    /// </summary>
    private BattleCardEffectPipeline.Context CreateCardEffectExecutionContext()
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
    private bool TrySpendMpAndDiscardUsedCard(int requiredMp)
    {
        // [2] 손패 소비가 실패할 경우 MP까지 원래 값으로 돌리기 위해 차감 전 수치를 보관한다.
        int mpBeforeCardPayment = playerMP.CurrentMP;
        if (!playerMP.TrySpend(requiredMp))
        {
            Debug.LogWarning($"카드 사용 확정 중 MP {requiredMp} 차감에 실패했습니다.", this);
            return false;
        }

        if (!drawSystem.TryMoveUsedCardToDiscardPile(selectedCardInfo))
        {
            // [2][6] DrawSystem은 검사에 실패하면 손패를 변경하지 않으므로 카드 복구는 필요 없다.
            // 먼저 성공했던 MP 차감만 되돌린 뒤 카드 선택·사거리·Preview 상태를 전부 취소한다.
            playerMP.SetCurrentMP(mpBeforeCardPayment);
            Debug.LogError("손패 상태가 변경되어 카드 소비에 실패했습니다. 차감된 MP를 복구했습니다.", this);
            CancelAll();
            return false;
        }

        return true;
    }

    /// <summary>카드를 소비하지 않고 선택 대상, 사거리 표시와 확인 대기 상태를 제거한다.</summary>
    /// <summary>
    /// 외부 취소 입력이 호출하는 공개 진입점이다. 진행 중인 카드 선택, 저장 대상,
    /// 사거리와 밀치기 Preview를 실제로 정리하는 작업은 CancelAll()에 위임한다.
    /// </summary>
    public void Cancel()
    {
        CancelAll();
    }

    /// <summary>턴 전환 시 완료되지 않은 카드 행동과 표시 상태를 안전하게 초기화한다.</summary>
    /// <summary>
    /// Player 턴 시작·종료처럼 턴이 전환될 때 이전 턴에 남은 카드 선택 상태와 모든 Preview를 제거한다.
    /// 사용 카드나 MP를 되돌리는 함수가 아니며, 아직 확정되지 않은 화면·진행 상태만 초기화한다.
    /// </summary>
    public void ResetTurn()
    {
        ClearStateAndRange();
    }

    /// <summary>
    /// 외부 대상을 직접 선택해야 하는 카드의 대상 선택 단계를 연다.
    /// 최초 카드 선택 또는 확정 실패 후 재선택에 공통으로 사용하며, 이전 Push 미리보기와 저장 대상을
    /// 지운 뒤 카드 사거리를 다시 표시한다.
    /// </summary>
    private void EnterTargetSelectionState()
    {
        pushPreviewView?.HideAllPushPreviews();
        IsAwaitingConfirmation = false;
        IsSelectingTarget = true;
        selectedTarget = null;
        selectedTargetTile = null;
        RefreshCardTargetSelectionRange();
        TargetSelectionRequested?.Invoke(
            $"{selectedCardInfo.ActionInfo.DisplayName}: 사거리 안의 대상을 우클릭하세요.");
    }

    /// <summary>
    /// 대상 선택을 끝내고 확인 버튼을 눌러 카드 사용을 확정할 수 있는 대기 상태로 전환한다.
    /// 밀치기·돌진 카드라면 확정 전에 예상 이동 결과를 갱신한다.
    /// ConfirmationRequested로 기본 공격과 같은 확인(사용) 버튼을 다시 띄우며, 실제 확정 입력은
    /// BattlePlayerActionController.ConfirmCurrentPlayerAction() → BattlePlayerCardFlow.Confirm()
    /// → TryConfirmCardUse() 순서로 처리된다.
    /// </summary>
    private void EnterCardUseConfirmationState()
    {
        // [1] 대상은 이미 저장되었으므로 대상 선택을 닫고 Player 클릭 확정만 기다린다.
        IsSelectingTarget = false;
        IsAwaitingConfirmation = true;

        // [5] Push가 없는 카드는 기존 Preview를 숨기고, Push가 있으면 예상 충돌 위치를 표시한다.
        RefreshPushPreview();

        // [7] 확인 버튼(사용 버튼)을 다시 띄워 Player가 무엇을 눌러야 확정되는지 보이게 한다.
        // MP 비용은 상태이상 등으로 확정 직전 다시 계산될 수 있어 여기서는 카드 기본 비용만 안내에 쓴다.
        ConfirmationRequested?.Invoke(
            $"{selectedCardInfo.ActionInfo.DisplayName}을(를) 사용하시겠습니까? (필요 MP {selectedCardInfo.ActionInfo.MPCost})");
    }

    /// <summary>
    /// 대상 선택이 끝난 뒤 선택 타일 또는 Player 타일을 중심으로 카드의 실제 영향 범위를 표시한다.
    /// 카드 사용 가능 사거리를 보여주는 RefreshCardTargetSelectionRange()와 달리, 이 함수는 카드가 확정됐을 때
    /// 피해·회복·지속 영역이 적용될 타일을 미리 보여준다.
    /// </summary>
    private void ShowEffectAreaPreview(MapInfo effectCenterTile)
    {
        if (selectedCardInfo == null || selectedCardInfo.CardData == null || effectCenterTile == null ||
            selectedCardInfo.CardData.areaType == BattleCardAreaType.Single)
            return;

        // Controller는 효과 범위의 계산 공식을 갖지 않는다. 현재 카드와 선택 결과만 전달하고,
        // 실제 BFS·Cross·Line·지속 영역 모양 판정은 공용 타일 계산기에 맡긴다.
        MapInfo cardUserTile = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        bool usesPersistentSquareArea = BattleCardEffectDataQuery.ContainsEffect(
            selectedCardInfo.CardData,
            BattleCardEffectType.CreateArea);
        HashSet<MapInfo> effectAreaTiles = BattleTileRangeCalculator.FindCardEffectAreaTiles(
            effectCenterTile,
            cardUserTile,
            selectedCardInfo.CardData.areaType,
            selectedCardInfo.CardData.areaSizeTiles,
            usesPersistentSquareArea,
            usesPersistentSquareArea ? allMapTiles : null);

        // 색상 합성과 원본색 복구는 Visualizer 책임이다. Controller는 계산 결과만 전달한다.
        rangeVisualizer?.ShowCardRangeTiles(effectAreaTiles, effectAreaColor);
        RangeVisibilityChanged?.Invoke(effectAreaTiles.Count > 0);
    }

    /// <summary>
    /// 현재 Player 타일에서 카드 사거리 안의 선택 가능 타일을 계산한 뒤 화면에 표시한다.
    /// 계산은 BattleTileRangeCalculator, 색상 표시는 BattleRangeVisualizer에 위임하며,
    /// 이 함수는 두 전문 컴포넌트를 순서대로 호출하고 결과를 선택 상태에 보관하는 역할만 한다.
    /// </summary>
    private void RefreshCardTargetSelectionRange()
    {
        // 이전 카드 Preview가 바꿨던 색상과 선택 가능 목록을 먼저 비운다.
        rangeVisualizer?.ClearCardRangeTiles();
        rangeTiles.Clear();

        MapInfo playerCurrentTile = findClosestTile != null && player != null
            ? findClosestTile(player.transform.position)
            : null;
        if (playerCurrentTile == null)
        {
            Debug.LogError(
                "카드 사거리 계산 실패: Player가 서 있는 MapInfo 타일을 찾지 못했습니다. " +
                "Player 등록과 맵 타일 목록을 확인해야 합니다.",
                this);
            CancelAll();
            return;
        }

        HashSet<MapInfo> tilesWithinTargetRange = BattleTileRangeCalculator.FindCardTargetTiles(
            playerCurrentTile,
            selectedCardInfo.ActionInfo.RangeTiles);
        // HashSet.UnionWith은 중복 타일을 만들지 않고 이번 계산 결과를 Controller의 현재 선택 가능 목록에 합친다.
        rangeTiles.UnionWith(tilesWithinTargetRange);
        rangeVisualizer?.ShowCardRangeTiles(rangeTiles, rangeColor);
        RangeVisibilityChanged?.Invoke(true);
    }

    /// <summary>
    /// [1][3] 현재 카드가 Player 자신이나 전체 대상이 아니라 Scene의 Enemy·Character·Tile 하나를
    /// 선택 대상으로 요구하는지 반환한다. true인 카드는 선택 대상과 대상 타일을 저장하고 사거리 검사를 거친다.
    /// 현재 Ally는 TryBeginSelectedCardFlow()에서 Player 자신으로 처리되므로 포함하지 않는다.
    /// 용병 등 실제 아군 선택 기능을 추가할 때 Ally 입력 탐색을 구현한 뒤 이 조건에도 포함해야 한다.
    /// </summary>
    private bool CurrentCardNeedsWorldTargetSelection()
    {
        BattleCardTargetType targetType = TargetType;
        return targetType == BattleCardTargetType.Enemy ||
               targetType == BattleCardTargetType.Character ||
               targetType == BattleCardTargetType.Tile;
    }

    /// <summary>
    /// [2] 카드 데이터의 기본 MP 비용에 Player 상태이상 비용 규칙을 적용해 최종 비용을 반환한다.
    /// 현재는 공격 카드에만 동상(Frostbite)의 공격 비용 +1을 적용하고 다른 카드 분류는 기본 비용을 유지한다.
    /// </summary>
    private int CalculateCardMpCostAfterStatusEffects(int baseMpCost, BattleCardData cardData)
    {
        // 현재는 동상(Frostbite)이 공격 카드와 기본 공격의 MP 비용을 1 증가시킨다.
        // 공격 카드가 아니거나 Player에게 상태이상 저장소가 없으면 데이터의 기본 비용을 그대로 사용한다.
        return playerStatusEffects != null &&
               cardData != null &&
               cardData.category == BattleCardCategory.Attack
            ? playerStatusEffects.ModifyAttackCost(baseMpCost)
            : baseMpCost;
    }

    /// <summary>
    /// [2] 카드 Pipeline이 요청한 상태이상을 대상 Unit의 공용 상태 저장소(BattleStatusEffects)에 적용한다.
    /// 2026-09-05: 예전에는 기절·속박만 BattleEnemyControlState라는 Enemy 전용 저장소에 따로 한 번 더
    /// 적용했다(같은 상태를 두 군데에 이중으로 기록). 그 중복은 표시되는 지속 턴(BattleStatusEffects,
    /// 누적 합산)과 실제 행동 차단 판정(ControlState, 최댓값 갱신)이 서로 어긋나는 원인이었다. 지금은
    /// EnemyTurnActor가 기절·속박도 BattleStatusEffects만 읽도록 바뀌어 ControlState 자체가 삭제됐으므로,
    /// 여기서도 모든 상태이상을 BattleStatusEffects.Apply() 한 번으로만 적용하면 된다.
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
    }

    /// <summary>[1][7] 카드 선택 상태를 초기화하고 상위 UI에도 사용 취소를 알린다.</summary>
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
    /// [5] 확정 전 Push 효과의 예상 결과를 표시한다. Push가 아니면 기존 표시를 숨긴다.
    /// 돌진 후 밀치기는 돌진 도착 타일을 밀치기 시작점으로 계산하고,
    /// 회전 공격은 인접한 모든 대상의 밀치기 계획을 각각 만들어 동시에 표시한다.
    /// </summary>
    private void RefreshPushPreview()
    {
        if (pushPreviewView == null || selectedCardInfo == null || selectedTarget == null)
        {
            pushPreviewView?.HideAllPushPreviews();
            return;
        }

        // Controller는 현재 카드 상태만 전달한다. 대상 검색·Dash 예상 위치·Push 충돌 계산은
        // Planner가 수행하고, View는 완성된 계획을 이미지로 표시한다.
        List<BattleCardMovementService.PushPlan> pushPlans =
            BattleCardPushPreviewPlanner.BuildPushPlans(
            player,
            selectedTarget,
            selectedCardInfo.CardData,
            findClosestTile);
        pushPreviewView.ShowPushPredictions(pushPlans);
    }

}

/// <summary>
/// 카드 데이터의 대상 선택 정책에 따라 Player가 대상을 직접 고를지 시스템이 자동 선택할지를 판정한다.
/// 이동·피해 같은 효과 종류와 대상 선택 방식을 분리해 같은 Teleport라도 수동 또는 자동 대상을 사용할 수 있다.
/// </summary>
internal static class BattleCardTargetSelectionRules
{
    /// <summary>이 카드가 사거리 안에서 현재 HP가 가장 낮은 Enemy를 자동 선택하도록 설정됐는지 반환한다.</summary>
    internal static bool UsesAutomaticLowestHealthEnemyTarget(BattleCardData card)
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
