using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상자에서 나온 장비 보상을 생성하고, 슬롯이 이미 차 있을 때 비교 후 선택을 처리한다.
/// 장비 상태의 원본은 PlayerWeapon이며, 이 클래스는 그 위에서 "상자 보상 흐름"만 담당한다.
/// DataConfig는 골드 계산(GetSaleValue)과 저장 호환 용도로만 사용하고,
/// 신규 장비 상태의 원본으로는 사용하지 않는다.
/// 장비 인벤토리는 두지 않는다 — 보상은 획득 현장에서 즉시 장착되거나 판매된다.
/// </summary>
public static class TreasureEquipmentRewardPresenter
{
    /// <summary>
    /// 상자의 장비 보상 전체 흐름을 처리한다.
    /// Treasure가 이미 들고 있는 UI 참조(rewardImage, getBtns, get/giveUp/leftEquip/rightEquip)를
    /// 그대로 재사용하므로 씬에 새 버튼을 추가하거나 새로 연결할 필요가 없다.
    /// </summary>
    public static void PresentEquipmentReward(
        Treasure treasure,
        Image rewardImage,
        GameObject getBtns,
        Button get,
        Button giveUp,
        Button leftEquip,
        Button rightEquip)
    {
        if (treasure == null || rewardImage == null)
        {
            Debug.LogError("[TreasureEquipmentRewardPresenter] treasure 또는 rewardImage가 없습니다.");
            return;
        }

        PlayerWeapon playerWeapon = GetPlayerComponent<PlayerWeapon>();
        weaponSet weaponSetComponent = GetPlayerComponent<weaponSet>();

        if (playerWeapon == null)
        {
            Debug.LogError("[TreasureEquipmentRewardPresenter] Player에 PlayerWeapon 컴포넌트가 없어 장비 보상을 처리할 수 없습니다. " +
                "Player 프리팹에 PlayerWeapon을 추가해주세요.");
            treasure.CloseTreasureAndStopCor();
            return;
        }

        EquipData reward = GenerateReward();
        if (reward == null)
        {
            Debug.LogWarning("[TreasureEquipmentRewardPresenter] equipDatabase가 비어 있어 장비 보상을 생성하지 못했습니다.");
            treasure.CloseTreasureAndStopCor();
            return;
        }

        rewardImage.sprite = reward.myEquipSprite;
        GetItem getItem = rewardImage.GetComponent<GetItem>();

        // 매번 깨끗한 상태에서 시작하도록 리스너와 버튼 표시를 모두 초기화한다.
        getBtns.SetActive(true);
        get.onClick.RemoveAllListeners();
        giveUp.onClick.RemoveAllListeners();
        leftEquip.onClick.RemoveAllListeners();
        rightEquip.onClick.RemoveAllListeners();
        get.gameObject.SetActive(false);
        giveUp.gameObject.SetActive(false);
        leftEquip.gameObject.SetActive(false);
        rightEquip.gameObject.SetActive(false);

        switch (reward.weaponKind)
        {
            case WeaponKind.Head:
                ResolveSingleSlot(treasure, getItem, get, giveUp, playerWeapon, weaponSetComponent,
                    reward, PlayerEquipmentSlotType.Head, playerWeapon.Head.CurrentEquipment);
                break;

            case WeaponKind.Body:
                ResolveSingleSlot(treasure, getItem, get, giveUp, playerWeapon, weaponSetComponent,
                    reward, PlayerEquipmentSlotType.Body, playerWeapon.Body.CurrentEquipment);
                break;

            case WeaponKind.Hand:
                ResolveHand(treasure, getItem, get, giveUp, leftEquip, rightEquip,
                    playerWeapon, weaponSetComponent, reward);
                break;

            case WeaponKind.TwoHand:
                ResolveTwoHand(treasure, playerWeapon, weaponSetComponent, reward);
                break;
        }
    }

    // ------------------------------------------------------------------
    // 슬롯별 처리
    // ------------------------------------------------------------------

