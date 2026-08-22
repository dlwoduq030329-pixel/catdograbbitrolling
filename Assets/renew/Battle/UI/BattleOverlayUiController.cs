using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 전투 위에 상점·보상·상태창·턴 안내가 표시될 때 전투 입력과 HUD 표시를 제어한다.
/// 턴 순서나 게임 규칙은 알지 못하며, BattleGameManager가 현재 Player 턴 여부만 전달한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleOverlayUiController : MonoBehaviour
{
    [Header("전투 입력")]
    [FormerlySerializedAs("playerInput")]
    [SerializeField] private BattlePlayerActionController playerActionInput;
    [FormerlySerializedAs("battleHudInput")]
    [SerializeField] private CanvasGroup battleHudInputGroup;

    [Header("상점이 열릴 때 숨길 UI")]
    [FormerlySerializedAs("turnEndButton")]
    [SerializeField] private Button turnEndButtonHiddenByShop;
    [FormerlySerializedAs("playerMpView")]
    [SerializeField] private PlayerMPUI playerManaViewHiddenByShop;
    [FormerlySerializedAs("cardPanel")]
    [SerializeField] private BattleCardPanelToggle cardHandHiddenByShop;
    [FormerlySerializedAs("hudHiddenWhileShopIsOpen")]
    [SerializeField] private GameObject battleHudRootHiddenByShop;
    [FormerlySerializedAs("uiHiddenWhileShopIsOpen")]
    [SerializeField] private GameObject[] additionalUiHiddenByShop;

    private readonly List<bool> activeStatesBeforeShopOpened = new List<bool>();
    private int activeInputBlockingOverlayCount;
    private bool hudWasInteractableBeforeFirstOverlay = true;
    private bool hudBlockedRaycastsBeforeFirstOverlay = true;

    /// <summary>전투 입력을 막는 오버레이가 하나 이상 열려 있는지 반환한다.</summary>
    public bool IsOverlayOpen => activeInputBlockingOverlayCount > 0;

    /// <summary>
    /// 상점·보상·상태창 등 전투 입력을 막는 오버레이 하나가 열렸음을 기록한다.
    /// Player 조작과 카메라는 매 호출에서 잠그고, HUD CanvasGroup의 기존 입력 상태는 첫 번째 오버레이가
    /// 열릴 때만 저장한 뒤 잠근다. 중첩된 오버레이가 있으면 마지막 UI가 닫힐 때까지 잠금이 유지된다.
    /// </summary>
    public void RegisterOpenedOverlayAndLockInput()
    {
        activeInputBlockingOverlayCount++;
        playerActionInput?.SetBattleInputEnabled(false);
        BattleMapCameraInput.SetEnabledOnMainCamera(false);

        if (activeInputBlockingOverlayCount != 1 || battleHudInputGroup == null)
        {
            return;
        }

        hudWasInteractableBeforeFirstOverlay = battleHudInputGroup.interactable;
        hudBlockedRaycastsBeforeFirstOverlay = battleHudInputGroup.blocksRaycasts;
        battleHudInputGroup.interactable = false;
        battleHudInputGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 오버레이 하나가 닫혔음을 기록한다. 다른 오버레이가 남아 있거나 전투가 정지됐다면 잠금을 유지하고,
    /// 마지막 오버레이가 닫힌 경우에만 현재 Player 턴 여부에 맞춰 Player·카메라 입력을 복구한다.
    /// HUD CanvasGroup은 첫 번째 오버레이가 열리기 직전의 interactable/raycast 상태로 정확히 되돌린다.
    /// </summary>
    public void RegisterClosedOverlayAndRestoreInput(
        bool shouldEnablePlayerTurnInput,
        bool battleIsStopped)
    {
        activeInputBlockingOverlayCount = Mathf.Max(0, activeInputBlockingOverlayCount - 1);
        if (activeInputBlockingOverlayCount > 0 || battleIsStopped)
        {
            return;
        }

        playerActionInput?.SetBattleInputEnabled(shouldEnablePlayerTurnInput);
        BattleMapCameraInput.SetEnabledOnMainCamera(shouldEnablePlayerTurnInput);

        if (battleHudInputGroup != null)
        {
            battleHudInputGroup.interactable = hudWasInteractableBeforeFirstOverlay;
            battleHudInputGroup.blocksRaycasts = hudBlockedRaycastsBeforeFirstOverlay;
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
            SaveAndHideUiBehindShop();
            return;
        }

        RestoreUiHiddenByShop();
    }

    /// <summary>
    /// 전투 종료·사망처럼 정상적인 오버레이 Close 호출을 기다릴 수 없는 상황에서 중첩 수를 즉시 비우고,
    /// HUD CanvasGroup을 첫 오버레이가 열리기 전 상태로 복원한다.
    /// </summary>
    public void ResetOverlayInputState()
    {
        activeInputBlockingOverlayCount = 0;
        if (battleHudInputGroup != null)
        {
            battleHudInputGroup.interactable = hudWasInteractableBeforeFirstOverlay;
            battleHudInputGroup.blocksRaycasts = hudBlockedRaycastsBeforeFirstOverlay;
        }
    }

    /// <summary>
    /// 상점 뒤에서 보이거나 입력되면 안 되는 UI들의 현재 activeSelf를 정해진 순서대로 저장하고 숨긴다.
    /// 이미 상태를 저장한 상태에서 다시 호출되면 기존 원본 상태가 덮이지 않도록 아무 작업도 하지 않는다.
    /// </summary>
    private void SaveAndHideUiBehindShop()
    {
        if (activeStatesBeforeShopOpened.Count > 0)
        {
            return;
        }

        RememberActiveStateAndHide(
            turnEndButtonHiddenByShop != null ? turnEndButtonHiddenByShop.gameObject : null);
        RememberActiveStateAndHide(
            playerManaViewHiddenByShop != null ? playerManaViewHiddenByShop.gameObject : null);
        RememberActiveStateAndHide(
            cardHandHiddenByShop != null ? cardHandHiddenByShop.gameObject : null);
        RememberActiveStateAndHide(battleHudRootHiddenByShop);

        if (additionalUiHiddenByShop == null)
        {
            return;
        }

        for (int uiIndex = 0; uiIndex < additionalUiHiddenByShop.Length; uiIndex++)
        {
            RememberActiveStateAndHide(additionalUiHiddenByShop[uiIndex]);
        }
    }

    /// <summary>
    /// 대상의 상점 진입 전 activeSelf를 복원 목록에 추가한 뒤 비활성화한다.
    /// null도 false 한 칸으로 기록해 저장 목록의 인덱스가 Inspector 대상 순서와 어긋나지 않게 한다.
    /// </summary>
    private void RememberActiveStateAndHide(GameObject uiObject)
    {
        if (uiObject == null)
        {
            activeStatesBeforeShopOpened.Add(false);
            return;
        }

        activeStatesBeforeShopOpened.Add(uiObject.activeSelf);
        uiObject.SetActive(false);
    }

    /// <summary>
    /// 상점 진입 때 사용한 것과 같은 대상 순서로 저장된 activeSelf 값을 되돌린다.
    /// 모든 대상 복원이 끝나면 다음 상점 진입에서 새 상태를 저장할 수 있도록 목록을 비운다.
    /// </summary>
    private void RestoreUiHiddenByShop()
    {
        if (activeStatesBeforeShopOpened.Count == 0)
        {
            return;
        }

        int savedStateIndex = 0;
        RestoreSavedActiveState(
            turnEndButtonHiddenByShop != null ? turnEndButtonHiddenByShop.gameObject : null,
            savedStateIndex++);
        RestoreSavedActiveState(
            playerManaViewHiddenByShop != null ? playerManaViewHiddenByShop.gameObject : null,
            savedStateIndex++);
        RestoreSavedActiveState(
            cardHandHiddenByShop != null ? cardHandHiddenByShop.gameObject : null,
            savedStateIndex++);
        RestoreSavedActiveState(battleHudRootHiddenByShop, savedStateIndex++);

        if (additionalUiHiddenByShop != null)
        {
            for (int uiIndex = 0; uiIndex < additionalUiHiddenByShop.Length; uiIndex++)
            {
                RestoreSavedActiveState(
                    additionalUiHiddenByShop[uiIndex],
                    savedStateIndex++);
            }
        }

        activeStatesBeforeShopOpened.Clear();
    }

    /// <summary>저장 목록에 해당 인덱스가 있을 때만 UI 오브젝트의 상점 진입 전 활성 상태를 복원한다.</summary>
    private void RestoreSavedActiveState(GameObject uiObject, int savedStateIndex)
    {
        if (uiObject != null && savedStateIndex < activeStatesBeforeShopOpened.Count)
        {
            uiObject.SetActive(activeStatesBeforeShopOpened[savedStateIndex]);
        }
    }
}
