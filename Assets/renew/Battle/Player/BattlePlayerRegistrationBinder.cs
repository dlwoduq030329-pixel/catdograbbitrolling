using UnityEngine;

/// <summary>
/// 새로 생성된 Player의 전투 데이터, MP 화면, 카드 덱, 행동 제어기를 한 번에 연결한다.
/// BattleGameManager는 Player 등록 시점만 결정하고 실제 컴포넌트 연결은 이 바인더에 위임한다.
/// (2026-08-22 개명: BattlePlayerRuntimeBinder -> BattlePlayerRegistrationBinder — 이름이 같은
/// BattlePlayerRegistrationService/BattlePlayerRuntimeDataFactory와 헷갈린다는 리뷰 지적으로,
/// "등록(Registration) 흐름의 Scene 진입점 컴포넌트"라는 역할이 이름에서 바로 드러나도록 바꿨다.
/// 실제 등록 로직은 BattlePlayerRegistrationService에 그대로 위임한다.)
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerRegistrationBinder : MonoBehaviour
{
    [Header("Player 등록 대상")]
    [SerializeField] private PlayerMPUI playerMpView;

    public bool TryBind(
        GameObject player,
        BattleCardDrawSystem cardDrawSystem,
        BattlePlayerActionController actionController,
        Object logContext,
        out BattleUnitMP playerMP,
        out PlayerCombatData combatData,
        out BattleHealth playerHealth)
    {
        return BattlePlayerRegistrationService.TryRegisterRuntime(
            player,
            playerMpView,
            cardDrawSystem,
            actionController,
            logContext,
            out playerMP,
            out combatData,
            out playerHealth);
    }
}
