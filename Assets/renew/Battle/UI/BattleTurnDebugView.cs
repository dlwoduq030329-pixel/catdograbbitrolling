using TMPro;
using UnityEngine;

/// <summary>
/// 현재 턴 번호와 주사위 값을 디버그 텍스트에 표시한다.
/// 턴 상태 변경과 주사위 계산은 담당하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleTurnDebugView : MonoBehaviour
{
    private TMP_Text turnText;
    private TMP_Text diceText;

    /// <summary>기존 BattleGameManager Inspector의 텍스트 참조를 전달받는다.</summary>
    public void Configure(TMP_Text targetTurnText, TMP_Text targetDiceText)
    {
        turnText = targetTurnText;
        diceText = targetDiceText;
    }

    /// <summary>현재 턴 번호를 표시한다.</summary>
    public void ShowTurn(int turnNumber)
    {
        if (turnText != null)
        {
            turnText.text = $"TURN {turnNumber}";
        }
    }

    /// <summary>현재 주사위 값을 표시한다.</summary>
    public void ShowDice(int diceValue)
    {
        if (diceText != null)
        {
            diceText.text = $"DICE {diceValue}";
        }
    }
}
