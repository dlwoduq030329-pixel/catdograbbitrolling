using UnityEngine;

/// <summary>
/// 밀치기 충돌 등으로 발생한 Enemy의 다음 턴 기절·속박 상태를 보관한다.
/// 주의(2026-08-21 2차 검증): 이 클래스는 Combat/BattleStatusEffects.cs와 상태가 이중화되어 있다.
/// BattleCardActionController.ApplyStatusToUnit()이 기절·속박을 걸 때마다 BattleStatusEffects.Apply()와
/// 이 클래스의 ApplyStun/ApplyRoot에 항상 동시에 기록하지만, 실제 턴 스킵 판정(EnemyTurnActor.TakeTurn)은
/// 이 클래스만 읽는다. 두 저장소의 갱신 규칙도 다르다 — BattleStatusEffects는 재적용 시 지속 턴을
/// 더하고, 이 클래스는 Mathf.Max로 더 큰 값만 남긴다. 감소 시점도 BattleStatusEffects는 "플레이어 턴 시작마다",
/// 이 클래스는 "그 Enemy 자신의 턴이 돌 때"로 서로 달라 표시값과 실제 동작이 어긋날 수 있다.
/// Combat 폴더 리뷰 차례에 두 저장소를 하나로 통합하기 전까지는 이 불일치를 인지하고 있어야 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyControlState : MonoBehaviour
{
    [SerializeField, Min(0)] private int stunTurns;
    [SerializeField, Min(0)] private int rootTurns;

    public int StunTurns => stunTurns;
    public int RootTurns => rootTurns;
    public event System.Action<BattleEnemyControlState> Changed;

    /// <summary>기절 지속 턴을 적용한다. 이미 남아있는 지속 턴보다 클 때만 값을 늘린다(누적 합산이 아닌 최댓값 갱신 — BattleStatusEffects.Apply()의 합산 방식과 다르다).</summary>
    public void ApplyStun(int turns)
    {
        stunTurns = Mathf.Max(stunTurns, Mathf.Max(0, turns));
        Changed?.Invoke(this);
    }

    /// <summary>속박 지속 턴을 적용한다. 이미 남아있는 지속 턴보다 클 때만 값을 늘린다(누적 합산이 아닌 최댓값 갱신 — BattleStatusEffects.Apply()의 합산 방식과 다르다).</summary>
    public void ApplyRoot(int turns)
    {
        rootTurns = Mathf.Max(rootTurns, Mathf.Max(0, turns));
        Changed?.Invoke(this);
    }

    /// <summary>
    /// 이번 Enemy 턴의 상태를 반환하고 지속 턴을 한 번 감소시킨다.
    /// 호출 시점은 EnemyTurnActor.TakeTurn() 시작 부분 한 곳뿐이며(BattleEnemyTurnRunner가 그 Enemy의
    /// 공식 차례를 돌릴 때만 호출), 넉백 등 다른 이동 경로는 이 메서드를 호출하지 않는다.
    /// 기절 중이어도 턴 수는 먼저 깎이며, 호출부는 isStunned가 true면 실제 이동·공격을 건너뛴다.
    /// </summary>
    public void ConsumeTurn(out bool isStunned, out bool isRooted)
    {
        isStunned = stunTurns > 0;
        isRooted = rootTurns > 0;
        if (stunTurns > 0)
        {
            stunTurns--;
        }
        if (rootTurns > 0)
        {
            rootTurns--;
        }
        Changed?.Invoke(this);
    }
}
