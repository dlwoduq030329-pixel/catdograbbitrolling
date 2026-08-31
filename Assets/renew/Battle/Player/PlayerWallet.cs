using System;
using UnityEngine;

/// <summary>
/// Player가 직접 소유하는 골드를 관리한다. 상점과 상자는 값 자체를 수정하지 않고
/// 이 컴포넌트의 지출·획득 함수만 호출한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWallet : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingGold = 180;

    public int Gold { get; private set; }
    public event Action<int> GoldChanged;

    private void Awake()
    {
        Gold = Mathf.Max(0, startingGold);
    }

    /// <summary>Player 등록 시 이전 진행 데이터의 골드를 현재 지갑으로 가져온다.</summary>
    public void InitializeGold(int gold)
    {
        Gold = Mathf.Max(0, gold);
        GoldChanged?.Invoke(Gold);
    }

    public bool CanAfford(int amount) => amount >= 0 && Gold >= amount;

    /// <summary>골드가 충분할 때만 차감하고 성공 여부를 반환한다.</summary>
    public bool TrySpendGold(int amount)
    {
        if (!CanAfford(amount)) return false;
        Gold -= amount;
        GoldChanged?.Invoke(Gold);
        return true;
    }

    /// <summary>양수 골드만 현재 지갑에 더한다.</summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        GoldChanged?.Invoke(Gold);
    }
}
