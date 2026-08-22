using System;
using UnityEngine;

/// <summary>
/// 현재 Player의 BattleHealth 생명주기와 초상화 체력 UI 연결을 한곳에서 관리한다.
/// 피해 계산이나 사망 규칙은 실행하지 않으며, Player가 교체될 때 이전 이벤트 구독을 안전하게 해제한다.
/// playerBody 밑에 런타임으로 붙는 자식 오브젝트가 아니라 BattleGameManager가 들고 있는
/// SerializeField 컴포넌트로, Scene 실행 내내 하나만 존재하며 Player가 바뀔 때마다 Bind()로
/// "재연결"된다. 그래서 OnDestroy는 사실상 Scene 종료 시점에만 발생한다(재도전/메인 복귀처럼
/// Scene을 유지한 채 Player만 초기화하는 흐름에서는 아직 별도로 호출되지 않는다 — 아래
/// ReleaseForRetryOrMainMenuReturn 참고).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerHealthBinding : MonoBehaviour
{
    [Header("플레이어 체력 UI")]
    [SerializeField] private BattlePlayerPortraitStatusView portraitView;
    [SerializeField, Min(0.01f)] private float damageDecreaseSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float healingIncreaseSpeed = 3f;

    // Action<BattleHealth>: "BattleHealth 하나를 받고 반환값 없는 함수"를 가리키는 델리게이트다.
    // 여기서는 BattleGameManager가 넘겨준 사망 처리 콜백(HandlePlayerDied)을 저장해뒀다가,
    // Player가 교체될 때마다 이전 BattleHealth.Died 이벤트에서 구독 해제하고 새 것에 다시 구독하는 데 쓴다.
    private Action<BattleHealth> playerDiedCallback;

    /// <summary>현재 Player에서 등록된 체력 컴포넌트다.</summary>
    public BattleHealth CurrentHealth { get; private set; }

    private void Awake()
    {
        portraitView?.ConfigureHealthAnimationSpeeds(damageDecreaseSpeed, healingIncreaseSpeed);
    }

    /// <summary>Player 사망 시 전투 중단을 처리할 Manager 콜백을 등록한다.</summary>
    public void SetDeathHandler(Action<BattleHealth> onPlayerDied)
    {
        if (CurrentHealth != null && playerDiedCallback != null)
        {
            CurrentHealth.Died -= playerDiedCallback;
        }

        playerDiedCallback = onPlayerDied;

        if (CurrentHealth != null && playerDiedCallback != null)
        {
            CurrentHealth.Died += playerDiedCallback;
        }
    }

    /// <summary>
    /// 새 Player 체력을 등록한다. 먼저 기존 Player의 사망 이벤트를 해제한 뒤 새 체력을
    /// 초상화 UI에 전달하고 사망 이벤트를 한 번만 구독한다. null은 현재 연결을 해제한다는 뜻이다.
    /// </summary>
    public void Bind(BattleHealth health)
    {
        if (CurrentHealth != null && playerDiedCallback != null)
        {
            CurrentHealth.Died -= playerDiedCallback;
        }

        CurrentHealth = health;
        portraitView?.BindPlayerHealth(CurrentHealth);

        if (CurrentHealth != null && playerDiedCallback != null)
        {
            CurrentHealth.Died += playerDiedCallback;
        }
    }

    /// <summary>소유자가 파괴될 때 현재 Player 이벤트와 UI 연결을 해제한다.</summary>
    public void ClearBinding()
    {
        if (CurrentHealth != null && playerDiedCallback != null)
        {
            CurrentHealth.Died -= playerDiedCallback;
        }

        portraitView?.BindPlayerHealth(null);
        CurrentHealth = null;
    }

    /// <summary>
    /// TODO(재도전/메인 화면 복귀 흐름 설계 후 연결): 지금은 Scene을 유지한 채 재도전하거나
    /// 메인 화면으로 돌아가는 흐름이 없어서 이 메서드를 실제로 부르는 곳이 없다(자리표시 스텁).
    /// 그런 흐름이 생기면 그 시점에서 ClearBinding()을 호출해 다음 플레이 전 UI·이벤트 연결을
    /// 미리 정리하도록 이 메서드 안을 채우고, 재도전/메인 복귀 로직 쪽에서 이 메서드를 호출한다.
    /// OnDestroy(Scene 종료 시점)보다 훨씬 이른 시점에 정리하고 싶다는 문제 인식에서 남겨둔다.
    /// </summary>
    public void ReleaseForRetryOrMainMenuReturn()
    {
        // TODO: 재도전/메인 화면 복귀 흐름이 확정되면 여기서 ClearBinding()을 호출한다.
    }

    private void OnDestroy()
    {
        ClearBinding();
    }
}
