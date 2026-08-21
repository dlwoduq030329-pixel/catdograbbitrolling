using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 턴 종료 버튼의 클릭 이벤트와 사용 가능 상태만 관리한다.
/// 주사위 굴리기는 더 이상 이 버튼이 담당하지 않는다 — 전용 주사위 버튼(BattleDiceRollButton 등)에서 처리한다.
/// 실제 턴 전환은 BattleGameManager에 위임한다.
/// 턴 종료는 버튼 클릭 또는 단축키(기본 E)로 가능하다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleTurnButtonController : MonoBehaviour
{
    [Header("턴 화면 참조")]
    [SerializeField] private Button turnEndButton;
    [SerializeField] private Button diceButton;
    [SerializeField] private BattleCardPanelToggle cardPanel;

    [Header("입력")]
    [InspectorName("턴 종료 단축키")]
    [SerializeField] private KeyCode endTurnKey = KeyCode.E;

    private UnityAction endTurnAction;
    private UnityAction rollDiceAction;

    private void OnEnable()
    {
        RegisterRuntimeListener();
    }

    /// <summary>E(기본) 단축키로 턴 종료를 실행한다.</summary>
    private void Update()
    {
        if (turnEndButton == null ||
            !turnEndButton.gameObject.activeInHierarchy ||
            !turnEndButton.interactable)
        {
            return;
        }

        if (BattleGameManager.Instance != null && BattleGameManager.Instance.IsModalInteractionOpen)
        {
            return;
        }

        if (Input.GetKeyDown(endTurnKey))
        {
            endTurnAction?.Invoke();
        }
    }

    /// <summary>턴 종료 버튼에 실행 콜백을 연결한다.</summary>
    public void Bind(UnityAction onEndTurn, UnityAction onRollDice)
    {
        RemoveRuntimeListeners();

        endTurnAction = onEndTurn;
        rollDiceAction = onRollDice;

        if (turnEndButton != null)
        {
            BattlePointerSelectionClearer.Ensure(turnEndButton.gameObject);
        }

        if (diceButton != null)
        {
            BattlePointerSelectionClearer.Ensure(diceButton.gameObject);
        }

        RegisterRuntimeListener();
    }

    /// <summary>Manager가 전달한 턴 상태에 맞춰 턴 종료·주사위 버튼을 갱신한다.</summary>
    public void ApplyTurnState(
        bool isPlayerTurn,
        bool hasRolledDice,
        bool battleStopped,
        bool overlayOpen)
    {
        if (turnEndButton != null)
        {
            turnEndButton.gameObject.SetActive(isPlayerTurn);
            turnEndButton.interactable = isPlayerTurn && !battleStopped && !overlayOpen;
        }

        if (diceButton != null && diceButton != turnEndButton)
        {
            bool canRoll = isPlayerTurn && !hasRolledDice && !battleStopped;
            diceButton.gameObject.SetActive(canRoll);
            diceButton.interactable = canRoll && !overlayOpen;
        }
    }

    public void ShowCardPanel() => cardPanel?.Show();
    public void HideCardPanel() => cardPanel?.Hide();

    /// <summary>Player 사망 등으로 전투가 중지됐을 때 모든 턴 입력을 즉시 비활성화한다.</summary>
    public void DisableAllInput()
    {
        if (turnEndButton != null) turnEndButton.interactable = false;
        if (diceButton != null) diceButton.interactable = false;
    }

    private void ExecuteEndTurn()
    {
        endTurnAction?.Invoke();
    }

    private void RegisterRuntimeListener()
    {
        if (turnEndButton != null)
        {
            turnEndButton.onClick.RemoveListener(ExecuteEndTurn);
            turnEndButton.onClick.AddListener(ExecuteEndTurn);
        }

        if (diceButton != null && diceButton != turnEndButton &&
            diceButton.GetComponent<BattleDiceRollButton>() == null)
        {
            diceButton.onClick.RemoveListener(ExecuteRollDice);
            diceButton.onClick.AddListener(ExecuteRollDice);
        }
    }

    private void ExecuteRollDice()
    {
        rollDiceAction?.Invoke();
    }

    private void OnDestroy()
    {
        RemoveRuntimeListeners();
    }

    private void RemoveRuntimeListeners()
    {
        if (turnEndButton != null)
        {
            turnEndButton.onClick.RemoveListener(ExecuteEndTurn);
        }


        if (diceButton != null)
        {
            diceButton.onClick.RemoveListener(ExecuteRollDice);
        }
    }
}
