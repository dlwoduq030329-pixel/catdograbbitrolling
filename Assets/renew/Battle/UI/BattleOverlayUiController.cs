using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 위에 상점·보상·상태창·턴 안내가 표시될 때 전투 입력과 HUD 표시를 제어한다.
/// 턴 순서나 게임 규칙은 알지 못하며, BattleGameManager가 현재 Player 턴 여부만 전달한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleOverlayUiController : MonoBehaviour
{
    [Header("전투 입력")]
    [SerializeField] private BattlePlayerActionController playerInput;
    [SerializeField] private CanvasGroup battleHudInput;

    [Header("상점이 열릴 때 숨길 UI")]
    [SerializeField] private Button turnEndButton;
    [SerializeField] private PlayerMPUI playerMpView;
    [SerializeField] private BattleCardPanelToggle cardPanel;
    [SerializeField] private GameObject hudHiddenWhileShopIsOpen;
    [SerializeField] private GameObject[] uiHiddenWhileShopIsOpen;

    private readonly List<bool> savedShopUiActiveStates = new List<bool>();
    private int openOverlayCount;
    private bool hudWasInteractable = true;
    private bool hudWasBlockingRaycasts = true;

    /// <summary>현재 열린 오버레이 수다. 둘 이상의 UI가 겹쳐 열려도 마지막 UI가 닫힐 때까지 입력을 잠근다.</summary>
    public bool IsOverlayOpen => openOverlayCount > 0;

    /// <summary>오버레이 하나가 열렸음을 기록하고 Player·카메라·HUD 입력을 잠근다.</summary>
    public void LockBattleInput()
    {
        openOverlayCount++;
        playerInput?.SetBattleInputEnabled(false);
        BattleMapCameraInput.SetEnabledOnMainCamera(false);

        if (openOverlayCount != 1 || battleHudInput == null)
        {
            return;
        }

        hudWasInteractable = battleHudInput.interactable;
        hudWasBlockingRaycasts = battleHudInput.blocksRaycasts;
        battleHudInput.interactable = false;
        battleHudInput.blocksRaycasts = false;
    }

    /// <summary>오버레이 하나가 닫혔음을 기록하고, 마지막 오버레이가 닫혔을 때만 전투 입력을 복구한다.</summary>
    public void UnlockBattleInput(bool enablePlayerTurnInput, bool battleIsStopped)
    {
        openOverlayCount = Mathf.Max(0, openOverlayCount - 1);
        if (openOverlayCount > 0 || battleIsStopped)
        {
            return;
        }

        playerInput?.SetBattleInputEnabled(enablePlayerTurnInput);
        BattleMapCameraInput.SetEnabledOnMainCamera(enablePlayerTurnInput);

        if (battleHudInput != null)
        {
            battleHudInput.interactable = hudWasInteractable;
            battleHudInput.blocksRaycasts = hudWasBlockingRaycasts;
        }
    }

    /// <summary>
    /// 상점이 열릴 때 명시적으로 등록된 전투 UI를 숨기고, 닫힐 때 각 UI의 이전 활성 상태를 복원한다.
    /// Scene 전체 Canvas를 검색하지 않으므로 Inspector 목록이 이 동작의 유일한 기준이다.
    /// </summary>
    public void SetShopOpen(bool shopIsOpen)
    {
        if (shopIsOpen)
        {
            SaveAndHideShopBackgroundUi();
            return;
        }

        RestoreShopBackgroundUi();
    }

    /// <summary>강제 종료 시 중첩 수를 비우고 HUD 입력 상태를 복원한다.</summary>
    public void Reset()
    {
        openOverlayCount = 0;
        if (battleHudInput != null)
        {
            battleHudInput.interactable = hudWasInteractable;
            battleHudInput.blocksRaycasts = hudWasBlockingRaycasts;
        }
    }

    private void SaveAndHideShopBackgroundUi()
    {
        if (savedShopUiActiveStates.Count > 0)
        {
            return;
        }

        SetActiveAndRemember(turnEndButton != null ? turnEndButton.gameObject : null);
        SetActiveAndRemember(playerMpView != null ? playerMpView.gameObject : null);
        SetActiveAndRemember(cardPanel != null ? cardPanel.gameObject : null);
        SetActiveAndRemember(hudHiddenWhileShopIsOpen);

        if (uiHiddenWhileShopIsOpen == null)
        {
            return;
        }

        for (int i = 0; i < uiHiddenWhileShopIsOpen.Length; i++)
        {
            SetActiveAndRemember(uiHiddenWhileShopIsOpen[i]);
        }
    }

    private void SetActiveAndRemember(GameObject target)
    {
        if (target == null)
        {
            savedShopUiActiveStates.Add(false);
            return;
        }

        savedShopUiActiveStates.Add(target.activeSelf);
        target.SetActive(false);
    }

    private void RestoreShopBackgroundUi()
    {
        if (savedShopUiActiveStates.Count == 0)
        {
            return;
        }

        int savedIndex = 0;
        RestoreSavedState(turnEndButton != null ? turnEndButton.gameObject : null, savedIndex++);
        RestoreSavedState(playerMpView != null ? playerMpView.gameObject : null, savedIndex++);
        RestoreSavedState(cardPanel != null ? cardPanel.gameObject : null, savedIndex++);
        RestoreSavedState(hudHiddenWhileShopIsOpen, savedIndex++);

        if (uiHiddenWhileShopIsOpen != null)
        {
            for (int i = 0; i < uiHiddenWhileShopIsOpen.Length; i++)
            {
                RestoreSavedState(uiHiddenWhileShopIsOpen[i], savedIndex++);
            }
        }

        savedShopUiActiveStates.Clear();
    }

    private void RestoreSavedState(GameObject target, int savedIndex)
    {
        if (target != null && savedIndex < savedShopUiActiveStates.Count)
        {
            target.SetActive(savedShopUiActiveStates[savedIndex]);
        }
    }
}
