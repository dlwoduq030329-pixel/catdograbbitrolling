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

    [Header("캐릭터별 턴 종료 이미지")]
    [Tooltip("캐릭터 선택 인덱스 0(Cat)에 적용할 턴 종료 버튼 이미지입니다.")]
    [SerializeField] private Sprite catTurnEndSprite;
    [Tooltip("캐릭터 선택 인덱스 1(Dog)에 적용할 턴 종료 버튼 이미지입니다.")]
    [SerializeField] private Sprite dogTurnEndSprite;
    [Tooltip("캐릭터 선택 인덱스 2(Bunny)에 적용할 턴 종료 버튼 이미지입니다.")]
    [SerializeField] private Sprite bunnyTurnEndSprite;

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

    /// <summary>
    /// SpawnPlayer의 캐릭터 선택 인덱스에 맞는 턴 종료 이미지를 버튼의 Target Image에 적용한다.
    /// 0=Cat, 1=Dog, 2=Bunny이며 알 수 없는 값이나 누락된 Sprite가 들어오면 현재 이미지를 유지한다.
    /// Player 등록 시 한 번만 호출되며 Update에서 캐릭터 정보를 반복 검색하지 않는다.
    /// </summary>
    public void ApplyTurnEndImageForCharacter(int characterIndex)
    {
        if (turnEndButton == null || turnEndButton.image == null)
        {
            Debug.LogError("캐릭터별 턴 종료 이미지를 적용할 Button/Image 참조가 없습니다.", this);
            return;
        }

        Sprite characterTurnEndSprite;
        switch (characterIndex)
        {
            case 0:
                characterTurnEndSprite = catTurnEndSprite;
                break;
            case 1:
                characterTurnEndSprite = dogTurnEndSprite;
                break;
            case 2:
                characterTurnEndSprite = bunnyTurnEndSprite;
                break;
            default:
                Debug.LogWarning($"지원하지 않는 캐릭터 인덱스입니다: {characterIndex}", this);
                return;
        }

        if (characterTurnEndSprite == null)
        {
            Debug.LogError($"캐릭터 {characterIndex}의 턴 종료 Sprite가 연결되지 않았습니다.", this);
            return;
        }

        turnEndButton.image.sprite = characterTurnEndSprite;
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