    /// <summary>머리/몸통처럼 슬롯이 하나뿐인 경우. 비어 있으면 즉시 장착, 차 있으면 [장착]/[50% 판매] 선택.</summary>
    private static void ResolveSingleSlot(
        Treasure treasure, GetItem getItem, Button get, Button giveUp,
        PlayerWeapon playerWeapon, weaponSet weaponSetComponent,
        EquipData reward, PlayerEquipmentSlotType slotType, EquipData currentEquipment)
    {
        if (currentEquipment == null)
        {
            playerWeapon.EquipInSlot(slotType, reward);
            RefreshVisuals(playerWeapon, weaponSetComponent);
            treasure.CloseTreasureAndStopCor();
            return;
        }

        if (getItem != null)
            getItem.Init(RewardState.Equipment, reward.weaponIndex, BuildComparisonText(currentEquipment, reward));

        get.gameObject.SetActive(true);
        giveUp.gameObject.SetActive(true);
        SetLabel(get, "장착");
        SetLabel(giveUp, $"50% 판매 (+{DataConfig.GetSaleValue(reward.cost)}G)");

        get.onClick.AddListener(() =>
        {
            EquipData removed = playerWeapon.EquipInSlot(slotType, reward);
            GrantSaleGold(removed);
            RefreshVisuals(playerWeapon, weaponSetComponent);
            treasure.CloseTreasureAndStopCor();
        });

        giveUp.onClick.AddListener(() =>
        {
            GrantSaleGold(reward);
            treasure.CloseTreasureAndStopCor();
        });
    }

    /// <summary>
    /// 한손 장비. 빈 손이 있으면 즉시 장착.
    /// 양손 다 차 있으면 [왼손 교체]/[오른손 교체]/[50% 판매] 세 가지 선택지를 제공한다.
    /// (스펙에는 손 선택만 명시돼 있지만, "보상이 그냥 사라지는 경우는 없게 한다"는 원칙과
    /// 다른 슬롯들과의 일관성을 위해 50% 판매 선택지를 함께 넣었다.)
    /// </summary>
    private static void ResolveHand(
        Treasure treasure, GetItem getItem, Button get, Button giveUp, Button leftEquip, Button rightEquip,
        PlayerWeapon playerWeapon, weaponSet weaponSetComponent, EquipData reward)
    {
        PlayerEquipmentSlotType? emptyHand = playerWeapon.FindEmptyHandSlot();
        if (emptyHand.HasValue)
        {
            playerWeapon.EquipHandInSlot(reward, emptyHand.Value);
            RefreshVisuals(playerWeapon, weaponSetComponent);
            treasure.CloseTreasureAndStopCor();
            return;
        }

        EquipData currentLeft = playerWeapon.LeftArm.CurrentEquipment;
        EquipData currentRight = playerWeapon.RightArm.CurrentEquipment;

        if (getItem != null)
        {
            string text = playerWeapon.IsTwoHandEquipped
                ? BuildComparisonText(currentLeft, reward)
                : BuildComparisonText(currentLeft, reward) + "\n\n" + BuildComparisonText(currentRight, reward);
            getItem.Init(RewardState.Equipment, reward.weaponIndex, text);
        }

        leftEquip.gameObject.SetActive(true);
        rightEquip.gameObject.SetActive(true);
        giveUp.gameObject.SetActive(true);
        SetLabel(leftEquip, "왼손 교체");
        SetLabel(rightEquip, "오른손 교체");
        SetLabel(giveUp, $"50% 판매 (+{DataConfig.GetSaleValue(reward.cost)}G)");

        leftEquip.onClick.AddListener(() =>
        {
            // 왼손이 양손 무기로 점유돼 있었다면 PlayerWeapon이 오른손도 함께 비우고
            // 그 양손 무기를 한 번만 반환한다 (중복 판매 없음).
            EquipData removed = playerWeapon.EquipHandInSlot(reward, PlayerEquipmentSlotType.LeftArm);
            GrantSaleGold(removed);
            RefreshVisuals(playerWeapon, weaponSetComponent);
            treasure.CloseTreasureAndStopCor();
        });

        rightEquip.onClick.AddListener(() =>
        {
            EquipData removed = playerWeapon.EquipHandInSlot(reward, PlayerEquipmentSlotType.RightArm);
            GrantSaleGold(removed);
            RefreshVisuals(playerWeapon, weaponSetComponent);
            treasure.CloseTreasureAndStopCor();
        });

        giveUp.onClick.AddListener(() =>
        {
            GrantSaleGold(reward);
            treasure.CloseTreasureAndStopCor();
        });
    }

