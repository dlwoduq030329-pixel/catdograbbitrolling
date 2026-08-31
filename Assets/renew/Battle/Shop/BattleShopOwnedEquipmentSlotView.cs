using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerWeapon의 장비 슬롯 하나를 상점 보유 장비 영역에 표시한다.
/// 장비 상태를 소유하지 않고 지정된 부위의 현재 장비 이미지만 그린다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleShopOwnedEquipmentSlotView : MonoBehaviour
{
    [SerializeField] private PlayerEquipmentSlotType slotType;
    [SerializeField] private Image equipmentImage;

    public PlayerEquipmentSlotType SlotType => slotType;

    /// <summary>장착 장비가 없으면 데이터베이스 0번의 기본 이미지를 표시한다.</summary>
    public void Display(EquipData equippedEquipment, Sprite emptySlotSprite)
    {
        if (equipmentImage == null) return;
        equipmentImage.sprite = equippedEquipment != null
            ? equippedEquipment.myEquipSprite
            : emptySlotSprite;
        equipmentImage.enabled = equipmentImage.sprite != null;
    }
}
