using System;
using UnityEngine;

/// <summary>
/// Player/Enemy가 이미 갖고 있는 레거시 Animator 연출(EnemyBattleAnim 등)을
/// Battle 모듈의 이동·공격 코드에서 그대로 재생하기 위한 공용 진입점이다.
/// 새 애니메이션 State를 만들지 않고, 기존 Animator Controller에 있는 State 이름만 재생한다.
/// 대상에 필요한 컴포넌트가 없으면 아무 효과 없이 조용히 넘어간다.
///
/// 캐릭터 하나에 Animator가 2개(이동용 + 전투용) 동시에 붙어있는 레거시 구조를 그대로 사용한다.
/// Battle 모듈은 항상 전투 상황에서만 호출되므로, Controller 이름에 "Battle"이 포함된 쪽을 우선 사용한다.
/// </summary>
public static class BattleCharacterAnimationBridge
{
    private const string IdleState = "Idle";
    private const string WalkState = "Walk";
    private const string DeathState = "Death";
    private const string DefaultAttackState = "IdleAttackMelee";

    /// <summary>
    /// 캐릭터에 붙은 여러 Animator 중 Controller 이름에 "Battle"이 포함된 전투용을 우선 반환한다.
    /// 전투용을 못 찾으면 붙어있는 첫 번째 Animator라도 반환해 최대한 연출이 끊기지 않게 한다.
    /// </summary>
    private static Animator ResolveBattleAnimator(GameObject character)
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
                candidate.runtimeAnimatorController.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidate;
            }
        }

        return animators[0];
    }

    /// <summary>
    /// EnemyBattleAnim은 내부적으로 자기 자신에서만 Animator를 찾는 레거시 코드라(자식 검색 없음),
    /// Cat_Player처럼 실제 Animator가 자식에 중첩된 구조에서는 내부 참조가 비어 예외를 던질 수 있다.
    /// 그런 경우 여기서 잡아서 호출부가 일반 Animator 경로로 안전하게 대체하도록 false를 반환한다.
    /// </summary>
    private static bool TryPlayLegacyEnemyAnim(System.Action legacyCall, GameObject character)
    {
        try
        {
            legacyCall();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"{character.name}: EnemyBattleAnim 연출 실패, 기본 Animator 재생으로 대체합니다. ({ex.GetType().Name})",
                character);
            return false;
        }
    }

    /// <summary>이동 시작 시 걷기 연출을 재생한다.</summary>
    public static void PlayWalk(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        EnemyBattleAnim enemyAnim = character.GetComponentInChildren<EnemyBattleAnim>(true);
        if (enemyAnim != null && TryPlayLegacyEnemyAnim(enemyAnim.Walk, character))
        {
            return;
        }

        Animator animator = ResolveBattleAnimator(character);
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

        EnemyBattleAnim enemyAnim = character.GetComponentInChildren<EnemyBattleAnim>(true);
        if (enemyAnim != null && TryPlayLegacyEnemyAnim(enemyAnim.Idle, character))
        {
            return;
        }

        Animator animator = ResolveBattleAnimator(character);
        if (animator != null)
        {
            animator.Play(IdleState);
        }
    }

    /// <summary>
    /// 기본 공격 연출을 재생한다. Enemy는 기존 EnemyBattleAnim의 랜덤 공격 모션을 그대로 쓰고,
    /// Player는 실제 스폰 프리팹에 무기 타입별 레거시 데이터(BattlePlayer)가 없어
    /// 항상 기본 근접 공격 State를 재생한다.
    /// </summary>
    public static void PlayAttack(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        EnemyBattleAnim enemyAnim = character.GetComponentInChildren<EnemyBattleAnim>(true);
        if (enemyAnim != null && TryPlayLegacyEnemyAnim(enemyAnim.Attack, character))
        {
            return;
        }

        Animator animator = ResolveBattleAnimator(character);
        if (animator == null)
        {
            return;
        }

        animator.Play(DefaultAttackState);
    }

    /// <summary>지정한 레거시 Animator State가 실제로 있을 때만 재생한다.</summary>
    public static bool PlayState(GameObject character, string stateName)
    {
        if (character == null || string.IsNullOrWhiteSpace(stateName)) return false;
        Animator animator = ResolveBattleAnimator(character);
        if (animator == null) return false;
        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash)) return false;
        animator.Play(stateHash);
        return true;
    }

    /// <summary>사망 연출을 재생한다. 사망 판정 자체는 이 메서드가 결정하지 않는다.</summary>
    public static void PlayDeath(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        EnemyBattleAnim enemyAnim = character.GetComponentInChildren<EnemyBattleAnim>(true);
        if (enemyAnim != null && TryPlayLegacyEnemyAnim(enemyAnim.Die, character))
        {
            return;
        }

        Animator animator = ResolveBattleAnimator(character);
        if (animator != null)
        {
            animator.Play(DeathState);
        }
    }
}
