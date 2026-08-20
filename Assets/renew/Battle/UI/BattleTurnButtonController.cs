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
    [Header("입력")]
    [InspectorName("턴 종료 단축키")]
    [SerializeField] private KeyCode endTurnKey = KeyCode.E;

    private Button turnEndButton;
    private UnityAction endTurnAction;

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
    public void Bind(Button targetTurnEndButton, UnityAction onEndTurn)
    {
        RemoveRuntimeListeners();

        turnEndButton = targetTurnEndButton;
        endTurnAction = onEndTurn;

        if (turnEndButton != null)
        {
            BattlePointerSelectionClearer.Ensure(turnEndButton.gameObject);
            RegisterRuntimeListener();
        }
    }

    /// <summary>현재 플레이어 턴 여부에 맞춰 턴 종료 버튼의 활성·사용 가능 상태를 갱신한다.</summary>
    public void ApplyTurnState(bool isPlayerTurn)
    {
        if (turnEndButton == null)
        {
            return;
        }

        turnEndButton.gameObject.SetActive(isPlayerTurn);
        turnEndButton.interactable = isPlayerTurn;
    }

    private void ExecuteEndTurn()
    {
        endTurnAction?.Invoke();
    }

    private void RegisterRuntimeListener()
    {
        if (turnEndButton == null)
        {
            return;
        }

        // Canvas 활성화 순서가 바뀌어도 중복 없이 버튼 동작을 복구한다.
        turnEndButton.onClick.RemoveListener(ExecuteEndTurn);
        turnEndButton.onClick.AddListener(ExecuteEndTurn);
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
    }
}
