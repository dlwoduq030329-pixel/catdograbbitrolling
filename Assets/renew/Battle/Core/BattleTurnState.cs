using UnityEngine;

/// <summary>현재 턴 번호와 Player/Enemy 턴 상태를 보관합니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleTurnState : MonoBehaviour
{
    [SerializeField, Min(1)] private int turn = 1;
    [SerializeField] private bool isPlayerTurn = true;

    public int Turn => turn;
    public bool IsPlayerTurn => isPlayerTurn;

    public void ResetTurn()
    {
        turn = Mathf.Max(1, turn);
        isPlayerTurn = true;
    }

    public bool TryStartEnemyTurn()
    {
        if (!isPlayerTurn)
            return false;

        isPlayerTurn = false;
        turn++;
        return true;
    }

    public void StartPlayerTurn()
    {
        isPlayerTurn = true;
    }

    public void SkipPlayerTurn()
    {
        isPlayerTurn = false;
        turn++;
    }
}
