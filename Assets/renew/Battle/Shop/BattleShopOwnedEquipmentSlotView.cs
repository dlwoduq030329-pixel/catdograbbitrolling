using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerWeapon의 장비 슬롯 하나를 상점 보유 장비 영역에 표시한다.
/// 장비 상태를 소유하지 않고 지정된 부위의 현재 장비 이미지만 그린다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleShopOwnedEquipmentSlotView : MonoBehaviour
{
    [Tooltip("이 View가 표시할 PlayerWeapon의 장비 부위입니다.")]
    [SerializeField] private PlayerEquipmentSlotType slotType;
    [Tooltip("현재 장비 또는 빈 슬롯 기본 Sprite를 표시할 이미지입니다.")]
    [SerializeField] private Image equipmentImage;

    /// <summary>Shop System이 이 View에 어떤 장비 슬롯 데이터를 전달할지 확인하는 읽기 전용 값이다.</summary>
    public PlayerEquipmentSlotType SlotType => slotType;

    /// <summary>
    /// 해당 슬롯에 장착된 장비가 있으면 장비 Sprite를 표시하고, 없으면 Shop System이 전달한
    /// 빈 슬롯 기본 Sprite를 표시한다. PlayerWeapon이나 EquipDatabase를 직접 조회하지 않는다.
    /// 두 Sprite가 모두 없을 때만 Image 컴포넌트를 숨긴다.
    /// </summary>
    public void Display(EquipData equippedEquipment, Sprite emptySlotSprite)
    {
        if (equipmentImage == null) return;
        equipmentImage.sprite = equippedEquipment != null
            ? equippedEquipment.myEquipSprite
            : emptySlotSprite;
        equipmentImage.enabled = equipmentImage.sprite != null;
    }
}
