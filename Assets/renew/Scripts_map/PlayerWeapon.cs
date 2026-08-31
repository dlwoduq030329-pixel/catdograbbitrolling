using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>PlayerWeapon이 반드시 보유하는 네 장비 슬롯의 종류다.</summary>
public enum PlayerEquipmentSlotType
{
    LeftArm,
    RightArm,
    Head,
    Body
}

/// <summary>
/// Player의 장비 슬롯 하나가 실제 EquipData를 직접 보유한다.
/// 데이터베이스 인덱스나 DataConfig를 장비 상태의 원본으로 사용하지 않는다.
/// </summary>
[Serializable]
public sealed class PlayerEquipmentSlot
{
    [SerializeField] private PlayerEquipmentSlotType slotType;
    [SerializeField] private EquipData currentEquipment;

    public PlayerEquipmentSlotType SlotType => slotType;
    public EquipData CurrentEquipment => currentEquipment;
    public bool IsEmpty => currentEquipment == null;

    public PlayerEquipmentSlot(PlayerEquipmentSlotType requiredSlotType)
    {
        slotType = requiredSlotType;
    }

    /// <summary>이 슬롯의 장비를 교체하고 이전 장비를 반환한다.</summary>
    internal EquipData ReplaceEquipment(EquipData newEquipment)
    {
        EquipData previousEquipment = currentEquipment;
        currentEquipment = newEquipment;
        return previousEquipment;
    }
}

/// <summary>
/// 현재 장착된 모든 장비에서 합산한 Player 장비 스탯이다.
/// 기본 캐릭터 스탯과 섞지 않고 CharactorStatus 같은 수신자에게 한 번에 전달하기 위한 값이다.
/// </summary>
public readonly struct PlayerEquipmentStats
{
    public int StrengthBonus { get; }
    public int DexterityBonus { get; }
    public int IntelligenceBonus { get; }
    public int WisdomBonus { get; }
    public int CharismaBonus { get; }
    public int VitalityBonus { get; }
    public float AttackRangeBonus { get; }

    public PlayerEquipmentStats(
        int strengthBonus,
        int dexterityBonus,
        int intelligenceBonus,
        int wisdomBonus,
        int charismaBonus,
        int vitalityBonus,
        float attackRangeBonus)
    {
        StrengthBonus = strengthBonus;
        DexterityBonus = dexterityBonus;
        IntelligenceBonus = intelligenceBonus;
        WisdomBonus = wisdomBonus;
        CharismaBonus = charismaBonus;
        VitalityBonus = vitalityBonus;
        AttackRangeBonus = attackRangeBonus;
    }
}

