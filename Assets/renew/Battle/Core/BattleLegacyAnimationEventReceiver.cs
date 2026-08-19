using UnityEngine;

/// <summary>
/// 레거시 공격 애니메이션 클립(IdleAttackMelee, IdleAttackHand 등)에 이미 박혀 있는
/// Animation Event(예: AdabptDam)가 받는 사람이 없어 콘솔 경고를 내지 않도록 받아주는 빈 껍데기다.
/// 실제 피해 적용은 이 컴포넌트가 아니라 BattleDamageService가 별도 흐름에서 처리한다.
/// 애니메이션 클립은 레거시·기존 게임과 공용이라 여기서 클립 자체를 수정하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleLegacyAnimationEventReceiver : MonoBehaviour
{
    /// <summary>IdleAttackMelee, IdleAttackHand 클립의 Animation Event가 호출한다. 의도적으로 아무 일도 하지 않는다.</summary>
    public void AdabptDam()
    {
        // Battle 모듈의 피해 적용은 BattleBasicAttackController/BattleCardActionController에서
        // BattleDamageService.TryApplyDamage로 이미 처리한다. 여기서는 경고 제거 목적만 있다.
    }

    /// <summary>레거시 범위 공격 이벤트를 받되 피해는 Battle 파이프라인에 맡긴다.</summary>
    public void GetDamRange() { }

    /// <summary>레거시 단일 공격 이벤트를 받되 피해는 Battle 파이프라인에 맡긴다.</summary>
    public void GetDamSingle() { }

    /// <summary>레거시 스킬 종료 이벤트를 받되 Battle의 행동 상태는 변경하지 않는다.</summary>
    public void EndSkill() { }
}
