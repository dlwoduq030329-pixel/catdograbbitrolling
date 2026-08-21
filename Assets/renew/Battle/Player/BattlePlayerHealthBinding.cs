using System;
using UnityEngine;

/// <summary>
/// 현재 Player의 BattleHealth 생명주기와 초상화 체력 UI 연결을 한곳에서 관리한다.
/// 피해 계산이나 사망 규칙은 실행하지 않으며, Player가 교체될 때 이전 이벤트 구독을 안전하게 해제한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerHealthBinding : MonoBehaviour
{
    [Header("플레이어 체력 UI")]
    [SerializeField] private BattlePlayerPortraitStatusView portraitView;
    [SerializeField, Min(0.01f)] private float damageDecreaseSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float healingIncreaseSpeed = 3f;

    private Action<BattleHealth> playerDied;

    /// <summary>현재 Player에서 등록된 체력 컴포넌트다.</summary>
    public BattleHealth CurrentHealth { get; private set; }

    private void Awake()
    {
        portraitView?.ConfigureAnimation(damageDecreaseSpeed, healingIncreaseSpeed);
    }

    /// <summary>Player 사망 시 전투 중단을 처리할 Manager 콜백을 등록한다.</summary>
    public void SetDeathHandler(Action<BattleHealth> onPlayerDied)
    {
        if (CurrentHealth != null && playerDied != null)
        {
            CurrentHealth.Died -= playerDied;
        }

        playerDied = onPlayerDied;

        if (CurrentHealth != null && playerDied != null)
        {
            CurrentHealth.Died -= playerDied;
            CurrentHealth.Died += playerDied;
        }
    }

    /// <summary>
    /// 새 Player 체력을 등록한다. 먼저 기존 Player의 사망 이벤트를 해제한 뒤 새 체력을
    /// 초상화 UI에 전달하고 사망 이벤트를 한 번만 구독한다. null은 현재 연결을 해제한다는 뜻이다.
    /// </summary>
    public void Bind(BattleHealth health)
    {
        if (CurrentHealth != null && playerDied != null)
        {
            CurrentHealth.Died -= playerDied;
        }

        CurrentHealth = health;
        portraitView?.Bind(CurrentHealth);

        if (CurrentHealth != null && playerDied != null)
        {
            CurrentHealth.Died -= playerDied;
            CurrentHealth.Died += playerDied;
        }
    }

    /// <summary>소유자가 파괴될 때 현재 Player 이벤트와 UI 연결을 해제한다.</summary>
    public void ClearBinding()
    {
        if (CurrentHealth != null && playerDied != null)
        {
            CurrentHealth.Died -= playerDied;
        }

        portraitView?.Bind(null);
        CurrentHealth = null;
    }

    private void OnDestroy()
    {
        ClearBinding();
    }
}
