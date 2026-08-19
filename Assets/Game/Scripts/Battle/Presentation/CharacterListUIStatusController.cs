using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CharacterListUI.prefab("캐릭터 정보" 화면)에는 WeaponR/WeaponL/WeaponBody/WeaponHead
/// 아이콘, STR/VIT/DEX/WIS 슬라이더, 그리고 레이더 차트(DrawState, "Status" 자식)가 있지만
/// 이를 채워주는 스크립트가 프리팹에 하나도 붙어있지 않았다(전부 Image/Slider/기본 UI
/// 컴포넌트뿐이고, DrawState의 CharactorStatus 참조도 Inspector에서 비어 있었다). 그 결과
/// 장비를 구매/판매해도 이 화면은 항상 빈 흰색 아이콘을 보여줬고, 스탯 레이더 차트는
/// 아예 아무것도 그리지 못했다.
///
/// 스탯 소스도 바로잡았다: DataConfig.playerDatas는 STR/WIS/DEX/VIT 4개뿐이고 장비 보너스만
/// 누적되는 구버전 값이라, 캐릭터 선택 화면에서 배분한 실제 포인트가 전혀 반영되지 않는다.
/// 실제 6개 스탯(STR/DEX/INT/WIS/CAR/VIT)은 CharactorStatus 컴포넌트에 있다.
///
/// 이 스크립트는 이름으로 자식을 찾아 연결하기 때문에 프리팹의 기존 Inspector 참조를
/// 새로 손댈 필요가 없다. CharacterListUI GameObject(또는 그 자식 아무 곳)에 컴포넌트로
/// 추가하기만 하면 된다. 패널이 열릴 때(OnEnable)마다 장비/스탯을 갱신하고, 열려 있는 동안
/// 배경 입력·카메라를 잠근다.
/// </summary>
public sealed class CharacterListUIStatusController : MonoBehaviour
{
    private Image weaponR;
    private Image weaponL;
    private Image weaponBody;
    private Image weaponHead;

    private Slider strSlider, vitSlider, dexSlider, wisSlider;
    private DrawState drawState;

    private void OnEnable()
    {
        BindIfNeeded();
        Refresh();
        EnsureIgnoresParentHudLock();
        LockBattleInput();
    }

    private void OnDisable()
    {
        UnlockBattleInput();
    }

    // 이 패널은 HUD의 프로필 아이콘 버튼이 CharacterListUI.SetActive(true)를 직접 호출하고,
    // 패널 안의 EscButton도 SetActive(false)를 직접 호출한다(둘 다 프리팹에 미리 박혀있는
    // 호출이라 코드로 가로채지 않았다). 대신 이 컴포넌트가 같은 GameObject에 붙어있으므로
    // OnEnable/OnDisable이 열고 닫을 때마다 항상 같이 호출된다는 점을 이용해 여기서 배경
    // 입력과 카메라를 잠그고 푼다.
    // 모달(BeginModalInteraction) 동안 BattleGameManager가 HUDCanvas 전체를 CanvasGroup으로
    // 잠그는데, 이 패널 자신이 HUDCanvas 안에 중첩되어 있어(HUDCanvas.prefab의 자식) 같이
    // 잠기면 EscButton조차 눌리지 않아 패널을 닫을 수 없게 된다. 이 패널 자신의 GameObject에
    // ignoreParentGroups = true인 CanvasGroup을 붙여 부모(HUDCanvas)의 잠금에서 예외로 둔다.
    private void EnsureIgnoresParentHudLock()
    {
        CanvasGroup selfGroup = GetComponent<CanvasGroup>();
        if (selfGroup == null) selfGroup = gameObject.AddComponent<CanvasGroup>();
        selfGroup.ignoreParentGroups = true;
    }

    private void LockBattleInput()
    {
        BattleGameManager.Instance?.BeginModalInteraction();
        BattleMapCameraInput.SetEnabledOnMainCamera(false);
    }

    private void UnlockBattleInput()
    {
        BattleGameManager manager = BattleGameManager.Instance;
        manager?.EndModalInteraction();
        BattleMapCameraInput.SetEnabledOnMainCamera(
            manager == null || !manager.IsModalInteractionOpen);
    }

    private void BindIfNeeded()
    {
        if (weaponR != null) return;

        weaponR = FindChild("WeaponR")?.GetComponent<Image>();
        weaponL = FindChild("WeaponL")?.GetComponent<Image>();
        weaponBody = FindChild("WeaponBody")?.GetComponent<Image>();
        weaponHead = FindChild("WeaponHead")?.GetComponent<Image>();

        strSlider = FindChild("STR_Slider")?.GetComponent<Slider>();
        vitSlider = FindChild("VIT_Slider")?.GetComponent<Slider>();
        dexSlider = FindChild("DEX_Slider")?.GetComponent<Slider>();
        wisSlider = FindChild("WIS_Slider")?.GetComponent<Slider>();

        // STR_Text/STR_StatusText 등의 TMP_Text는 DrawState(레이더 차트)가 자신의
        // statusText[] 배열로 이미 직접 소유하고 있다(같은 GameObject). 여기서 따로 쓰면
        // DataConfig.playerDatas(구버전, 4개 스탯)와 CharactorStatus(현재 6개 스탯)가
        // 서로 다른 값을 같은 텍스트에 덮어쓰는 충돌이 난다. DrawState.SetStatus/Refresh만
        // 호출해서 텍스트와 차트를 한 번에 정확한 값으로 갱신한다.
        drawState = FindChild("Status")?.GetComponent<DrawState>();
    }

