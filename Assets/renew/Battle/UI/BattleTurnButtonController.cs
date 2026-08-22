using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 턴 종료 버튼 클릭, 턴 종료 단축키, 턴 종료 버튼의 표시·활성 상태만 관리한다.
/// 실제 종료 가능 조건과 턴 전환은 BattleGameManager가 판단하며, 주사위와 카드 패널은 각 전용 UI가 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleTurnButtonController : MonoBehaviour
{
    [Header("턴 종료 UI")]
    [Tooltip("플레이어 턴에 표시하고 클릭 시 BattleGameManager.EndTurn을 요청할 버튼입니다.")]
    [SerializeField] private Button turnEndButton;

    [Header("입력")]
    [InspectorName("턴 종료 단축키")]
    [Tooltip("턴 종료 버튼과 동일한 요청을 실행하는 키입니다. 실제 종료 가능 여부는 BattleGameManager가 다시 검사합니다.")]
    [SerializeField] private KeyCode endTurnKey = KeyCode.E;

    private UnityAction requestEndTurn;

    private void OnEnable()
    {
        RegisterTurnEndButtonListener();
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

        if (Input.GetKeyDown(endTurnKey))
        {
            requestEndTurn?.Invoke();
        }
    }

    /// <summary>턴 종료 버튼에 실행 콜백을 연결한다.</summary>
    public void BindEndTurnAction(UnityAction onEndTurnRequested)
    {
        RemoveTurnEndButtonListener();

        requestEndTurn = onEndTurnRequested;

        if (turnEndButton != null)
        {
            BattlePointerSelectionClearer.Ensure(turnEndButton.gameObject);
        }

        RegisterTurnEndButtonListener();
    }

    /// <summary>Manager가 전달한 상태에 맞춰 턴 종료 버튼의 표시 여부와 클릭 가능 여부만 갱신한다.</summary>
    public void ApplyTurnEndButtonState(
        bool isPlayerTurn,
        bool battleStopped,
        bool overlayOpen)
    {
        if (turnEndButton != null)
        {
            turnEndButton.gameObject.SetActive(isPlayerTurn);
            turnEndButton.interactable = isPlayerTurn && !battleStopped && !overlayOpen;
        }

    }

    /// <summary>Player 사망 등으로 전투가 중지됐을 때 모든 턴 입력을 즉시 비활성화한다.</summary>
    public void DisableTurnEndInput()
    {
        if (turnEndButton != null) turnEndButton.interactable = false;
    }

    private void ExecuteEndTurn()
    {
        requestEndTurn?.Invoke();
    }

    private void RegisterTurnEndButtonListener()
    {
        if (turnEndButton != null)
        {
            turnEndButton.onClick.RemoveListener(ExecuteEndTurn);
            turnEndButton.onClick.AddListener(ExecuteEndTurn);
        }
    }

    private void OnDestroy()
    {
        RemoveTurnEndButtonListener();
    }

    private void RemoveTurnEndButtonListener()
    {
        if (turnEndButton != null)
        {
            turnEndButton.onClick.RemoveListener(ExecuteEndTurn);
        }
    }
}
