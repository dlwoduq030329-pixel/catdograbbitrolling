using UnityEngine;

/// <summary>밀치기 충돌 등으로 발생한 Enemy의 다음 턴 기절·속박 상태를 보관한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleEnemyControlState : MonoBehaviour
{
    [SerializeField, Min(0)] private int stunTurns;
    [SerializeField, Min(0)] private int rootTurns;

    public int StunTurns => stunTurns;
    public int RootTurns => rootTurns;
    public event System.Action<BattleEnemyControlState> Changed;

    public void ApplyStun(int turns)
    {
        stunTurns = Mathf.Max(stunTurns, Mathf.Max(0, turns));
        Changed?.Invoke(this);
    }

    public void ApplyRoot(int turns)
    {
        rootTurns = Mathf.Max(rootTurns, Mathf.Max(0, turns));
        Changed?.Invoke(this);
    }

    /// <summary>이번 Enemy 턴의 상태를 반환하고 지속 턴을 한 번 감소시킨다.</summary>
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
