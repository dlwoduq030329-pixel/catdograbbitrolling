using UnityEngine;

/// <summary>
/// Player와 Enemy의 이동·공격·사망 코드를 실제 Animator State 재생으로 연결하는 공용 진입점이다.
/// 행동 코드는 Animator가 어느 자식에 있는지 알 필요 없이 이 클래스에 캐릭터 루트만 전달한다.
/// 이 클래스는 애니메이션만 재생하며 이동, 공격 판정, 피해 적용, 사망 판정은 결정하지 않는다.
/// </summary>
public static class BattleCharacterAnimationBridge
{
    private const string IdleState = "Idle";
    private const string WalkState = "Walk";
    private const string DeathState = "Death";
    private const string DefaultAttackState = "IdleAttackMelee";

    /// <summary>
    /// 캐릭터 루트와 모든 자식에서 전투에 사용할 Animator를 찾는다.
    /// 모델 Animator가 캐릭터 루트가 아닌 자식 프리팹에 붙어 있으므로 자식까지 검색해야 한다.
    /// 여러 Animator가 있으면 Controller 이름에 "Battle"이 포함된 것을 우선하고,
    /// 전투 전용 Controller가 없을 때만 첫 번째 Animator를 대체 대상으로 사용한다.
/// </summary>
    private static Animator FindBattleAnimator(GameObject character)
    {
        // Cat_Player 등 스폰되는 루트 오브젝트 자체에는 Animator가 없고, 그 안에 중첩된
        // 실제 모델 프리팹(Ch_Cat_fix 등) 자식 오브젝트에 Animator가 붙어있는 구조라 자식까지 검색한다.
        Animator[] animators = character.GetComponentsInChildren<Animator>(true);
        if (animators.Length == 0)
        {
            return null;
        }

        foreach (Animator candidate in animators)
        {
            if (candidate != null &&
                candidate.runtimeAnimatorController != null &&
                candidate.runtimeAnimatorController.name.IndexOf(
                    "Battle",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidate;
            }
        }

        return animators[0];
    }

    /// <summary>이동 시작 시 걷기 연출을 재생한다.</summary>
    public static void PlayWalk(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        Animator animator = FindBattleAnimator(character);
        if (animator != null)
        {
            animator.Play(WalkState);
        }
    }

    /// <summary>이동이 끝나거나 공격 후 대기 상태로 돌아갈 때 재생한다.</summary>
    public static void PlayIdle(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        Animator animator = FindBattleAnimator(character);
        if (animator != null)
        {
            animator.Play(IdleState);
        }
    }

    /// <summary>
    /// 기본 공격 연출을 재생한다.
    /// 현재 Player와 Enemy 모두 같은 기본 근접 공격 State를 사용하며,
    /// 추후 무기·Enemy 종류별 공격 State가 확정되면 호출자가 State 이름을 명시하는 구조로 확장한다.
    /// </summary>
    public static void PlayAttack(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        Animator animator = FindBattleAnimator(character);
        if (animator == null)
        {
            return;
        }

        animator.Play(DefaultAttackState);
    }

    /// <summary>
    /// 호출자가 지정한 Animator State를 실제로 보유한 경우에만 재생한다.
    /// 카드 연출처럼 행동마다 State 이름이 달라지는 코드가 이 공용 함수를 사용한다.
    /// 반환값이 true면 State를 찾아 재생을 요청한 것이고, false면 캐릭터·이름·Animator·State 중 하나가 없다는 뜻이다.
    /// 이 반환값을 이용하면 호출자가 기본 공격 애니메이션이나 VFX 같은 대체 연출을 선택할 수 있다.
    /// </summary>
    public static bool PlayState(GameObject character, string stateName)
    {
        if (character == null || string.IsNullOrWhiteSpace(stateName)) return false;
        Animator animator = FindBattleAnimator(character);
        if (animator == null) return false;
        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash)) return false;
        animator.Play(stateHash);
        return true;
    }

    /// <summary>
    /// 사망 Animator State를 재생하고 재생 요청 성공 여부를 반환한다.
    /// 사망 판정이나 오브젝트 제거는 담당하지 않는다.
    /// 호출자는 반환값과 별개로 사망 VFX를 재생할 수 있고, false일 때 대체 연출을 선택할 수도 있다.
    /// </summary>
    public static bool PlayDeath(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        return PlayState(character, DeathState);
    }
}
