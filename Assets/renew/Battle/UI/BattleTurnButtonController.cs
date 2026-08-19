using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 주사위 버튼과 턴 종료 버튼의 클릭 이벤트 및 사용 가능 상태를 관리한다.
/// 실제 주사위 계산과 턴 전환은 BattleGameManager에 위임한다.
/// 주사위는 마우스 클릭으로만 굴릴 수 있고, 턴 종료는 버튼 클릭 또는 단축키(기본 E)로 가능하다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleTurnButtonController : MonoBehaviour
{
    [Header("입력")]
    [InspectorName("턴 종료 단축키")]
    [SerializeField] private KeyCode endTurnKey = KeyCode.E;

    private Button actionButton;
    private Image actionImage;
    private Sprite diceSprite;
    private Sprite turnEndSprite;
    private UnityAction endTurnAction;
    private UnityAction rollDiceAction;
    private bool isDiceAction;

    private void OnEnable()
    {
        RegisterRuntimeListener();
    }

    /// <summary>E(기본) 단축키로 턴 종료만 실행한다. 주사위는 이 경로로 굴릴 수 없다(마우스 전용).</summary>
    private void Update()
    {
        if (actionButton == null ||
            !actionButton.gameObject.activeInHierarchy ||
            !actionButton.interactable)
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

    /// <summary>공용 행동 버튼에 주사위·턴 종료 이미지와 실행 콜백을 연결한다.</summary>
    public void Bind(
        Button targetActionButton,
        Sprite targetDiceSprite,
        Sprite targetTurnEndSprite,
        UnityAction onEndTurn,
        UnityAction onRollDice)
    {
        RemoveRuntimeListeners();

        actionButton = targetActionButton;
        actionImage = actionButton != null ? actionButton.targetGraphic as Image : null;
        if (actionImage == null && actionButton != null)
        {
            actionImage = actionButton.GetComponent<Image>();
        }

        diceSprite = targetDiceSprite;
        turnEndSprite = targetTurnEndSprite;
        endTurnAction = onEndTurn;
        rollDiceAction = onRollDice;

        if (actionButton != null)
        {
            BattlePointerSelectionClearer.Ensure(actionButton.gameObject);
            RegisterRuntimeListener();
        }
    }

    /// <summary>현재 턴과 주사위 실행 여부에 따라 버튼 기능·이미지·활성 상태를 갱신한다.</summary>
    public void ApplyTurnState(bool isPlayerTurn, bool hasRolledDice)
    {
        if (actionButton == null)
        {
            return;
        }

        actionButton.gameObject.SetActive(isPlayerTurn);
        actionButton.interactable = isPlayerTurn;
        isDiceAction = !hasRolledDice;

        if (actionImage != null)
        {
            Sprite targetSprite = isDiceAction ? diceSprite : turnEndSprite;
            if (targetSprite != null)
            {
                actionImage.sprite = targetSprite;
            }
        }
    }

    private void ExecuteCurrentAction()
    {
        if (isDiceAction)
        {
            rollDiceAction?.Invoke();
            return;
        }

        endTurnAction?.Invoke();
    }

    private void RegisterRuntimeListener()
    {
        if (actionButton == null)
        {
            return;
        }

        // Canvas 활성화 순서가 바뀌어도 중복 없이 공용 버튼 동작을 복구한다.
        actionButton.onClick.RemoveListener(ExecuteCurrentAction);
        actionButton.onClick.AddListener(ExecuteCurrentAction);
    }

    private void OnDestroy()
    {
        RemoveRuntimeListeners();
    }

    private void RemoveRuntimeListeners()
    {
        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(ExecuteCurrentAction);
        }
    }
}