    public void Refresh()
    {
        BindIfNeeded();
        RefreshWeaponIcons();
        RefreshStats();
    }

    private void RefreshWeaponIcons()
    {
        EquipDatabase database = DataPool.Instance != null ? DataPool.Instance.equipDatabase : null;
        SetEquipIcon(weaponL, database, DataConfig.leftHand);
        SetEquipIcon(weaponR, database, DataConfig.rightHand);
        SetEquipIcon(weaponBody, database, DataConfig.body);
        SetEquipIcon(weaponHead, database, DataConfig.head);
    }

    private static void SetEquipIcon(Image image, EquipDatabase database, int equipIndex)
    {
        if (image == null) return;
        bool valid = database != null && database.equip != null &&
            equipIndex > 0 && equipIndex < database.equip.Count;
        image.sprite = valid ? database.equip[equipIndex].myEquipSprite : null;
        image.enabled = image.sprite != null;
    }

    // DataConfig.playerDatas(4개 스탯, 장비 보너스만 누적되는 구버전 값)는 캐릭터 선택 화면에서
    // 배분한 실제 스탯 포인트를 전혀 반영하지 않는다. 실제 기본 스탯은 CharactorStatus 컴포넌트에
    // 있다(StatusUI → SpawnPlayer.PlayerInfoInit → CharactorStatus.InitStatus 경로로 채워짐).
    // 씬에 CharactorStatus 인스턴스가 정확히 하나뿐이라 FindFirstObjectByType으로 안전하게
    // 찾을 수 있다.
    //
    // 장비를 장착해도 그래프가 그대로였던 이유: CharactorStatus는 기본 스탯만 담고 있고,
    // 장비 스탯 보너스(stroffset 등)는 DataConfig.leftDa/rightDa/bodyDa/headDa(현재 장착된
    // EquipData 4개)에 들어있다. 이 둘을 합산해서 보여줘야 한다.
    private void RefreshStats()
    {
        CharactorStatus status = FindFirstObjectByType<CharactorStatus>();
        if (status == null)
        {
            Debug.LogWarning("[CharacterListUIStatusController] CharactorStatus를 찾지 못해 스탯을 갱신하지 못했습니다.", this);
            return;
        }

        int strBonus = 0, dexBonus = 0, wisBonus = 0, vitBonus = 0;
        AddEquipmentOffsets(DataConfig.leftDa, ref strBonus, ref dexBonus, ref wisBonus, ref vitBonus);
        // 양손무기는 leftDa와 rightDa가 같은 EquipData를 가리키므로 두 번 더하지 않는다.
        if (!ReferenceEquals(DataConfig.leftDa, DataConfig.rightDa))
            AddEquipmentOffsets(DataConfig.rightDa, ref strBonus, ref dexBonus, ref wisBonus, ref vitBonus);
        AddEquipmentOffsets(DataConfig.bodyDa, ref strBonus, ref dexBonus, ref wisBonus, ref vitBonus);
        AddEquipmentOffsets(DataConfig.headDa, ref strBonus, ref dexBonus, ref wisBonus, ref vitBonus);

        if (drawState != null)
        {
            // 레이더 차트 + 6개 스탯 텍스트(STR/DEX/INT/WIS/CAR/VIT)를 한 번에 갱신. INT/CAR은
            // 장비 보너스 시스템이 없어 0으로 둔다.
            drawState.SetStatus(status);
            drawState.SetEquipmentBonus(strBonus, dexBonus, 0, wisBonus, 0, vitBonus);
        }
        else
        {
            Debug.LogWarning("[CharacterListUIStatusController] 'Status' 자식에서 DrawState를 찾지 못했습니다.", this);
        }

        SetSlider(strSlider, status.STR + strBonus);
        SetSlider(vitSlider, status.VIT + vitBonus);
        SetSlider(dexSlider, status.DEX + dexBonus);
        SetSlider(wisSlider, status.WIS + wisBonus);
    }

    private static void AddEquipmentOffsets(EquipData equip, ref int str, ref int dex, ref int wis, ref int vit)
    {
        if (equip == null) return;
        str += equip.stroffset;
        dex += equip.dexoffset;
        wis += equip.wisoffset;
        vit += equip.vitoffset;
    }

    private static void SetSlider(Slider slider, int value)
    {
        if (slider != null) slider.value = value / 12f;
    }

    private Transform FindChild(string name)
    {
        return FindChildRecursive(transform, name);
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
