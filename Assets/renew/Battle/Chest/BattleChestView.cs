using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle 상자 오버레이의 화면 요소와 연출만 담당한다.
/// 보상 추첨, Player 데이터 변경, 열린 상자 판정은 알지 못한다.
/// PC에서 새 BattleChestOverlay 프리팹을 만들 때 모든 참조를 Inspector로 직접 연결한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleChestView : MonoBehaviour
{
    [Header("화면 오브젝트")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject closedLidImageObject;
    [SerializeField] private GameObject openedLidImageObject;

    [Header("보상 표시")]
    [SerializeField] private Image rewardImage;

    [Header("상자 연출")]
    [Tooltip("현재 프리팹의 Drop DOTween 이벤트가 연결된 Button입니다. 사용자 입력용이 아니라 연출 호출용입니다.")]
    [SerializeField] private Button dropAnimationEventButton;
    [Tooltip("현재 프리팹의 Open DOTween 이벤트가 연결된 Button입니다. 사용자 입력용이 아니라 연출 호출용입니다.")]
    [SerializeField] private Button openAnimationEventButton;
    [Tooltip("상자 anim. Button 연출을 Animator로 교체하면 사용합니다.")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string dropAnimationTrigger = "Drop";
    [SerializeField] private string openAnimationTrigger = "Open";

    [Header("상자 효과음")]
    [SerializeField] private AudioSource chestAudioSource;
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private AudioClip openSound;

    /// <summary>상자 화면을 열기 전 닫힌 뚜껑과 비어 있는 보상 화면으로 되돌린다.</summary>
    public void ResetView()
    {
        SetObjectActive(closedLidImageObject, true);
        SetObjectActive(openedLidImageObject, false);

        if (rewardImage != null)
        {
            rewardImage.sprite = null;
            rewardImage.color = Color.white;
            rewardImage.gameObject.SetActive(false);
        }

        if (chestAnimator != null)
        {
            chestAnimator.ResetTrigger(dropAnimationTrigger);
            chestAnimator.ResetTrigger(openAnimationTrigger);
        }
    }

    /// <summary>초기화된 상자 오버레이 화면을 표시한다.</summary>
    public void Show()
    {
        ResetView();
        SetObjectActive(overlayRoot != null ? overlayRoot : gameObject, true);
    }

    /// <summary>상자가 화면에 떨어지는 연출과 효과음을 재생한다.</summary>
    public void PlayDropAnimation()
    {
        if (dropAnimationEventButton != null)
            dropAnimationEventButton.onClick.Invoke();
        else
            PlayAnimatorTrigger(dropAnimationTrigger);
        PlaySound(dropSound);
    }

    /// <summary>상자 뚜껑을 열린 모습으로 바꾸고 열림 연출과 효과음을 재생한다.</summary>
    public void PlayOpenAnimation()
    {
        SetObjectActive(closedLidImageObject, false);
        SetObjectActive(openedLidImageObject, true);
        if (openAnimationEventButton != null)
            openAnimationEventButton.onClick.Invoke();
        else
            PlayAnimatorTrigger(openAnimationTrigger);
        PlaySound(openSound);
    }

    /// <summary>카드 또는 장비 이미지를 미리 넣되 상자가 열리기 전까지 숨겨 둔다.</summary>
    public void PrepareItemReward(Sprite rewardSprite)
    {
        if (rewardImage != null)
        {
            rewardImage.sprite = rewardSprite;
            rewardImage.color = Color.white;
            rewardImage.preserveAspect = true;
            rewardImage.gameObject.SetActive(false);
        }
    }

    /// <summary>상자 열림 연출이 끝난 뒤 준비해 둔 보상 이미지를 실제로 표시한다.</summary>
    public void RevealPreparedReward()
    {
        if (rewardImage != null)
            rewardImage.gameObject.SetActive(rewardImage.sprite != null);
    }

    /// <summary>상자 효과음을 멈추고 오버레이 전체를 숨긴다.</summary>
    public void Hide()
    {
        if (chestAudioSource != null)
            chestAudioSource.Stop();

        SetObjectActive(overlayRoot != null ? overlayRoot : gameObject, false);
    }

    private void PlayAnimatorTrigger(string triggerName)
    {
        if (chestAnimator != null && !string.IsNullOrWhiteSpace(triggerName))
            chestAnimator.SetTrigger(triggerName);
    }

    private void PlaySound(AudioClip sound)
    {
        if (chestAudioSource != null && sound != null)
            chestAudioSource.PlayOneShot(sound);
    }

    private static void SetObjectActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
