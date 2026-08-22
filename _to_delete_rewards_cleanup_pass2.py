# -*- coding: utf-8 -*-
def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

def apply(path, replacements):
    content = load(path)
    for i, (old, new) in enumerate(replacements, start=1):
        count = content.count(old)
        assert count == 1, (path, i, count, old[:120])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

p = "Assets/renew/Battle/Rewards/BattleChestRewardSystem.cs"
apply(p, [
    # 1) 필드 목록에서 죽은 장비 선택 UI 필드 제거
    (
'''    private GameObject equipmentChoicePanel;
    private TMP_Text equipmentChoiceText;
    private TMP_Text goldRewardText;
    private Button equipDefaultButton;
    private Button equipLeftButton;
    private Button equipRightButton;
    private MapInfo currentTile;''',
'''    private TMP_Text goldRewardText;
    private MapInfo currentTile;'''
    ),

    # 2) TryGetReward에 "결정만 하고 실제 데이터는 안 건드린다" 요약 추가
    (
'''    private bool TryGetReward(MapInfo tile, out PendingReward reward)
    {''',
'''    /// <summary>
    /// 이 타일이 이번에 어떤 보상을 줄지 결정만 하고 <c>pendingRewards</c>에 캐시한다.
    /// DataConfig(카드 보유 수, 골드, 장비 슬롯) 등 실제 플레이어 데이터는 이 단계에서 건드리지 않는다 —
    /// 실제 지급은 나중에 <see cref="ClaimCardReward"/>/<see cref="ClaimGoldReward"/>/<see cref="ClaimEquipment"/>가 한다.
    /// 이미 결정된 타일이면(다시 열거나 강제로 닫혔다 재오픈) 새로 굴리지 않고 캐시된 값을 그대로 돌려준다.
    /// </summary>
    private bool TryGetReward(MapInfo tile, out PendingReward reward)
    {'''
    ),

    # 3) BuildEligibleCards -> CollectCardRewardCandidates (선언 + 호출부) + 요약
    (
'''        List<CardData> cards = BuildEligibleCards();
        List<int> equipment = BuildEligibleEquipment();''',
'''        List<CardData> cards = CollectCardRewardCandidates();
        List<int> equipment = CollectEquipmentRewardIndices();'''
    ),
    (
'''    private List<CardData> BuildEligibleCards()
    {
        List<CardData> result = new List<CardData>();
        if (battleCardDatabase == null || originalCardDatabase == null) return result;
        foreach (BattleCardData battleCard in battleCardDatabase.Cards)
        {
            if (battleCard == null || battleCard.legacyCardIndex < 0) continue;
            int owned = DataConfig.CardsCount.TryGetValue(battleCard.legacyCardIndex, out int count) ? count : 0;
            CardData original = BattleCardConnector.FindOriginalCard(battleCard.legacyCardIndex, originalCardDatabase);
            if (owned < 2 && original != null) result.Add(original);
        }
        return result;
    }

    private List<int> BuildEligibleEquipment()
    {
        List<int> result = new List<int>();
        if (equipmentDatabase == null) return result;
        for (int i = 1; i < equipmentDatabase.equip.Count; i++)
            if (equipmentDatabase.equip[i] != null) result.Add(i);
        return result;
    }

    private EquipData CreateEquipmentReward(int index)
    {''',
'''    /// <summary>
    /// 이번 상자가 카드를 줄 수 있다면 그 후보 목록을 만든다. battleCardDatabase의 각 카드 중
    /// 원본 카드로 변환 가능하고(legacyCardIndex 매칭) 플레이어가 아직 2장 미만으로 보유한 카드만 포함한다
    /// (보유 상한을 넘는 카드는 상자 보상에서 제외).
    /// </summary>
    private List<CardData> CollectCardRewardCandidates()
    {
        List<CardData> result = new List<CardData>();
        if (battleCardDatabase == null || originalCardDatabase == null) return result;
        foreach (BattleCardData battleCard in battleCardDatabase.Cards)
        {
            if (battleCard == null || battleCard.legacyCardIndex < 0) continue;
            int owned = DataConfig.CardsCount.TryGetValue(battleCard.legacyCardIndex, out int count) ? count : 0;
            CardData original = BattleCardConnector.FindOriginalCard(battleCard.legacyCardIndex, originalCardDatabase);
            if (owned < 2 && original != null) result.Add(original);
        }
        return result;
    }

    /// <summary>
    /// 이번 상자가 장비를 줄 수 있다면 그 후보의 equipmentDatabase 인덱스 목록을 만든다.
    /// 인덱스 0은 "미장착"을 뜻하는 값이라 제외하고, 실제 EquipData가 채워진 인덱스만 포함한다.
    /// </summary>
    private List<int> CollectEquipmentRewardIndices()
    {
        List<int> result = new List<int>();
        if (equipmentDatabase == null) return result;
        for (int i = 1; i < equipmentDatabase.equip.Count; i++)
            if (equipmentDatabase.equip[i] != null) result.Add(i);
        return result;
    }

    /// <summary>
    /// 뽑힌 장비 인덱스의 원본 EquipData를 복제한 뒤, 상점과 같은 <c>BattleShopConfig.RollRarity</c>로
    /// 등급을 굴리고, 등급에 따라 가격(부위별 기본가 x 등급 배수)과 스탯 보너스(등급별 굴림 횟수만큼
    /// STR/WIS/DEX/VIT 중 하나씩 +1)를 부여한다. 상점 장비와 같은 등급 확률표를 재사용하기 위해
    /// Awake에서 shopConfig를 미리 로드해 둔다.
    /// </summary>
    private EquipData CreateEquipmentReward(int index)
    {'''
    ),

    # 4) PlayOpenSequence 요약
    (
'''    private IEnumerator PlayOpenSequence()
    {''',
'''    /// <summary>
    /// 레거시 상자 프리팹(TreasureChest_Start)의 Drop/Open 버튼 클릭을 코드로 그대로 흉내 내
    /// 원래 붙어 있던 연출(애니메이션·사운드)을 재사용한다. Drop 클릭 후 DropToReadySeconds만큼
    /// 기다렸다가 Open 클릭 + 뚜껑 이미지 전환 + 보상 이미지 표시를 하고, RewardDisplaySeconds만큼
    /// 더 보여준 뒤 <see cref="AutoClaimCurrentReward"/>로 자동 지급한다.
    /// </summary>
    private IEnumerator PlayOpenSequence()
    {'''
    ),

    # 5) PrepareRewardImages 요약 + 죽은 UI 참조 제거
    (
'''    private void PrepareRewardImages(PendingReward reward)
    {''',
'''    /// <summary>
    /// 상자를 열기 전, 보상 이미지 슬롯들을 미리 이번 보상에 맞게 채워 두되 화면에는 아직 표시하지 않는다
    /// (PlayOpenSequence가 뚜껑이 열리는 타이밍에 SetActive(true)로 실제로 보여준다).
    /// 카드/장비 보상이면 각각의 스프라이트를, 골드 보상이면 스프라이트 없이 금색 틴트만 준다.
    /// 이미지에 달려 있던 버튼은 리스너를 모두 지우고 비활성화한다 — 지급은 전부
    /// <see cref="AutoClaimCurrentReward"/>의 타이머로만 일어나고 클릭으로 트리거되지 않는다.
    /// </summary>
    private void PrepareRewardImages(PendingReward reward)
    {'''
    ),
    (
'''        if (goldRewardText != null)
        {
            goldRewardText.gameObject.SetActive(reward.Type == BattleChestRewardType.Gold);
            goldRewardText.text = reward.Type == BattleChestRewardType.Gold ? $"{reward.Gold}G" : string.Empty;
        }
        if (equipmentChoicePanel != null) equipmentChoicePanel.SetActive(false);
    }

    private bool CanClaim(BattleChestRewardType type)
    {''',
'''        if (goldRewardText != null)
        {
            goldRewardText.gameObject.SetActive(reward.Type == BattleChestRewardType.Gold);
            goldRewardText.text = reward.Type == BattleChestRewardType.Gold ? $"{reward.Gold}G" : string.Empty;
        }
    }

    /// <summary>
    /// 지금 이 보상 종류를 실제로 지급해도 되는 상태인지 검사한다. 뚜껑이 열려 보상이 준비됐고,
    /// 현재 열린 타일·보상이 있고, 요청한 타입과 실제 보상 타입이 같고, 아직 이 타일을 수령한 적이
    /// 없어야 true다. Claim* 계열 메서드가 실제 데이터를 건드리기 전에 공통으로 거치는 방어 검사다.
    /// </summary>
    private bool CanClaim(BattleChestRewardType type)
    {'''
    ),

    # 6) ClaimCardReward / ClaimGoldReward 요약(실제 데이터 반영 지점임을 명시)
    (
'''    private void ClaimCardReward()
    {
        if (!CanClaim(BattleChestRewardType.Card) || currentReward.Card == null) return;
        rewardReady = false;
        DataConfig.AddDic(currentReward.Card.index, 1);
        Debug.Log($"[Chest] Card reward: {currentReward.Card.name}", currentTile);
        CompleteCurrentReward();
    }

    private void ClaimGoldReward()
    {
        if (!CanClaim(BattleChestRewardType.Gold)) return;
        rewardReady = false;
        DataConfig.playerMoney += Mathf.Max(0, currentReward.Gold);
        Debug.Log($"[Chest] Gold reward: {currentReward.Gold}G", currentTile);
        CompleteCurrentReward();
    }''',
'''    /// <summary>카드 보상을 실제로 지급한다(<c>DataConfig.AddDic</c> — 여기서 처음 실제 보유 카드 수가 늘어난다).</summary>
    private void ClaimCardReward()
    {
        if (!CanClaim(BattleChestRewardType.Card) || currentReward.Card == null) return;
        rewardReady = false;
        DataConfig.AddDic(currentReward.Card.index, 1);
        Debug.Log($"[Chest] Card reward: {currentReward.Card.name}", currentTile);
        CompleteCurrentReward();
    }

    /// <summary>골드 보상을 실제로 지급한다(<c>DataConfig.playerMoney</c>에 여기서 처음 더해진다).</summary>
    private void ClaimGoldReward()
    {
        if (!CanClaim(BattleChestRewardType.Gold)) return;
        rewardReady = false;
        DataConfig.playerMoney += Mathf.Max(0, currentReward.Gold);
        Debug.Log($"[Chest] Gold reward: {currentReward.Gold}G", currentTile);
        CompleteCurrentReward();
    }'''
    ),

    # 7) 죽은 장비 선택 UI 삭제: OpenEquipmentChoice + Claim*Left/Right + ClaimEquipment 단순화
    (
'''    private void OpenEquipmentChoice()
    {
        if (!CanClaim(BattleChestRewardType.Equipment) || currentReward.Equipment == null) return;
        EquipData equipment = currentReward.Equipment;
        bool hand = equipment.weaponKind == WeaponKind.Hand;
        equipmentChoiceText.text = $"{equipment.cardname} [{equipment.weapon}]\\n" +
            $"STR+{equipment.stroffset} WIS+{equipment.wisoffset} " +
            $"DEX+{equipment.dexoffset} VIT+{equipment.vitoffset}";
        equipDefaultButton.gameObject.SetActive(!hand);
        equipLeftButton.gameObject.SetActive(hand);
        equipRightButton.gameObject.SetActive(hand);
        equipmentChoicePanel.SetActive(true);
    }

    private void ClaimEquipmentDefault() => ClaimEquipment(null);
    private void ClaimEquipmentLeft() => ClaimEquipment(true);
    private void ClaimEquipmentRight() => ClaimEquipment(false);

    private void ClaimEquipment(bool? equipLeft)
    {
        if (!CanClaim(BattleChestRewardType.Equipment) || currentReward.Equipment == null) return;
        rewardReady = false;
        EquipData equipment = currentReward.Equipment;
        if (equipment.weaponKind == WeaponKind.Hand && equipLeft.HasValue)
            DataConfig.EquipHandInSlot(equipment, equipLeft.Value);
        else
            DataConfig.GetWeapon(equipment);

        weaponSet view = BattleGameManager.Instance != null && BattleGameManager.Instance.CurrentPlayer != null
            ? BattleGameManager.Instance.CurrentPlayer.GetComponent<weaponSet>() : null;
        view?.EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head);
        Debug.Log($"[Chest] Equipment reward: {equipment.cardname} [{equipment.weapon}]", currentTile);
        CompleteCurrentReward();
    }''',
'''    /// <summary>
    /// 장비 보상을 실제로 지급한다. 항상 <c>DataConfig.GetWeapon</c>(빈 손에 자동 장착, 양손이 다
    /// 차있으면 왼손을 강제로 교체 — DataConfig.cs 자체 동작)을 거친다.
    /// (2026-08-22 정리, 사용자 확인: 원래 있던 "왼손/오른손 직접 선택" 확정 UI(OpenEquipmentChoice +
    /// 선택 패널·버튼들)는 AutoClaimCurrentReward가 항상 이 메서드를 인자 없이 호출하도록 바뀐 뒤로
    /// 어디서도 호출되지 않는 죽은 코드였다 — 양손이 다 차있어도 플레이어에게 묻지 않고 그냥 왼손을
    /// 교체해버리는 게 현재의 실제 동작이다. 되살릴 필요가 생기면 DataConfig.EquipHandInSlot(equipment,
    /// equipLeft)로 특정 손을 강제 교체하는 경로를 이 메서드에 다시 연결하면 된다.)
    /// </summary>
    private void ClaimEquipment()
    {
        if (!CanClaim(BattleChestRewardType.Equipment) || currentReward.Equipment == null) return;
        rewardReady = false;
        EquipData equipment = currentReward.Equipment;
        DataConfig.GetWeapon(equipment);

        weaponSet view = BattleGameManager.Instance != null && BattleGameManager.Instance.CurrentPlayer != null
            ? BattleGameManager.Instance.CurrentPlayer.GetComponent<weaponSet>() : null;
        view?.EquipAdapt(DataConfig.leftHand, DataConfig.rightHand, DataConfig.body, DataConfig.head);
        Debug.Log($"[Chest] Equipment reward: {equipment.cardname} [{equipment.weapon}]", currentTile);
        CompleteCurrentReward();
    }'''
    ),

    # 8) AutoClaimCurrentReward의 호출부를 새 시그니처에 맞춤
    (
'''            case BattleChestRewardType.Equipment:
                ClaimEquipment(null);
                break;''',
'''            case BattleChestRewardType.Equipment:
                ClaimEquipment();
                break;'''
    ),

    # 9) CompleteCurrentReward / Close 요약 + Close()에서 죽은 UI 참조 제거
    (
'''    private void CompleteCurrentReward()
    {
        openedTiles.Add(currentTile);
        pendingRewards.Remove(currentTile);
        Close();
    }

    private void Close()
    {
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        openingRoutine = null;
        rewardReady = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
        if (equipmentChoicePanel != null) equipmentChoicePanel.SetActive(false);
        if (lidClosed != null) lidClosed.SetActive(true);
        if (lidOpened != null) lidOpened.SetActive(false);
        SetRewardImagesActive(false);
        currentTile = null;
        currentReward = null;
        ReleaseModalLock();
    }''',
'''    /// <summary>
    /// 보상이 실제로 지급된 뒤 이 타일을 "완전히 열림"으로 확정한다(이제부터 재오픈 불가).
    /// TryOpen 시점이 아니라 여기서만 openedTiles에 추가하는 이유는 지급 전에 강제로 닫히면
    /// (예: ForceClose) pendingRewards의 캐시된 보상을 유지한 채 같은 상자를 다시 열 수 있게
    /// 하기 위해서다.
    /// </summary>
    private void CompleteCurrentReward()
    {
        openedTiles.Add(currentTile);
        pendingRewards.Remove(currentTile);
        Close();
    }

    /// <summary>
    /// 상자 UI를 초기 상태(뚜껑 닫힘, 보상 이미지 숨김, 캔버스 비활성)로 되돌리고 입력 잠금을 해제한다.
    /// 정상 지급 완료(<see cref="CompleteCurrentReward"/>) 경로와 강제 종료(<see cref="ForceClose"/>)
    /// 경로가 공유하는 유일한 정리 지점이다.
    /// </summary>
    private void Close()
    {
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        openingRoutine = null;
        rewardReady = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
        if (lidClosed != null) lidClosed.SetActive(true);
        if (lidOpened != null) lidOpened.SetActive(false);
        SetRewardImagesActive(false);
        currentTile = null;
        currentReward = null;
        ReleaseModalLock();
    }'''
    ),

    # 10) EnsureView()에서 죽은 UI 생성 호출 제거 + 관련 필드 채우기 코드 정리
    (
'''        if (rewardImages.Length > 0)
        {
            goldRewardText = CreateText(rewardImages[0].rectTransform, "Gold Reward", Vector2.zero,
                rewardImages[0].rectTransform.rect.size, 42f);
            goldRewardText.color = Color.white;
            goldRewardText.gameObject.SetActive(false);
        }
        CreateEquipmentChoiceView(root.GetComponent<RectTransform>());
    }

    private void CreateEquipmentChoiceView(RectTransform parent)
    {
        RectTransform panel = new GameObject("Equipment Choice", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(parent, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(680f, 300f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.98f);
        equipmentChoicePanel = panel.gameObject;
        equipmentChoiceText = CreateText(panel, "Equipment", new Vector2(0f, 55f), new Vector2(620f, 150f), 25f);
        equipDefaultButton = CreateButton(panel, "EQUIP", new Vector2(0f, -95f), ClaimEquipmentDefault);
        equipLeftButton = CreateButton(panel, "LEFT", new Vector2(-130f, -95f), ClaimEquipmentLeft);
        equipRightButton = CreateButton(panel, "RIGHT", new Vector2(130f, -95f), ClaimEquipmentRight);
        equipmentChoicePanel.SetActive(false);
    }''',
'''        if (rewardImages.Length > 0)
        {
            goldRewardText = CreateText(rewardImages[0].rectTransform, "Gold Reward", Vector2.zero,
                rewardImages[0].rectTransform.rect.size, 42f);
            goldRewardText.color = Color.white;
            goldRewardText.gameObject.SetActive(false);
        }
    }'''
    ),
])
