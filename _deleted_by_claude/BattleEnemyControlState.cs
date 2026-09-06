using UnityEngine;

/// <summary>
/// 2026-09-05: 이 클래스는 더 이상 사용되지 않는다(dead code). 기절·속박을 BattleStatusEffects(공용
/// 상태이상 저장소)와 이중으로 관리하던 구조를 통합하면서, EnemyTurnActor·BattleCardActionController가
/// 모두 BattleStatusEffects만 읽고 쓰도록 바뀌었고 이 클래스를 참조하는 코드는 하나도 남지 않았다.
/// 이 세션에서는 파일 삭제 도구를 쓸 수 없어 코드만 비활성 상태로 남겨뒀다 — Unity 에디터에서
/// 이 BattleEnemyControlState.cs와 짝인 .meta 파일을 직접 삭제해도 된다. 다만 삭제 전에 프리팹에
/// 이 컴포넌트가 수동으로 붙어있는 곳이 있는지(EnemySpawner는 더 이상 런타임에 자동 추가하지 않음)
/// 한 번 확인해서, 붙어있다면 먼저 그 컴포넌트도 제거해 두는 게 안전하다.
/// 예전 문서(참고용, 더 이상 유효하지 않음): 밀치기 충돌 등으로 발생한 Enemy의 다음 턴 기절·속박
/// 상태를 보관하던 클래스였다.
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
