using UnityEngine;

/// <summary>
/// Battle씬에서 실제로 스폰되는 캐릭터(Assets/renew/Battle/Player/Prefabs의
/// Bunny_Player/Cat_Player/Dog_Player)에는 장비 부착 컴포넌트가 아예 붙어있지 않았다.
/// 기존 weaponSet(main.unity 구버전 캐릭터에서 사용)이나 BattlePlayerEquip
/// (Assets/Game/Characters의 다른 캐릭터 세트에서 사용) 둘 다 이 프리팹에는 연결되어 있지
/// 않다 — 셋 다 골격은 같지만(handslot.l_end / handslot.r_end / chest / head_end) 서로
/// 다른 프리팹 묶음이었다.
///
/// 프리팹 파일을 직접 손대는 대신, 이 컴포넌트를 런타임에 BattleComponentResolver.GetOrAdd로
/// 붙이고 이름으로 본을 찾아 연결한다(main.unity의 weaponSet Inspector 참조가 가리키던
/// 이름과 동일). 새 프리팹이 추가돼도 본 이름만 같으면 별도 Inspector 작업 없이 동작한다.
/// </summary>
public sealed class BattleEquipVisualBinder : MonoBehaviour
{
    private const string LeftBoneName = "handslot.l_end";
    private const string RightBoneName = "handslot.r_end";
    private const string BodyBoneName = "chest";
    private const string HeadBoneName = "head_end";

    private Transform leftSlot;
    private Transform rightSlot;
    private Transform bodySlot;
    private Transform headSlot;
    private GameObject leftEquipmentInstance;
    private GameObject rightEquipmentInstance;
    private GameObject bodyEquipmentInstance;
    private GameObject headEquipmentInstance;
    private PlayerWeapon playerWeapon;
    private bool bound;

    /// <summary>PlayerWeapon의 변경 이벤트를 구독하고 현재 장비 모델을 즉시 반영한다.</summary>
    public void Bind(PlayerWeapon sourcePlayerWeapon)
    {
        if (playerWeapon != null)
            playerWeapon.EquipmentChanged -= HandleEquipmentChanged;

        playerWeapon = sourcePlayerWeapon;
        if (playerWeapon != null)
            playerWeapon.EquipmentChanged += HandleEquipmentChanged;

        Refresh();
    }

    public void Refresh()
    {
        BindIfNeeded();

        if (playerWeapon == null)
        {
            Debug.LogWarning("[BattleEquipVisualBinder] PlayerWeapon이 연결되지 않았습니다.", this);
            return;
        }

        ApplySlot("Left", leftSlot, playerWeapon.LeftArm.CurrentEquipment, true, false, ref leftEquipmentInstance);
        ApplySlot("Right", rightSlot, playerWeapon.RightArm.CurrentEquipment, true, true, ref rightEquipmentInstance);
        ApplySlot("Body", bodySlot, playerWeapon.Body.CurrentEquipment, false, false, ref bodyEquipmentInstance);
        ApplySlot("Head", headSlot, playerWeapon.Head.CurrentEquipment, false, false, ref headEquipmentInstance);
    }

    private void HandleEquipmentChanged(PlayerWeapon changedPlayerWeapon) => Refresh();

    private void BindIfNeeded()
    {
        if (bound) return;
        bound = true;

        leftSlot = FindChild(transform, LeftBoneName);
        rightSlot = FindChild(transform, RightBoneName);
        bodySlot = FindChild(transform, BodyBoneName);
        headSlot = FindChild(transform, HeadBoneName);

        if (leftSlot == null) Debug.LogWarning($"[BattleEquipVisualBinder] '{LeftBoneName}' 본을 찾지 못했습니다.", this);
        if (rightSlot == null) Debug.LogWarning($"[BattleEquipVisualBinder] '{RightBoneName}' 본을 찾지 못했습니다.", this);
        if (bodySlot == null) Debug.LogWarning($"[BattleEquipVisualBinder] '{BodyBoneName}' 본을 찾지 못했습니다.", this);
        if (headSlot == null) Debug.LogWarning($"[BattleEquipVisualBinder] '{HeadBoneName}' 본을 찾지 못했습니다.", this);
    }

    private void ApplySlot(
        string slotLabel,
        Transform slot,
        EquipData equipment,
        bool isHandSlot,
        bool useSecondaryTwoHandPrefab,
        ref GameObject equipmentInstance)
    {
        if (slot == null) return;

        // 캐릭터 프리팹의 원본 자식은 유지하고 이 Binder가 생성한 장비만 제거한다.
        if (equipmentInstance != null)
        {
            Destroy(equipmentInstance);
            equipmentInstance = null;
        }

        if (equipment == null)
        {
            Debug.Log($"[BattleEquipVisualBinder] {slotLabel} 슬롯: 미장착", this);
            return;
        }
        GameObject prefab = useSecondaryTwoHandPrefab &&
                            equipment.weaponKind == WeaponKind.TwoHand &&
                            equipment.weaponPrefab2 != null
            ? equipment.weaponPrefab2
            : equipment.weaponPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[BattleEquipVisualBinder] {slotLabel} 슬롯: {equipment.cardname}의 weaponPrefab이 비어 있습니다.", this);
            return;
        }

        equipmentInstance = Instantiate(prefab, slot);
        Debug.Log($"[BattleEquipVisualBinder] {slotLabel} 슬롯: {equipment.cardname} 모델 생성 완료.", this);
        if (isHandSlot)
        {
            equipmentInstance.transform.localRotation = Quaternion.Euler(-90f, 0f, 100f);
        }
    }

    private void OnDestroy()
    {
        if (playerWeapon != null)
            playerWeapon.EquipmentChanged -= HandleEquipmentChanged;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            Transform found = FindChild(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