    /// <summary>
    /// 양손 장비. 좌우 슬롯을 동시에 점유하며, 선택 UI 없이 즉시 처리한다.
    /// 기존에 서로 다른 한손 장비 두 개가 있었다면 각각 50%로 자동 판매하고,
    /// 기존이 같은 양손 장비였다면 한 번만 판매한다.
    /// </summary>
    private static void ResolveTwoHand(
        Treasure treasure, PlayerWeapon playerWeapon, weaponSet weaponSetComponent, EquipData reward)
    {
        playerWeapon.EquipTwoHandEquipment(reward, out EquipData removedLeft, out EquipData removedRight);
        GrantSaleGold(removedLeft);
        GrantSaleGold(removedRight);
        RefreshVisuals(playerWeapon, weaponSetComponent);
        treasure.CloseTreasureAndStopCor();
    }

    // ------------------------------------------------------------------
    // 보상 생성
    // ------------------------------------------------------------------

    private static EquipData GenerateReward()
    {
        if (DataPool.Instance == null || DataPool.Instance.equipDatabase == null) return null;

        var equipList = DataPool.Instance.equipDatabase.equip;
        if (equipList == null || equipList.Count == 0) return null;

        EquipData reward = equipList[Random.Range(0, equipList.Count)].Clone();

        int rarity = Random.Range(0, 4);
        reward.weapon = (weaponSt)rarity;

        int bonusRolls;
        switch (rarity)
        {
            case 1: reward.cost = 60; bonusRolls = 4; break;
            case 2: reward.cost = 100; bonusRolls = 6; break;
            case 3: reward.cost = 160; bonusRolls = 10; break;
            default: reward.cost = 40; bonusRolls = 0; break;
        }

        for (int i = 0; i < bonusRolls; i++)
        {
            switch (Random.Range(0, 6))
            {
                case 0: reward.stroffset++; break;
                case 1: reward.dexoffset++; break;
                case 2: reward.intoffset++; break;
                case 3: reward.wisoffset++; break;
                case 4: reward.caroffset++; break;
                case 5: reward.vitoffset++; break;
            }
        }

        return reward;
    }

    // ------------------------------------------------------------------
    // 공용 헬퍼
    // ------------------------------------------------------------------

    private static T GetPlayerComponent<T>() where T : Component
    {
        if (GameManagerInMain.Instance == null || GameManagerInMain.Instance.Player == null) return null;
        return GameManagerInMain.Instance.Player.GetComponent<T>();
    }

    /// <summary>기존 규칙 그대로: 판매가는 구매가의 50%다. equipment가 null이면 아무 일도 하지 않는다.</summary>
    private static void GrantSaleGold(EquipData equipment)
    {
        if (equipment == null || GameManagerInMain.Instance == null) return;
        int saleGold = DataConfig.GetSaleValue(equipment.cost);
        GameManagerInMain.Instance.SetGold(saleGold);
    }

    /// <summary>PlayerWeapon의 최신 슬롯 상태를 weaponSet(3D 모델)에 반영한다.</summary>
    private static void RefreshVisuals(PlayerWeapon playerWeapon, weaponSet weaponSetComponent)
    {
        if (playerWeapon == null || weaponSetComponent == null) return;

        int leftIndex = playerWeapon.LeftArm.CurrentEquipment?.weaponIndex ?? 0;
        int rightIndex = playerWeapon.RightArm.CurrentEquipment?.weaponIndex ?? 0;
        int bodyIndex = playerWeapon.Body.CurrentEquipment?.weaponIndex ?? 0;
        int headIndex = playerWeapon.Head.CurrentEquipment?.weaponIndex ?? 0;
        weaponSetComponent.EquipAdapt(leftIndex, rightIndex, bodyIndex, headIndex);
    }

    private static void SetLabel(Button button, string label)
    {
        if (button == null) return;
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = label;
    }

    private static string BuildComparisonText(EquipData current, EquipData reward)
    {
        string rewardPart = "새 장비: " + reward.cardname + "\n" + FormatStats(reward);
        string currentPart = current == null
            ? "현재 장착: 없음"
            : "현재 장착: " + current.cardname + "\n" + FormatStats(current);

        return rewardPart + "\n\n" + currentPart;
    }

    private static string FormatStats(EquipData equipment)
    {
        return $"STR {equipment.stroffset:+0;-0;0}  DEX {equipment.dexoffset:+0;-0;0}  INT {equipment.intoffset:+0;-0;0}\n" +
               $"WIS {equipment.wisoffset:+0;-0;0}  CAR {equipment.caroffset:+0;-0;0}  VIT {equipment.vitoffset:+0;-0;0}\n" +
               $"사거리 {equipment.attackRange}";
    }
}