/// <summary>
/// 실제 Player가 왼팔·오른팔·머리·몸통 장비와 그 장비 스탯을 직접 소유한다.
/// 상점, 상자, 3D 모델, UI 연결은 이 클래스의 상태와 이벤트를 사용하며 DataConfig는 사용하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeapon : MonoBehaviour
{
    [Header("Player 필수 장비 슬롯")]
    [InspectorName("왼팔 장비 슬롯")]
    [Tooltip("Player의 왼팔에 현재 장착된 EquipData를 보관합니다.")]
    [SerializeField] private PlayerEquipmentSlot leftArm =
        new PlayerEquipmentSlot(PlayerEquipmentSlotType.LeftArm);
    [InspectorName("오른팔 장비 슬롯")]
    [Tooltip("Player의 오른팔에 현재 장착된 EquipData를 보관합니다.")]
    [SerializeField] private PlayerEquipmentSlot rightArm =
        new PlayerEquipmentSlot(PlayerEquipmentSlotType.RightArm);
    [InspectorName("머리 장비 슬롯")]
    [Tooltip("Player의 머리에 현재 장착된 EquipData를 보관합니다.")]
    [SerializeField] private PlayerEquipmentSlot head =
        new PlayerEquipmentSlot(PlayerEquipmentSlotType.Head);
    [InspectorName("몸통 장비 슬롯")]
    [Tooltip("Player의 몸통에 현재 장착된 EquipData를 보관합니다.")]
    [SerializeField] private PlayerEquipmentSlot body =
        new PlayerEquipmentSlot(PlayerEquipmentSlotType.Body);

    public PlayerEquipmentSlot LeftArm => leftArm;
    public PlayerEquipmentSlot RightArm => rightArm;
    public PlayerEquipmentSlot Head => head;
    public PlayerEquipmentSlot Body => body;
    public PlayerEquipmentStats TotalEquipmentStats { get; private set; }

    /// <summary>
    /// 같은 양손 장비가 왼팔과 오른팔을 함께 점유하는지 슬롯 상태에서 계산한다.
    /// 별도 bool 값을 저장하지 않으므로 실제 슬롯 상태와 어긋나지 않는다.
    /// </summary>
    public bool IsTwoHandEquipped =>
        leftArm?.CurrentEquipment != null &&
        leftArm.CurrentEquipment.weaponKind == WeaponKind.TwoHand &&
        ReferenceEquals(leftArm.CurrentEquipment, rightArm?.CurrentEquipment);

    /// <summary>슬롯 장비가 변경된 뒤 3D 모델과 장비 UI에 최신 PlayerWeapon을 전달한다.</summary>
    public event Action<PlayerWeapon> EquipmentChanged;

    /// <summary>슬롯 변경 후 다시 합산한 장비 스탯을 Player 상태 계층에 직접 전달한다.</summary>
    public event Action<PlayerEquipmentStats> EquipmentStatsChanged;

    /// <summary>왼팔 장비를 교체하고 이전에 장착돼 있던 장비를 반환한다.</summary>
    public EquipData EquipLeftArm(EquipData equipment)
    {
        if (equipment != null && equipment.weaponKind == WeaponKind.TwoHand)
            return EquipTwoHandEquipment(equipment);

        return SetEquipmentInSlot(leftArm, equipment);
    }

    /// <summary>오른팔 장비를 교체하고 이전에 장착돼 있던 장비를 반환한다.</summary>
    public EquipData EquipRightArm(EquipData equipment)
    {
        if (equipment != null && equipment.weaponKind == WeaponKind.TwoHand)
            return EquipTwoHandEquipment(equipment);

        return SetEquipmentInSlot(rightArm, equipment);
    }

    /// <summary>머리 장비를 교체하고 이전에 장착돼 있던 장비를 반환한다.</summary>
    public EquipData EquipHead(EquipData equipment) => SetEquipmentInSlot(head, equipment);

    /// <summary>몸통 장비를 교체하고 이전에 장착돼 있던 장비를 반환한다.</summary>
    public EquipData EquipBody(EquipData equipment) => SetEquipmentInSlot(body, equipment);

    /// <summary>지정 슬롯의 현재 장비를 해제하고 해제된 EquipData를 반환한다.</summary>
    public EquipData UnequipSlot(PlayerEquipmentSlotType slotType)
    {
        return SetEquipmentInSlot(GetSlot(slotType), null);
    }

    /// <summary>지정 슬롯에 장비를 직접 배치하고 이전 EquipData를 반환한다.</summary>
    public EquipData EquipInSlot(PlayerEquipmentSlotType slotType, EquipData equipment)
    {
        switch (slotType)
        {
            case PlayerEquipmentSlotType.LeftArm: return EquipLeftArm(equipment);
            case PlayerEquipmentSlotType.RightArm: return EquipRightArm(equipment);
            case PlayerEquipmentSlotType.Head: return EquipHead(equipment);
            case PlayerEquipmentSlotType.Body: return EquipBody(equipment);
            default:
                Debug.LogError($"지원하지 않는 Player 장비 슬롯입니다: {slotType}", this);
                return null;
        }
    }

    /// <summary>
    /// DataConfig.Init의 장비 초기화 역할을 대신한다.
    /// 네 슬롯을 모두 비우고 스탯·UI·3D 모델 수신자에게 한 번만 변경을 알린다.
    /// </summary>
    public void ClearAllEquipment()
    {
        leftArm.ReplaceEquipment(null);
        rightArm.ReplaceEquipment(null);
        head.ReplaceEquipment(null);
        body.ReplaceEquipment(null);
        RecalculateStatsAndNotifyChanges();
    }

    /// <summary>
    /// 장비 종류에 맞는 슬롯을 자동으로 선택한다.
    /// 한손 장비는 왼팔, 오른팔의 빈 슬롯 순서로 사용하고 둘 다 차 있으면 왼팔을 교체한다.
    /// </summary>
    public void EquipEquipmentAutomatically(EquipData equipment)
    {
        EquipEquipmentAutomatically(equipment, out _, out _);
    }

    /// <summary>
    /// 장비 종류에 맞는 슬롯을 자동으로 선택하고, 밀려난 기존 장비를 반환한다.
    /// 양손 장비가 서로 다른 한손 장비 두 개를 교체하면 두 반환값을 모두 사용해야 한다.
    /// </summary>
    public void EquipEquipmentAutomatically(
        EquipData equipment,
        out EquipData removedPrimaryEquipment,
        out EquipData removedSecondaryEquipment)
    {
        removedPrimaryEquipment = null;
        removedSecondaryEquipment = null;

        if (equipment == null)
        {
            Debug.LogWarning("자동 장착할 EquipData가 없습니다.", this);
            return;
        }

        switch (equipment.weaponKind)
        {
            case WeaponKind.Hand:
                PlayerEquipmentSlotType handSlot = FindEmptyHandSlot() ?? PlayerEquipmentSlotType.LeftArm;
                removedPrimaryEquipment = EquipHandInSlot(equipment, handSlot);
                break;
            case WeaponKind.Body:
                removedPrimaryEquipment = EquipBody(equipment);
                break;
            case WeaponKind.Head:
                removedPrimaryEquipment = EquipHead(equipment);
                break;
            case WeaponKind.TwoHand:
                EquipTwoHandEquipment(
                    equipment,
                    out removedPrimaryEquipment,
                    out removedSecondaryEquipment);
                break;
            default:
                Debug.LogError($"지원하지 않는 장비 종류입니다: {equipment.weaponKind}", this);
                break;
        }
    }

    /// <summary>한손 장비를 지정한 왼팔 또는 오른팔 슬롯에 장착한다.</summary>
    public EquipData EquipHandInSlot(EquipData equipment, PlayerEquipmentSlotType handSlotType)
    {
        if (equipment == null || equipment.weaponKind != WeaponKind.Hand)
        {
            Debug.LogWarning("왼팔·오른팔 슬롯에는 WeaponKind.Hand 장비만 개별 장착할 수 있습니다.", this);
            return null;
        }

        if (handSlotType != PlayerEquipmentSlotType.LeftArm &&
            handSlotType != PlayerEquipmentSlotType.RightArm)
        {
            Debug.LogError($"손 장비를 장착할 수 없는 슬롯입니다: {handSlotType}", this);
            return null;
        }

        return SetEquipmentInSlot(GetSlot(handSlotType), equipment);
    }

    /// <summary>왼팔을 먼저 검사한 뒤 비어 있는 손 슬롯을 반환한다.</summary>
    public PlayerEquipmentSlotType? FindEmptyHandSlot()
    {
        if (leftArm.IsEmpty) return PlayerEquipmentSlotType.LeftArm;
        if (rightArm.IsEmpty) return PlayerEquipmentSlotType.RightArm;
        return null;
    }

    /// <summary>
    /// 현재 공격 유형에 사용할 손 장비를 반환한다.
    /// 시스템 개선 대상: 최종적으로는 사거리 자동 비교가 아니라 플레이어 또는 카드가 사용할 손을 선택해야 한다.
    /// </summary>
    public EquipData GetActiveHandEquipment(playerAttackST attackType)
    {
        EquipData leftEquipment = leftArm.CurrentEquipment;
        EquipData rightEquipment = rightArm.CurrentEquipment;

        if (leftEquipment == null) return rightEquipment;
        if (rightEquipment == null) return leftEquipment;
        if (ReferenceEquals(leftEquipment, rightEquipment)) return leftEquipment;

        if (attackType == playerAttackST.Range)
        {
            return leftEquipment.attackRange >= rightEquipment.attackRange
                ? leftEquipment
                : rightEquipment;
        }

        return leftEquipment.attackRange <= rightEquipment.attackRange
            ? leftEquipment
            : rightEquipment;
    }

    private void Awake()
    {
        EnsureAllEquipmentSlotsExist();
        RecalculateEquipmentStats();
    }

    private EquipData SetEquipmentInSlot(PlayerEquipmentSlot slot, EquipData equipment)
    {
        if (slot == null)
        {
            Debug.LogError("PlayerWeapon의 필수 장비 슬롯이 없습니다.", this);
            return null;
        }

        if (ReferenceEquals(slot.CurrentEquipment, equipment))
        {
            return slot.CurrentEquipment;
        }

        EquipData previousEquipment = slot.CurrentEquipment;

        // 양손 장비의 한쪽을 교체하거나 해제하면 반대쪽에 같은 장비 참조를 남기지 않는다.
        if (IsTwoHandEquipped &&
            (slot.SlotType == PlayerEquipmentSlotType.LeftArm ||
             slot.SlotType == PlayerEquipmentSlotType.RightArm))
        {
            leftArm.ReplaceEquipment(null);
            rightArm.ReplaceEquipment(null);
        }

        slot.ReplaceEquipment(equipment);
        RecalculateStatsAndNotifyChanges();
        return previousEquipment;
    }

    /// <summary>양손 장비 하나가 왼팔과 오른팔 슬롯을 동시에 점유하도록 장착한다.</summary>
    private EquipData EquipTwoHandEquipment(EquipData equipment)
    {
        EquipTwoHandEquipment(equipment, out EquipData removedLeft, out _);
        return removedLeft;
    }

    /// <summary>
    /// 양손 장비를 장착하고, 밀려난 왼팔·오른팔 장비를 각각 반환한다.
    /// 기존에 있던 게 같은 양손 장비였다면(왼팔·오른팔이 같은 참조) 오른쪽은 null로 반환해
    /// 호출자가 같은 장비를 두 번 판매 처리하지 않도록 한다.
    /// 기존에 서로 다른 한손 장비 두 개가 있었다면 둘 다 반환하므로 각각 판매 처리해야 한다.
    /// </summary>
    public void EquipTwoHandEquipment(EquipData equipment, out EquipData removedLeft, out EquipData removedRight)
    {
        if (equipment == null || equipment.weaponKind != WeaponKind.TwoHand)
        {
            Debug.LogWarning("양손 슬롯 점유에는 WeaponKind.TwoHand 장비가 필요합니다.", this);
            removedLeft = null;
            removedRight = null;
            return;
        }

        bool wasTwoHandEquipped = IsTwoHandEquipped;
        removedLeft = leftArm.CurrentEquipment;
        removedRight = wasTwoHandEquipped ? null : rightArm.CurrentEquipment;

        leftArm.ReplaceEquipment(equipment);
        rightArm.ReplaceEquipment(equipment);
        RecalculateStatsAndNotifyChanges();
    }

    /// <summary>
    /// 네 슬롯의 EquipData에서 장비 스탯을 처음부터 다시 합산한다.
    /// 같은 EquipData가 양팔을 함께 점유하는 양손 장비는 한 번만 계산한다.
    /// </summary>
    private void RecalculateEquipmentStats()
    {
        int strengthBonus = 0;
        int dexterityBonus = 0;
        int intelligenceBonus = 0;
        int wisdomBonus = 0;
        int charismaBonus = 0;
        int vitalityBonus = 0;
        float attackRangeBonus = 0f;

        HashSet<EquipData> countedEquipment = new HashSet<EquipData>();
        AddSlotEquipmentStats(leftArm, countedEquipment, ref strengthBonus, ref dexterityBonus,
            ref intelligenceBonus, ref wisdomBonus, ref charismaBonus, ref vitalityBonus,
            ref attackRangeBonus);
        AddSlotEquipmentStats(rightArm, countedEquipment, ref strengthBonus, ref dexterityBonus,
            ref intelligenceBonus, ref wisdomBonus, ref charismaBonus, ref vitalityBonus,
            ref attackRangeBonus);
        AddSlotEquipmentStats(head, countedEquipment, ref strengthBonus, ref dexterityBonus,
            ref intelligenceBonus, ref wisdomBonus, ref charismaBonus, ref vitalityBonus,
            ref attackRangeBonus);
        AddSlotEquipmentStats(body, countedEquipment, ref strengthBonus, ref dexterityBonus,
            ref intelligenceBonus, ref wisdomBonus, ref charismaBonus, ref vitalityBonus,
            ref attackRangeBonus);

        TotalEquipmentStats = new PlayerEquipmentStats(
            strengthBonus,
            dexterityBonus,
            intelligenceBonus,
            wisdomBonus,
            charismaBonus,
            vitalityBonus,
            attackRangeBonus);
    }

    /// <summary>장비 변경 결과를 다시 계산한 뒤 스탯 수신자와 3D/UI 수신자에게 한 번씩 알린다.</summary>
    private void RecalculateStatsAndNotifyChanges()
    {
        RecalculateEquipmentStats();
        EquipmentStatsChanged?.Invoke(TotalEquipmentStats);
        EquipmentChanged?.Invoke(this);
    }

    private static void AddSlotEquipmentStats(
        PlayerEquipmentSlot slot,
        ISet<EquipData> countedEquipment,
        ref int strengthBonus,
        ref int dexterityBonus,
        ref int intelligenceBonus,
        ref int wisdomBonus,
        ref int charismaBonus,
        ref int vitalityBonus,
        ref float attackRangeBonus)
    {
        EquipData equipment = slot?.CurrentEquipment;
        if (equipment == null || !countedEquipment.Add(equipment))
        {
            return;
        }

        strengthBonus += equipment.stroffset;
        dexterityBonus += equipment.dexoffset;
        intelligenceBonus += equipment.intoffset;
        wisdomBonus += equipment.wisoffset;
        charismaBonus += equipment.caroffset;
        vitalityBonus += equipment.vitoffset;
        attackRangeBonus += equipment.attackRange;
    }

    private PlayerEquipmentSlot GetSlot(PlayerEquipmentSlotType slotType)
    {
        switch (slotType)
        {
            case PlayerEquipmentSlotType.LeftArm: return leftArm;
            case PlayerEquipmentSlotType.RightArm: return rightArm;
            case PlayerEquipmentSlotType.Head: return head;
            case PlayerEquipmentSlotType.Body: return body;
            default:
                Debug.LogError($"지원하지 않는 Player 장비 슬롯입니다: {slotType}", this);
                return null;
        }
    }

    /// <summary>기존 Scene의 빈 PlayerWeapon 직렬화 데이터에서도 네 필수 슬롯을 항상 복구한다.</summary>
    private void EnsureAllEquipmentSlotsExist()
    {
        if (leftArm == null || leftArm.SlotType != PlayerEquipmentSlotType.LeftArm)
            leftArm = new PlayerEquipmentSlot(PlayerEquipmentSlotType.LeftArm);
        if (rightArm == null || rightArm.SlotType != PlayerEquipmentSlotType.RightArm)
            rightArm = new PlayerEquipmentSlot(PlayerEquipmentSlotType.RightArm);
        if (head == null || head.SlotType != PlayerEquipmentSlotType.Head)
            head = new PlayerEquipmentSlot(PlayerEquipmentSlotType.Head);
        if (body == null || body.SlotType != PlayerEquipmentSlotType.Body)
            body = new PlayerEquipmentSlot(PlayerEquipmentSlotType.Body);
    }
}
//ai 기능구현 중심으로 써야지.... 구조를 짜버리면 내가 못따라가
// 구조 자체는 이제 내가 작성하고, ai한테 도움 받는걸로 수정함 모르는건 즉각 질문하는 구조 + 주석하고 summary tooltip 담당
