using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle 상자 오버레이에 연결된 화면 오브젝트, 이미지, 애니메이션과 효과음만 관리한다.
/// 어떤 보상이 나오는지, Player에게 무엇을 지급하는지, 이미 연 상자인지는 알지 못한다.
/// BattleChestRewardSystem이 결정한 보상 Sprite와 연출 시점만 전달받아 화면에 표현한다.
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

    /// <summary>
    /// 이전 상자 연출의 흔적을 모두 지운다.
    /// 닫힌 상자만 표시하고, 이전 보상 Sprite와 Animator Trigger를 초기화한다.
    /// </summary>
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

    /// <summary>
    /// 이전 표시 상태를 먼저 초기화한 뒤 상자 오버레이를 활성화한다.
    /// BattleChestRewardSystem.TryOpen이 상자 이벤트를 시작할 때 호출한다.
    /// </summary>
    public void Show()
    {
        ResetView();
        SetObjectActive(overlayRoot != null ? overlayRoot : gameObject, true);
    }

    /// <summary>
    /// 상자가 화면에 떨어지는 연출과 효과음을 재생한다.
    /// 현재 프리팹의 DOTween Button 이벤트를 우선 사용하고, 연결되지 않았다면 Animator Trigger를 사용한다.
    /// </summary>
    public void PlayDropAnimation()
    {
        if (dropAnimationEventButton != null)
            dropAnimationEventButton.onClick.Invoke();
        else
            PlayAnimatorTrigger(dropAnimationTrigger);
        PlaySound(dropSound);
    }

    /// <summary>
    /// 닫힌 상자 이미지를 열린 상자 이미지로 교체한 뒤 열림 연출과 효과음을 재생한다.
    /// DOTween Button 이벤트가 없을 때만 Animator Trigger를 대체 경로로 사용한다.
    /// </summary>
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

    /// <summary>
    /// 카드 또는 장비 Sprite를 보상 Image에 미리 저장하되 상자가 열리기 전까지 오브젝트를 숨긴다.
    /// Color.white는 투명도를 초기화하기 위한 값이며, 실제 숨김은 SetActive(false)가 담당한다.
    /// </summary>
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

    /// <summary>상자 열림 연출이 끝난 뒤 준비된 Sprite가 있을 때만 보상 이미지를 활성화한다.</summary>
    public void RevealPreparedReward()
    {
        if (rewardImage != null)
            rewardImage.gameObject.SetActive(rewardImage.sprite != null);
    }

    /// <summary>진행 중인 상자 효과음을 멈추고 오버레이 전체를 비활성화한다.</summary>
    public void Hide()
    {
        if (chestAudioSource != null)
            chestAudioSource.Stop();

        SetObjectActive(overlayRoot != null ? overlayRoot : gameObject, false);
    }

    /// <summary>Animator와 Trigger 이름이 모두 유효할 때만 해당 연출 Trigger를 실행한다.</summary>
    private void PlayAnimatorTrigger(string triggerName)
    {
        if (chestAnimator != null && !string.IsNullOrWhiteSpace(triggerName))
            chestAnimator.SetTrigger(triggerName);
    }

    /// <summary>AudioSource와 Clip이 연결된 경우에만 다른 효과음을 끊지 않고 한 번 재생한다.</summary>
    private void PlaySound(AudioClip sound)
    {
        if (chestAudioSource != null && sound != null)
            chestAudioSource.PlayOneShot(sound);
    }

    /// <summary>null 참조 오류 없이 대상 오브젝트의 활성 상태를 변경하는 공통 보조 함수다.</summary>
    private static void SetObjectActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
