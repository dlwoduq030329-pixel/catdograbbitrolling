using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 이동, 공격, 카드 행동에서 공용으로 사용하는 확인·취소 버튼과 안내 문구를 표시한다.
/// 어떤 행동을 확정할지는 판단하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleActionConfirmView : MonoBehaviour
{
    private const string DefaultButtonGroupName = "Move Bu";
    private const string DefaultConfirmButtonName = "Con";
    private const string DefaultCancelButtonName = "Quit";
    private const string DefaultMessageTextName = "ExplaneText";

    private Button confirmButton;
    private Button cancelButton;
    private GameObject buttonGroup;
    private TMP_Text messageText;
    private UnityAction confirmAction;
    private UnityAction cancelAction;

    public Button ConfirmButton => confirmButton;
    public Button CancelButton => cancelButton;
    public GameObject ButtonGroup => buttonGroup;
    public TMP_Text MessageText => messageText;

    /// <summary>공용 Confirm/Quit UI 참조와 현재 행동이 실행할 콜백을 교체 연결한다.</summary>
    public void Bind(
        Button targetConfirmButton,
        Button targetCancelButton,
        GameObject targetButtonGroup,
        TMP_Text targetMessageText,
        UnityAction onConfirm,
        UnityAction onCancel)
    {
        RemoveRuntimeListeners();

        confirmButton = targetConfirmButton;
        cancelButton = targetCancelButton;
        buttonGroup = targetButtonGroup;
        messageText = targetMessageText;
        confirmAction = onConfirm;
        cancelAction = onCancel;

        ResolveMissingReferences();

        if (confirmButton != null && confirmAction != null)
        {
            BattlePointerSelectionClearer.Ensure(confirmButton.gameObject);
            confirmButton.onClick.AddListener(confirmAction);
        }

        if (cancelButton != null && cancelAction != null)
        {
            BattlePointerSelectionClearer.Ensure(cancelButton.gameObject);
            cancelButton.onClick.AddListener(cancelAction);
        }
    }

    /// <summary>확정 대기 중일 때만 버튼 그룹을 표시하며 행동 규칙 자체는 판단하지 않는다.</summary>
    public void SetVisible(bool visible)
    {
        if (buttonGroup != null)
        {
            buttonGroup.SetActive(visible);
            return;
        }

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(visible);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(visible);
        }
    }

    /// <summary>현재 확인 대상에 맞는 안내 문구를 null 안전하게 표시한다.</summary>
    public void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
        }
    }

    private void OnDestroy()
    {
        RemoveRuntimeListeners();
    }

    private void RemoveRuntimeListeners()
    {
        if (confirmButton != null && confirmAction != null)
        {
            confirmButton.onClick.RemoveListener(confirmAction);
        }

        if (cancelButton != null && cancelAction != null)
        {
            cancelButton.onClick.RemoveListener(cancelAction);
        }
    }

    /// <summary>Inspector 참조가 없는 이전 씬에서만 이름 기반으로 공용 확인 UI를 보완한다.</summary>
    private void ResolveMissingReferences()
    {
        if (buttonGroup == null)
        {
            Transform groupTransform = FindTransformByName(DefaultButtonGroupName);
            buttonGroup = groupTransform != null ? groupTransform.gameObject : null;
        }

        if (confirmButton == null)
        {
            confirmButton = FindComponentByName<Button>(DefaultConfirmButtonName);
        }

        if (cancelButton == null)
        {
            cancelButton = FindComponentByName<Button>(DefaultCancelButtonName);
        }

        if (messageText == null)
        {
            messageText = FindComponentByName<TMP_Text>(DefaultMessageTextName);
        }
    }

    private static Transform FindTransformByName(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform target in transforms)
        {
            if (target != null && target.name == objectName)
            {
                return target;
            }
        }

        return null;
    }

    private static T FindComponentByName<T>(string objectName) where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T component in components)
        {
            if (component != null && component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }
}
