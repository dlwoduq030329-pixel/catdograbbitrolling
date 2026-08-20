using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Store 타일용 카드 전용 상점 MVP. 장비/인벤토리/자동 장착 로직을 포함하지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class BattleCardShopSystem : MonoBehaviour
{
    private const int SlotCount = 6;
    // Item01 is 158x268 and the card image is naturally 100x150.8; at this scale the
    // image reaches ~150 wide / ~227 tall, filling the slot without reaching the
    // (always-visible) price text near the bottom.
    private const float OfferImageScale = 1.5f;
    private enum OfferKind { None, Card, Equipment }
    private sealed class StoreState
    {
        internal readonly OfferKind[] Kinds = new OfferKind[SlotCount];
        internal readonly int[] OfferedCards = { -1, -1, -1, -1, -1, -1 };
        internal readonly EquipData[] OfferedEquipment = new EquipData[SlotCount];
        internal readonly bool[] Sold = new bool[SlotCount];
        internal int RerollPrice = 10;
    }

    private readonly Dictionary<MapInfo, StoreState> stores = new Dictionary<MapInfo, StoreState>();
    private BattleCardDatabase battleCardDatabase;
    private CardDatabase originalCardDatabase;
    private EquipDatabase equipmentDatabase;
    private BattleShopConfig shopConfig;
    private MapInfo currentStore;
    private StoreState currentState;
    private Canvas canvas;
    private GameObject viewRoot;
    private TMP_Text goldText;
    private TMP_Text rerollText;
    private Button rerollButton;
    private Button[] offerButtons;
    private Image[] offerImages;
    private TMP_Text[] offerTexts;
    private TMP_Text[] offerPriceTexts;
    private GameObject equipmentConfirmPanel;
    private TMP_Text equipmentConfirmText;
    private Button equipmentConfirmButton;
    private Button equipmentLeftButton;
    private Button equipmentRightButton;
    private Button equipmentCancelButton;
    private int pendingEquipmentSlot = -1;
    private bool holdsModalLock;
    private TMP_Text tagText;
    private TMP_Text propertyText;
    private TMP_Text damageText;
    private TMP_Text equipmentInfoText;
    private Image hoverPreviewImage;
    private GameObject selectedImageRoot;
    private TMP_Text selectedCardNameText;
    private InventoryStore[] ownedCardSlots;
    private EquipStore[] ownedEquipmentSlots;
    private ScrollRect ownedInventoryScroll;
    private Button purchaseButton;
    private int selectedPurchaseSlot = -1;
    private enum PurchaseButtonMode { None, BuyOffer, SellCard, SellEquipment }
    private PurchaseButtonMode purchaseButtonMode = PurchaseButtonMode.None;
    private int selectedSellCardIndex = -1;
    private EquipState selectedSellEquipState;
    private bool hasSelectedSellEquip;

    private void Update()
    {
        if (viewRoot == null || !viewRoot.activeSelf || !Input.GetKeyDown(KeyCode.Escape)) return;

        // 장비 구매 확인창 또는 카드/장비 선택(구매·판매 상세 미리보기)이 열려 있으면
        // ESC로 상점 전체를 나가는 대신 그 선택만 취소한다. 아무것도 선택된 게 없을 때만
        // 상점을 닫는다.
        if (pendingEquipmentSlot != -1)
        {
            CancelEquipmentPurchase();
            return;
        }
        if (purchaseButtonMode != PurchaseButtonMode.None)
        {
            ResetPurchaseSelection();
            HideOfferDetails();
            return;
        }
        Close();
    }

    public void Configure(BattleCardDatabase battleCards, CardDatabase originalCards)
    {
        battleCardDatabase = battleCards;
        originalCardDatabase = originalCards;
        equipmentDatabase = BattleEquipmentDatabaseReference.Load()?.Database;
        shopConfig = BattleShopConfig.Load();
    }

    public bool TryEnter(MapInfo tile)
    {
        if (tile == null || tile.Type != TileType.Store) return false;
        EnsureView();
        if (viewRoot == null)
        {
            Debug.LogError("[Shop] 상점 UI를 생성하지 못했습니다.", this);
            return false;
        }

        currentStore = tile;
        if (!stores.TryGetValue(tile, out currentState))
        {
            currentState = new StoreState();
            currentState.RerollPrice = shopConfig != null ? shopConfig.initialRerollPrice : 10;
            stores.Add(tile, currentState);
            GenerateOffers(currentState);
        }
        viewRoot.SetActive(true);
        AcquireModalLock();
        BattleGameManager.Instance?.SetShopHudVisible(false, viewRoot);
        RefreshView();
        Debug.Log($"[Shop] {tile.name} 상점 진입", tile);
        return true;
    }

    private void GenerateOffers(StoreState state)
    {
        List<int> cardCandidates = BuildEligibleCards();
        List<int> equipmentCandidates = new List<int>();
        if (equipmentDatabase != null)
            for (int i = 1; i < equipmentDatabase.equip.Count; i++) equipmentCandidates.Add(i);
        List<int> cardFallbacks = new List<int>(cardCandidates);
        List<int> equipmentFallbacks = new List<int>(equipmentCandidates);

        List<OfferKind> layout = new List<OfferKind>(SlotCount);
        int cardSlots = shopConfig != null ? shopConfig.cardSlots : 3;
        int equipmentSlots = shopConfig != null ? shopConfig.equipmentSlots : 3;
        for (int i = 0; i < Mathf.Min(cardSlots, SlotCount); i++) layout.Add(OfferKind.Card);
        for (int i = 0; i < Mathf.Min(equipmentSlots, SlotCount - layout.Count); i++) layout.Add(OfferKind.Equipment);
        while (layout.Count < SlotCount) layout.Add(OfferKind.None);
        for (int i = layout.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (layout[i], layout[swap]) = (layout[swap], layout[i]);
        }

        for (int i = 0; i < SlotCount; i++)
        {
            state.Sold[i] = false;
            state.Kinds[i] = OfferKind.None;
            state.OfferedCards[i] = -1;
            state.OfferedEquipment[i] = null;

            OfferKind requestedKind = layout[i];
            if (requestedKind == OfferKind.Card && cardCandidates.Count == 0 && equipmentCandidates.Count > 0)
                requestedKind = OfferKind.Equipment;
            else if (requestedKind == OfferKind.Equipment && equipmentCandidates.Count == 0 && cardCandidates.Count > 0)
                requestedKind = OfferKind.Card;

            if (requestedKind == OfferKind.Card && cardCandidates.Count > 0)
            {
                int pick = Random.Range(0, cardCandidates.Count);
                state.Kinds[i] = OfferKind.Card;
                state.OfferedCards[i] = cardCandidates[pick];
                cardCandidates.RemoveAt(pick);
            }
            else if (requestedKind == OfferKind.Equipment && equipmentCandidates.Count > 0)
            {
                int equipmentIndex = PickEquipmentCandidate(equipmentCandidates);
                equipmentCandidates.Remove(equipmentIndex);
                state.Kinds[i] = OfferKind.Equipment;
                state.OfferedEquipment[i] = CreateRandomEquipment(equipmentIndex);
            }

            // A small database can contain fewer than six unique, currently eligible
            // products. Never leave a visible shop slot empty: reuse a card first and
            // equipment only when no card reward is available.
            if (state.Kinds[i] == OfferKind.None && cardFallbacks.Count > 0)
            {
                state.Kinds[i] = OfferKind.Card;
                state.OfferedCards[i] = cardFallbacks[Random.Range(0, cardFallbacks.Count)];
            }
            else if (state.Kinds[i] == OfferKind.None && equipmentFallbacks.Count > 0)
            {
                state.Kinds[i] = OfferKind.Equipment;
                state.OfferedEquipment[i] = CreateRandomEquipment(PickEquipmentCandidate(equipmentFallbacks));
            }
        }
    }

    private int PickEquipmentCandidate(List<int> candidates)
    {
        if (candidates == null || candidates.Count == 0) return -1;
        WeaponKind preferred = shopConfig != null
            ? shopConfig.RollEquipmentKind(DataConfig.stage)
            : equipmentDatabase.equip[candidates[0]].weaponKind;
        List<int> pool = candidates.FindAll(index => equipmentDatabase.equip[index].weaponKind == preferred);
        if (pool.Count == 0) pool = candidates;

        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++) totalWeight += IsEquipped(pool[i]) ? 1 : 4;
        int roll = Random.Range(0, Mathf.Max(1, totalWeight));
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= IsEquipped(pool[i]) ? 1 : 4;
            if (roll < 0) return pool[i];
        }
        return pool[0];
    }

    private static bool IsEquipped(int equipmentIndex)
    {
        return (DataConfig.leftDa != null && DataConfig.leftDa.weaponIndex == equipmentIndex) ||
               (DataConfig.rightDa != null && DataConfig.rightDa.weaponIndex == equipmentIndex) ||
               (DataConfig.bodyDa != null && DataConfig.bodyDa.weaponIndex == equipmentIndex) ||
               (DataConfig.headDa != null && DataConfig.headDa.weaponIndex == equipmentIndex);
    }

    private EquipData CreateRandomEquipment(int equipmentIndex)
    {
        List<EquipData> equipment = equipmentDatabase.equip;
        EquipData offer = equipment[equipmentIndex].Clone();
        weaponSt rarity = shopConfig != null
            ? shopConfig.RollRarity(DataConfig.stage)
            : (weaponSt)Random.Range(0, Mathf.Clamp(DataConfig.stage, 1, 4));
        offer.weapon = rarity;

        int bonusRolls;
        int basePrice = offer.weaponKind == WeaponKind.TwoHand ? 90 :
            offer.weaponKind == WeaponKind.Body ? 75 : offer.weaponKind == WeaponKind.Head ? 70 : 60;
        switch (offer.weapon)
        {
            case weaponSt.Rare: offer.cost = Mathf.RoundToInt(basePrice * 1.6f); bonusRolls = 4; break;
            case weaponSt.Epic: offer.cost = Mathf.RoundToInt(basePrice * 2.5f); bonusRolls = 6; break;
            case weaponSt.Legendary: offer.cost = basePrice * 4; bonusRolls = 10; break;
            default: offer.cost = basePrice; bonusRolls = 0; break;
        }

        for (int i = 0; i < bonusRolls; i++)
        {
            switch (Random.Range(0, 4))
            {
                case 0: offer.stroffset++; break;
                case 1: offer.wisoffset++; break;
                case 2: offer.dexoffset++; break;
                case 3: offer.vitoffset++; break;
            }
        }

        return offer;
    }

    private List<int> BuildEligibleCards()
    {
        List<int> result = new List<int>();
        if (battleCardDatabase == null || originalCardDatabase == null) return result;
        foreach (BattleCardData battleCard in battleCardDatabase.Cards)
        {
            if (battleCard == null || battleCard.legacyCardIndex < 0) continue;
            int owned = DataConfig.CardsCount.TryGetValue(battleCard.legacyCardIndex, out int count) ? count : 0;
            if (owned >= 2) continue;
            if (BattleCardConnector.FindOriginalCard(battleCard.legacyCardIndex, originalCardDatabase) != null)
                result.Add(battleCard.legacyCardIndex);
        }
        return result;
    }

    private void Buy(int slot)
    {
        if (currentState == null || slot < 0 || slot >= SlotCount || currentState.Sold[slot]) return;

        if (currentState.Kinds[slot] == OfferKind.Equipment)
        {
            BuyEquipment(slot);
            return;
        }
        if (currentState.Kinds[slot] != OfferKind.Card || currentState.OfferedCards[slot] < 0) return;

        int index = currentState.OfferedCards[slot];
        CardData card = BattleCardConnector.FindOriginalCard(index, originalCardDatabase);
        if (card == null) return;
        int owned = DataConfig.CardsCount.TryGetValue(index, out int count) ? count : 0;
        int price = Mathf.Max(0, card.cardCost * 2);
        if (owned >= 2) { Debug.LogWarning($"[Shop] {card.name} 보유 한도 2장", this); RefreshView(); return; }
        if (DataConfig.playerMoney < price) { Debug.LogWarning($"[Shop] 골드 부족: {price}G 필요", this); return; }

        DataConfig.playerMoney -= price;
        DataConfig.AddDic(index, 1);
        currentState.Sold[slot] = true;
        Debug.Log($"[Shop] 카드 구매: {card.name} / {price}G", this);
        RefreshView();
    }

    private void BuyEquipment(int slot)
    {
        EquipData equipment = currentState.OfferedEquipment[slot];
        if (equipment == null) return;
        pendingEquipmentSlot = slot;
        ConfirmEquipmentPurchaseInHand(null);
    }

    private void ConfirmEquipmentPurchase()
    {
        ConfirmEquipmentPurchaseInHand(null);
    }

    private void ConfirmEquipmentPurchaseLeft()
    {
        ConfirmEquipmentPurchaseInHand(true);
    }

    private void ConfirmEquipmentPurchaseRight()
    {
        ConfirmEquipmentPurchaseInHand(false);
    }

    private void ConfirmEquipmentPurchaseInHand(bool? equipLeft)
    {
        int slot = pendingEquipmentSlot;
        if (currentState == null || slot < 0 || slot >= SlotCount) return;
        EquipData equipment = currentState.OfferedEquipment[slot];
        if (equipment == null || currentState.Sold[slot]) return;
        int price = Mathf.Max(0, equipment.cost);
        if (DataConfig.playerMoney < price)
        {
            Debug.LogWarning($"[Shop] Not enough gold. Equipment costs {price}G.", this);
            return;
        }

        DataConfig.playerMoney -= price;
        if (equipment.weaponKind == WeaponKind.Hand && equipLeft.HasValue)
            DataConfig.EquipHandInSlot(equipment, equipLeft.Value);
        else
            DataConfig.GetWeapon(equipment);
        ApplyEquipmentVisual();
        currentState.Sold[slot] = true;
        pendingEquipmentSlot = -1;
        if (equipmentConfirmPanel != null) equipmentConfirmPanel.SetActive(false);
        Debug.Log($"[Shop] Equipment purchased: {equipment.cardname} ({equipment.weapon}) / {price}G", this);
        RefreshView();
    }

    /// <summary>
    /// weaponSet(main.unity 구버전 캐릭터)도 BattlePlayerEquip(Assets/Game/Characters의
    /// 다른 캐릭터 세트)도 실제로 Battle씬(moon.unity)에서 SpawnPlayer가 생성하는 프리팹
    /// (Assets/renew/Battle/Player/Prefabs/Bunny_Player 등)에는 붙어있지 않았다. 셋 다
    /// 골격 본 이름(handslot.l_end / handslot.r_end / chest / head_end)은 동일해서,
    /// main.unity의 weaponSet Inspector 참조가 가리키던 것과 같은 이름으로 런타임에 찾아
    /// 붙이는 BattleEquipVisualBinder를 사용한다. 프리팹 파일은 건드리지 않는다.
    ///
    /// DataPool.Instance.equipDatabase가 Inspector에서 비어 있는(EquipStore.Init()과 같은
    /// 원인) 문제도 함께 있어, equipDatabase를 먼저 채워 넣은 뒤 갱신한다.
    /// </summary>
    private void ApplyEquipmentVisual()
    {
        if (BattleGameManager.Instance == null || BattleGameManager.Instance.CurrentPlayer == null)
        {
            Debug.LogWarning("[Shop] CurrentPlayer가 없어 장비 모델을 갱신하지 못했습니다.", this);
            return;
        }
        GameObject playerObject = BattleGameManager.Instance.CurrentPlayer;

        if (DataPool.Instance != null && DataPool.Instance.equipDatabase == null && equipmentDatabase != null)
            DataPool.Instance.equipDatabase = equipmentDatabase;

        if (DataPool.Instance == null || DataPool.Instance.equipDatabase == null)
        {
            Debug.LogError("[Shop] DataPool.Instance.equipDatabase가 비어 있어 장비 모델을 갱신하지 못했습니다.", this);
            return;
        }

        try
        {
            BattleEquipVisualBinder equipmentView = BattleComponentResolver.GetOrAdd<BattleEquipVisualBinder>(playerObject, null);
            equipmentView.Refresh();
            CharacterListUIStatusController statusController =
                FindFirstObjectByType<CharacterListUIStatusController>(FindObjectsInactive.Include);
            statusController?.Refresh();
            Debug.Log($"[Shop] 장비 모델 갱신: L={DataConfig.leftHand} R={DataConfig.rightHand} Body={DataConfig.body} Head={DataConfig.head} (player={playerObject.name})", this);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void CancelEquipmentPurchase()
    {
        pendingEquipmentSlot = -1;
        if (equipmentConfirmPanel != null) equipmentConfirmPanel.SetActive(false);
    }

    private static EquipData GetComparableEquipment(WeaponKind kind)
    {
        switch (kind)
        {
            case WeaponKind.Body: return DataConfig.bodyDa;
            case WeaponKind.Head: return DataConfig.headDa;
            case WeaponKind.TwoHand: return DataConfig.leftDa;
            default: return DataConfig.leftDa ?? DataConfig.rightDa;
        }
    }

    private static int GetReplacementRefund(WeaponKind kind)
    {
        if (kind == WeaponKind.Body) return DataConfig.GetSaleValue(DataConfig.bodyDa != null ? DataConfig.bodyDa.cost : 0);
        if (kind == WeaponKind.Head) return DataConfig.GetSaleValue(DataConfig.headDa != null ? DataConfig.headDa.cost : 0);
        if (kind == WeaponKind.TwoHand)
        {
            int left = DataConfig.GetSaleValue(DataConfig.leftDa != null ? DataConfig.leftDa.cost : 0);
            int right = ReferenceEquals(DataConfig.leftDa, DataConfig.rightDa)
                ? 0 : DataConfig.GetSaleValue(DataConfig.rightDa != null ? DataConfig.rightDa.cost : 0);
            return left + right;
        }
        if (DataConfig.leftDa == null || DataConfig.rightDa == null) return 0;
        return DataConfig.GetSaleValue(DataConfig.leftDa.cost);
    }

    private static string FormatEquipment(EquipData equipment)
    {
        return equipment == null
            ? "EMPTY"
            : $"{equipment.cardname} [{equipment.weapon}] STR+{equipment.stroffset} WIS+{equipment.wisoffset} DEX+{equipment.dexoffset} VIT+{equipment.vitoffset}";
    }

    private static int GetSlotRefund(EquipData equipment)
    {
        return DataConfig.GetSaleValue(equipment != null ? equipment.cost : 0);
    }

    private void Reroll()
    {
        if (currentState == null) return;
        if (DataConfig.playerMoney < currentState.RerollPrice)
        { Debug.LogWarning($"[Shop] 리롤 골드 부족: {currentState.RerollPrice}G 필요", this); return; }
        DataConfig.playerMoney -= currentState.RerollPrice;
        Debug.Log($"[Shop] 상품 리롤 / {currentState.RerollPrice}G", this);
        int maximumPrice = shopConfig != null ? shopConfig.maximumRerollPrice : 160;
        currentState.RerollPrice = Mathf.Min(maximumPrice, currentState.RerollPrice * 2);
        ResetPurchaseSelection();
        GenerateOffers(currentState);
        RefreshView();
    }

    private void Close()
    {
        CancelEquipmentPurchase();
        ResetPurchaseSelection();
        HideOfferDetails();
        if (viewRoot != null) viewRoot.SetActive(false);
        BattleGameManager.Instance?.SetShopHudVisible(true);
        ReleaseModalLock();
        currentStore = null;
        currentState = null;
    }

    public void ForceClose()
    {
        Close();
    }

    private void AcquireModalLock()
    {
        if (holdsModalLock) return;
        BattleGameManager.Instance?.BeginModalInteraction();
        holdsModalLock = true;
    }

    private void ReleaseModalLock()
    {
        if (!holdsModalLock) return;
        holdsModalLock = false;
        BattleGameManager.Instance?.EndModalInteraction();
    }

    private void OnDisable()
    {
        ReleaseModalLock();
    }

    private void RefreshView()
    {
        if (viewRoot == null || currentState == null) return;
        FillExistingEmptySlots(currentState);
        if (goldText != null) goldText.text = $"Gold : {DataConfig.playerMoney}G";
        if (rerollText != null) rerollText.text = $"REROLL {currentState.RerollPrice}G";
        if (rerollButton != null)
            rerollButton.interactable = DataConfig.playerMoney >= currentState.RerollPrice;
        for (int i = 0; i < SlotCount; i++)
        {
            CardData card = currentState.Kinds[i] == OfferKind.Card && currentState.OfferedCards[i] >= 0
                ? BattleCardConnector.FindOriginalCard(currentState.OfferedCards[i], originalCardDatabase) : null;
            EquipData equipment = currentState.Kinds[i] == OfferKind.Equipment
                ? currentState.OfferedEquipment[i] : null;
            bool hasOffer = card != null || equipment != null;
            int price = card != null ? Mathf.Max(0, card.cardCost * 2) :
                equipment != null ? Mathf.Max(0, equipment.cost) : 0;
            bool canAfford = DataConfig.playerMoney >= price;
            bool atCardLimit = card != null &&
                DataConfig.CardsCount.TryGetValue(card.index, out int ownedCount) && ownedCount >= 2;
            if (offerButtons != null && i < offerButtons.Length && offerButtons[i] != null)
                offerButtons[i].interactable = hasOffer && !currentState.Sold[i] && canAfford && !atCardLimit;
            if (offerImages != null && i < offerImages.Length && offerImages[i] != null)
            {
                Sprite sprite = card != null ? CardArtResolver.ResolveDisplaySprite(card.myCardSprite) :
                    equipment != null ? equipment.myEquipSprite : null;
                offerImages[i].sprite = sprite;
                offerImages[i].enabled = sprite != null;

                CardCostLabelView offerCostLabel = CardCostLabelView.Ensure(offerImages[i].transform);
                if (offerCostLabel != null)
                {
                    if (card != null)
                    {
                        offerCostLabel.Show();
                        offerCostLabel.SetCost(card.cost, card.rare);
                    }
                    else
                    {
                        offerCostLabel.Hide();
                    }
                }
            }
            if (offerTexts != null && i < offerTexts.Length && offerTexts[i] != null)
            {
                offerTexts[i].text = string.Empty;
                offerTexts[i].gameObject.SetActive(false);
            }
            if (offerPriceTexts != null && i < offerPriceTexts.Length && offerPriceTexts[i] != null)
            {
                offerPriceTexts[i].color = !canAfford ? Color.red : Color.white;
                offerPriceTexts[i].text = !hasOffer ? string.Empty : currentState.Sold[i] ? "SOLD OUT" :
                    atCardLimit ? "MAX" : $"{price}G";
                offerPriceTexts[i].enableWordWrapping = false;
                offerPriceTexts[i].overflowMode = TextOverflowModes.Overflow;
                offerPriceTexts[i].maxVisibleLines = 1;
            }
        }
        RefreshOwnedInventory();
    }

    private void FillExistingEmptySlots(StoreState state)
    {
        List<int> cards = BuildEligibleCards();
        List<int> equipment = new List<int>();
        if (equipmentDatabase != null)
            for (int i = 1; i < equipmentDatabase.equip.Count; i++) equipment.Add(i);

        for (int i = 0; i < SlotCount; i++)
        {
            if (state.Kinds[i] != OfferKind.None) continue;
            if (cards.Count > 0)
            {
                state.Kinds[i] = OfferKind.Card;
                state.OfferedCards[i] = cards[Random.Range(0, cards.Count)];
            }
            else if (equipment.Count > 0)
            {
                state.Kinds[i] = OfferKind.Equipment;
                state.OfferedEquipment[i] = CreateRandomEquipment(PickEquipmentCandidate(equipment));
            }
        }
    }

    private static Color GetRarityColor(weaponSt rarity)
    {
        switch (rarity)
        {
            case weaponSt.Rare: return Color.green;
            case weaponSt.Epic: return new Color(0.64f, 0.21f, 0.93f, 1f);
            case weaponSt.Legendary: return Color.yellow;
            default: return Color.white;
        }
    }

    private void EnsureView()
    {
        if (viewRoot != null) return;
        if (TryBindSceneStoreView()) return;
        Debug.LogError("[Shop] Battle Canvas 아래에서 Event_Store 프리팹 인스턴스를 찾지 못했습니다.", this);
    }

    /// <summary>Battle Canvas에 배치된 Event_Store 인스턴스의 기존 슬롯과 텍스트만 연결한다.</summary>
    private bool TryBindSceneStoreView()
    {
        Transform storeRoot = null;
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.gameObject.name == "Event_Store")
            {
                storeRoot = candidate;
                break;
            }
        }
        if (storeRoot == null) return false;

        viewRoot = storeRoot.gameObject;
        canvas = storeRoot.GetComponentInParent<Canvas>(true);
        if (!BattleLegacyStoreViewAdapter.TryBind(viewRoot, SlotCount, out BattleLegacyStoreViewAdapter.Binding binding))
        {
            viewRoot = null;
            canvas = null;
            return false;
        }

        offerButtons = binding.Buttons;
        offerImages = binding.CardImages;
        offerTexts = binding.CardNames;
        offerPriceTexts = binding.CardPrices;
        goldText = binding.GoldText;
        rerollButton = binding.RerollButton;
        rerollText = binding.RerollText;
        tagText = FindNamedComponent<TMP_Text>(storeRoot, "TText");
        propertyText = FindNamedComponent<TMP_Text>(storeRoot, "SText");
        damageText = FindNamedComponent<TMP_Text>(storeRoot, "PText");
        equipmentInfoText = FindNamedComponent<TMP_Text>(storeRoot, "InText");
        hoverPreviewImage = binding.PreviewImage;
        Transform selectedImage = FindNamedTransform(storeRoot, "SelectedImage");
        selectedImageRoot = selectedImage != null ? selectedImage.gameObject : null;
        selectedCardNameText = selectedImage != null
            ? FindNamedComponent<TMP_Text>(selectedImage, "CardName")
            : null;

        Transform inventoryRoot = FindNamedTransform(storeRoot, "Inventory");
        ownedInventoryScroll = inventoryRoot != null ? inventoryRoot.GetComponent<ScrollRect>() : null;
        ownedCardSlots = inventoryRoot != null
            ? inventoryRoot.GetComponentsInChildren<InventoryStore>(true)
            : System.Array.Empty<InventoryStore>();
        Transform equipmentRoot = FindNamedTransform(storeRoot, "EquipMent");
        ownedEquipmentSlots = equipmentRoot != null
            ? equipmentRoot.GetComponentsInChildren<EquipStore>(true)
            : System.Array.Empty<EquipStore>();
        purchaseButton = FindNamedComponent<Button>(storeRoot, "BuySellButton");
        if (purchaseButton != null)
        {
            purchaseButton.onClick = new Button.ButtonClickedEvent();
            purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
            purchaseButton.gameObject.SetActive(false);
        }

        // EscButton keeps a prefab-authored persistent onClick that calls
        // Event_Store.SetActive(false) directly, bypassing Close() (and therefore
        // SetShopHudVisible(true)/EndModalInteraction). Replacing the event object
        // drops that legacy call and routes the button through our own cleanup.
        if (binding.EscButton != null)
        {
            binding.EscButton.onClick = new Button.ButtonClickedEvent();
            binding.EscButton.onClick.AddListener(Close);
        }

        foreach (StoreManager legacy in viewRoot.GetComponentsInChildren<StoreManager>(true)) legacy.enabled = false;
        foreach (StoreSet legacy in viewRoot.GetComponentsInChildren<StoreSet>(true)) legacy.enabled = false;
        foreach (StoreCardOwn legacy in viewRoot.GetComponentsInChildren<StoreCardOwn>(true)) legacy.enabled = false;
        foreach (InventoryStore legacy in viewRoot.GetComponentsInChildren<InventoryStore>(true)) legacy.enabled = false;
        foreach (EquipStore legacy in viewRoot.GetComponentsInChildren<EquipStore>(true)) legacy.enabled = false;
        foreach (sellCard legacy in viewRoot.GetComponentsInChildren<sellCard>(true)) legacy.enabled = false;

        for (int i = 0; i < SlotCount; i++)
        {
            int slot = i;
            if (offerButtons[i] != null)
            {
                offerButtons[i].onClick = new Button.ButtonClickedEvent();
                offerButtons[i].onClick.AddListener(() => SelectOfferForPurchase(slot));
            }
            if (offerImages[i] != null)
                offerImages[i].rectTransform.localScale = new Vector3(OfferImageScale, OfferImageScale, 1f);
            GameObject hoverRoot = binding.SlotRoots[i];
            BattleShopOfferHover hover = BattleComponentResolver.GetOrAdd<BattleShopOfferHover>(hoverRoot, null);
            // The per-slot "Button" object inside Item01 is inactive by default in the
            // prefab (legacy design showed it only after a click elsewhere), so it can
            // never receive a raycasted click on its own. The slot root stays active, so
            // route the click through the same hover relay instead.
            //
            // 클릭으로 정보가 "고정"된 뒤에는(purchaseButtonMode != None) 마우스가 슬롯을
            // 벗어나도 정보가 사라지면 안 된다 — 이전에는 hover exit이 무조건 HideOfferDetails
            // 를 불러서 클릭해 선택한 아이템도 마우스만 떼면 정보가 꺼졌다. 아무것도 선택 안 된
            // 상태에서만 hover 미리보기가 동작하도록 막는다. 다른 아이템 클릭 시 정보 갱신,
            // 구매/ESC 시 정보 해제는 SelectOfferForPurchase/BuySelectedOffer/Update의 ESC
            // 처리에서 각각 담당한다.
            hover.Bind(
                () => { if (purchaseButtonMode == PurchaseButtonMode.None) ShowOfferDetails(slot); },
                () => { if (purchaseButtonMode == PurchaseButtonMode.None) HideOfferDetails(); },
                () => SelectOfferForPurchase(slot));
        }

        if (rerollButton != null)
        {
            rerollButton.onClick = new Button.ButtonClickedEvent();
            rerollButton.onClick.AddListener(Reroll);
        }
        HideOfferDetails();
        RefreshOwnedInventory();
        viewRoot.SetActive(false);
        return true;
    }

    private void ShowOfferDetails(int slot)
    {
        if (currentState == null || slot < 0 || slot >= SlotCount || currentState.Sold[slot])
        {
            HideOfferDetails();
            return;
        }

        CardData card = currentState.Kinds[slot] == OfferKind.Card
            ? BattleCardConnector.FindOriginalCard(currentState.OfferedCards[slot], originalCardDatabase)
            : null;
        EquipData equipment = currentState.Kinds[slot] == OfferKind.Equipment
            ? currentState.OfferedEquipment[slot]
            : null;

        if (card != null)
        {
            BattleCardData battleCard = battleCardDatabase?.FindByLegacyCardIndex(card.index);
            SetTextVisible(tagText, true, $"TAG  {GetTargetLabel(battleCard)}");
            SetTextVisible(propertyText, true, $"PROPERTY  {GetPropertyLabel(battleCard)}");
            SetTextVisible(damageText, true, $"{Mathf.Max(0, card.damage)} DAMAGE");
            SetTextVisible(equipmentInfoText, false, string.Empty);
            SetPreviewImage(card.myCardSprite, card.name);
            return;
        }

        if (equipment != null)
        {
            SetTextVisible(tagText, false, string.Empty);
            SetTextVisible(propertyText, false, string.Empty);
            SetTextVisible(damageText, false, string.Empty);
            SetTextVisible(equipmentInfoText, true,
                $"STR +{equipment.stroffset}\nDEX +{equipment.dexoffset}\nVIT +{equipment.vitoffset}\nWIS +{equipment.wisoffset}");
            SetPreviewImage(equipment.myEquipSprite, equipment.cardname);
            return;
        }

        HideOfferDetails();
    }

    private void SelectOfferForPurchase(int slot)
    {
        if (currentState == null || slot < 0 || slot >= SlotCount || currentState.Sold[slot]) return;
        selectedPurchaseSlot = slot;
        selectedSellCardIndex = -1;
        purchaseButtonMode = PurchaseButtonMode.BuyOffer;
        ShowOfferDetails(slot);
        if (purchaseButton == null) return;
        purchaseButton.gameObject.SetActive(true);
        TMP_Text label = purchaseButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "BUY";
    }

    /// <summary>보유 인벤토리 카드를 눌렀을 때 같은 BuySellButton을 판매 모드로 전환한다.</summary>
    private void SelectInventoryCardForSale(int cardIndex)
    {
        if (currentState == null) return;
        if (!DataConfig.CardsCount.TryGetValue(cardIndex, out int owned) || owned <= 0) return;
        CardData card = BattleCardConnector.FindOriginalCard(cardIndex, originalCardDatabase);
        if (card == null) return;

        selectedPurchaseSlot = -1;
        if (IsProtectedStartingCard(cardIndex))
        {
            selectedSellCardIndex = -1;
            purchaseButtonMode = PurchaseButtonMode.None;
            SetPreviewImage(card.myCardSprite, $"{card.name} (LOCKED)");
            if (purchaseButton != null) purchaseButton.gameObject.SetActive(false);
            Debug.Log($"[Shop] 기본 장착 카드는 판매할 수 없습니다: 카드 {cardIndex}", this);
            return;
        }

        selectedSellCardIndex = cardIndex;
        purchaseButtonMode = PurchaseButtonMode.SellCard;
        HideOfferDetails();
        SetPreviewImage(card.myCardSprite, card.name);
        if (purchaseButton == null) return;
        purchaseButton.gameObject.SetActive(true);
        TMP_Text label = purchaseButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "SELL";
    }

    /// <summary>보유 장비 슬롯을 눌렀을 때 같은 BuySellButton을 장비 판매 모드로 전환한다.</summary>
    private void SelectEquipmentSlotForSale(EquipState state)
    {
        if (currentState == null) return;
        int equipIndex = GetEquippedIndex(state);
        if (equipIndex <= 0) return; // 0번 = 미장착 (EquipStore.OnPointerDown과 동일한 규칙)

        selectedPurchaseSlot = -1;
        selectedSellCardIndex = -1;
        selectedSellEquipState = state;
        hasSelectedSellEquip = true;
        purchaseButtonMode = PurchaseButtonMode.SellEquipment;
        HideOfferDetails();
        bool valid = equipmentDatabase != null && equipmentDatabase.equip != null &&
            equipIndex >= 0 && equipIndex < equipmentDatabase.equip.Count;
        if (valid)
        {
            EquipData equipment = equipmentDatabase.equip[equipIndex];
            SetPreviewImage(equipment.myEquipSprite, equipment.cardname);
        }
        if (purchaseButton == null) return;
        purchaseButton.gameObject.SetActive(true);
        TMP_Text label = purchaseButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "SELL";
    }

    private void OnPurchaseButtonClicked()
    {
        if (purchaseButtonMode == PurchaseButtonMode.SellCard) SellSelectedCard();
        else if (purchaseButtonMode == PurchaseButtonMode.SellEquipment) SellSelectedEquipment();
        else BuySelectedOffer();
    }

    private void BuySelectedOffer()
    {
        int slot = selectedPurchaseSlot;
        if (slot < 0) return;
        Buy(slot);
        ResetPurchaseSelection();
        HideOfferDetails();
    }

    /// <summary>레거시 sellCard.cs와 같은 공식(원가의 절반)으로 보유 카드 1장을 판매한다.</summary>
    private void SellSelectedCard()
    {
        int index = selectedSellCardIndex;
        if (index < 0) { ResetPurchaseSelection(); return; }
        if (!DataConfig.CardsCount.TryGetValue(index, out int owned) || owned <= 0) { ResetPurchaseSelection(); RefreshView(); return; }
        if (IsProtectedStartingCard(index))
        {
            Debug.LogWarning($"[Shop] 기본 장착 카드 판매 요청 차단: 카드 {index}", this);
            ResetPurchaseSelection();
            RefreshOwnedInventory();
            return;
        }
        CardData card = BattleCardConnector.FindOriginalCard(index, originalCardDatabase);
        if (card == null) { ResetPurchaseSelection(); return; }

        int refund = Mathf.Max(0, card.cardCost / 2);
        DataConfig.AddDic(index, -1);
        int remainingOwned = Mathf.Max(0, owned - 1);
        SynchronizePlayerCardData(index, remainingOwned);
        BattleGameManager.Instance?.CardDrawSystem?.SynchronizeOwnedCardCount(index, remainingOwned);
        DataConfig.playerMoney += refund;
        RefreshLinkedCardInventories();
        Debug.Log($"[Shop] 카드 판매: {card.name} / {refund}G / 남은 보유 {remainingOwned}장", this);
        ResetPurchaseSelection();
        RefreshView();
    }

    /// <summary>전투 시작 당시 기본 덱에 포함된 카드 종류인지 확인해 판매를 잠근다.</summary>
    private static bool IsProtectedStartingCard(int cardIndex)
    {
        BattleCardDrawSystem drawSystem = BattleGameManager.Instance?.CardDrawSystem;
        if (drawSystem != null) return drawSystem.IsProtectedStartingCard(cardIndex);

        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        if (playerDeck == null || playerDeck.deckCardforUI == null) return false;
        foreach (int equippedCardIndex in playerDeck.deckCardforUI)
            if (equippedCardIndex == cardIndex) return true;
        return false;
    }

    /// <summary>현재 등록 Player의 PlayerDeck을 우선 반환하고 구형 Scene은 비활성 객체 검색으로 보완한다.</summary>
    private static PlayerDeck ResolveCurrentPlayerDeck()
    {
        PlayerDeck playerDeck = BattleGameManager.Instance != null && BattleGameManager.Instance.CurrentPlayer != null
            ? BattleGameManager.Instance.CurrentPlayer.GetComponentInParent<PlayerDeck>(true)
            : null;
        return playerDeck != null
            ? playerDeck
            : Object.FindFirstObjectByType<PlayerDeck>(FindObjectsInactive.Include);
    }

    /// <summary>판매 후 저장 덱과 실제 PlayerDeck의 동일 카드 수를 최종 보유 수량 이하로 맞춘다.</summary>
    private static void SynchronizePlayerCardData(int cardIndex, int remainingOwned)
    {
        int savedCount = 0;
        foreach (int savedCard in DataConfig.cardData)
            if (savedCard == cardIndex) savedCount++;
        for (int i = DataConfig.cardData.Count - 1; i >= 0 && savedCount > remainingOwned; i--)
        {
            if (DataConfig.cardData[i] != cardIndex) continue;
            DataConfig.cardData.RemoveAt(i);
            savedCount--;
        }

        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        if (playerDeck == null) return;
        if (playerDeck.deckCardforUI != null)
        {
            int deckCount = 0;
            foreach (int equippedCard in playerDeck.deckCardforUI)
                if (equippedCard == cardIndex) deckCount++;
            for (int i = playerDeck.deckCardforUI.Length - 1; i >= 0 && deckCount > remainingOwned; i--)
            {
                if (playerDeck.deckCardforUI[i] != cardIndex) continue;
                playerDeck.deckCardforUI[i] = -1;
                deckCount--;
            }
        }

        if (remainingOwned <= 0) playerDeck.cardPool.Remove(cardIndex);
        else playerDeck.cardPool[cardIndex] = remainingOwned;
    }

    /// <summary>판매 결과를 상점 밖 일반 카드 인벤토리에도 즉시 전달한다.</summary>
    private static void RefreshLinkedCardInventories()
    {
        foreach (InventorySetting inventory in
                 Object.FindObjectsByType<InventorySetting>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (inventory != null) inventory.InitAll();
        }
    }

    /// <summary>레거시 sellCard.EquipCardSellBtn과 같은 DataConfig.SellXWeapon() 경로로 장비 1개를 판매한다.</summary>
    private void SellSelectedEquipment()
    {
        if (!hasSelectedSellEquip) { ResetPurchaseSelection(); return; }
        EquipState state = selectedSellEquipState;
        int equipIndex = GetEquippedIndex(state);
        if (equipIndex <= 0) { ResetPurchaseSelection(); RefreshView(); return; }

        int goldBefore = DataConfig.playerMoney;
        switch (state)
        {
            case EquipState.LeftHand: DataConfig.SellLeftWeapon(); break;
            case EquipState.RightHand: DataConfig.SellRightWeapon(); break;
            case EquipState.Head: DataConfig.SellheadWeapon(); break;
            case EquipState.Body: DataConfig.SellBodyWeapon(); break;
        }
        Debug.Log($"[Shop] 장비 판매: {state} / {DataConfig.playerMoney - goldBefore}G", this);

        ApplyEquipmentVisual();

        ResetPurchaseSelection();
        RefreshView();
    }

    private void ResetPurchaseSelection()
    {
        selectedPurchaseSlot = -1;
        selectedSellCardIndex = -1;
        hasSelectedSellEquip = false;
        purchaseButtonMode = PurchaseButtonMode.None;
        if (purchaseButton != null) purchaseButton.gameObject.SetActive(false);
    }

    private void HideOfferDetails()
    {
        SetTextVisible(tagText, false, string.Empty);
        SetTextVisible(propertyText, false, string.Empty);
        SetTextVisible(damageText, false, string.Empty);
        SetTextVisible(equipmentInfoText, false, string.Empty);
        SetPreviewImage(null, string.Empty);
    }

    private void SetPreviewImage(Sprite sprite, string displayName)
    {
        bool visible = sprite != null;
        if (hoverPreviewImage != null)
        {
            hoverPreviewImage.sprite = sprite;
            hoverPreviewImage.enabled = visible;
            hoverPreviewImage.preserveAspect = true;
        }
        if (selectedCardNameText != null)
            selectedCardNameText.text = visible ? displayName : string.Empty;
        if (selectedImageRoot != null)
            selectedImageRoot.SetActive(visible);
    }

    /// <summary>상점 인벤토리 영역에 현재 보유 카드와 장착 장비를 표시한다.</summary>
    private void RefreshOwnedInventory()
    {
        if (ownedCardSlots != null)
        {
            List<int> ownedCardIndices = new List<int>(DataConfig.CardsCount.Keys);
            ownedCardIndices.Sort();
            for (int i = 0; i < ownedCardSlots.Length; i++)
            {
                InventoryStore slot = ownedCardSlots[i];
                if (slot == null) continue;
                bool hasCard = i < ownedCardIndices.Count;
                slot.gameObject.SetActive(hasCard);
                if (!hasCard) continue;

                int cardIndex = ownedCardIndices[i];
                slot.StoreInvenInit(cardIndex);
                // InventoryStore.OnPointerDown is disabled along with the rest of the
                // legacy sell chain (StoreManager/StoreSet), so clicking an owned card
                // did nothing. Route the click through the same hover relay used by the
                // shop offers so tapping a card shows the SELL button instead.
                BattleShopOfferHover hover = BattleComponentResolver.GetOrAdd<BattleShopOfferHover>(slot.gameObject, null);
                hover.Bind(null, null, () => SelectInventoryCardForSale(cardIndex));
            }
            if (ownedInventoryScroll != null)
            {
                ownedInventoryScroll.scrollSensitivity = 25f;
                Canvas.ForceUpdateCanvases();
                if (ownedInventoryScroll.content != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(ownedInventoryScroll.content);
                ownedInventoryScroll.verticalNormalizedPosition = 1f;
                ownedInventoryScroll.StopMovement();
            }
        }

        if (ownedEquipmentSlots == null) return;
        foreach (EquipStore slot in ownedEquipmentSlots)
        {
            if (slot == null) continue;
            slot.gameObject.SetActive(true);
            RefreshOwnedEquipmentSlot(slot);
        }
    }

    /// <summary>DataPool을 요구하는 레거시 Init을 호출하지 않고 장착 데이터와 표시 이미지만 연결한다.</summary>
    private void RefreshOwnedEquipmentSlot(EquipStore slot)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        System.Reflection.FieldInfo stateField = typeof(EquipStore).GetField("state", flags);
        System.Reflection.FieldInfo imageField = typeof(EquipStore).GetField("thisIMG", flags);
        if (stateField == null || imageField == null) return;

        EquipState state = (EquipState)stateField.GetValue(slot);
        Image display = imageField.GetValue(slot) as Image;
        if (display == null) return;
        int equipmentIndex = GetEquippedIndex(state);
        bool valid = equipmentDatabase != null && equipmentDatabase.equip != null &&
            equipmentIndex >= 0 && equipmentIndex < equipmentDatabase.equip.Count;
        display.sprite = valid ? equipmentDatabase.equip[equipmentIndex].myEquipSprite : null;
        display.enabled = display.sprite != null;

        // EquipStore.OnPointerDown은 부모의 legacy sellCard 컴포넌트를 요구해 Battle 상점에서는
        // 아무 반응이 없었다. 카드와 동일한 클릭 릴레이로 판매 모드 전환을 연결한다.
        BattleShopOfferHover hover = BattleComponentResolver.GetOrAdd<BattleShopOfferHover>(slot.gameObject, null);
        hover.Bind(null, null, () => SelectEquipmentSlotForSale(state));
    }

    private static int GetEquippedIndex(EquipState state)
    {
        switch (state)
        {
            case EquipState.LeftHand: return DataConfig.leftHand;
            case EquipState.RightHand: return DataConfig.rightHand;
            case EquipState.Head: return DataConfig.head;
            case EquipState.Body: return DataConfig.body;
            default: return -1;
        }
    }

    private static void SetTextVisible(TMP_Text target, bool visible, string value)
    {
        if (target == null) return;
        target.text = value;
        target.gameObject.SetActive(visible);
    }

    private static string GetTargetLabel(BattleCardData card)
    {
        if (card == null) return "NONE";
        switch (card.targetType)
        {
            case BattleCardTargetType.Self: return "SELF";
            case BattleCardTargetType.Enemy: return "ENEMY";
            case BattleCardTargetType.Ally: return "ALLY";
            case BattleCardTargetType.Character: return "CHARACTER";
            case BattleCardTargetType.Tile: return "TILE";
            case BattleCardTargetType.AllEnemies: return "ALL ENEMIES";
            default: return "NONE";
        }
    }

    private static string GetPropertyLabel(BattleCardData card)
    {
        if (card == null) return "NONE";
        switch (card.cardType)
        {
            case BattleCardType.PhysicalDamage: return "PHYSICAL";
            case BattleCardType.MagicDamage: return "MAGIC";
            default: return "SUPPORT";
        }
    }

    /// <summary>레거시 Store 프리팹의 외형과 배열만 재사용하고 구매 규칙은 Battle 상점이 담당한다.</summary>
    private bool TryCreateLegacyView()
    {
        BattleLegacyStorePrefabReference reference = BattleLegacyStorePrefabReference.Load();
        if (reference == null || reference.Prefab == null) return false;

        GameObject root = new GameObject(
            "Battle Legacy Store", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject legacy = Instantiate(reference.Prefab, root.transform, false);
        legacy.SetActive(true);
        SetNamedObjectActive(legacy.transform, "Inventory", false);
        SetNamedObjectActive(legacy.transform, "BuySellButton", false);
        RectTransform legacyRect = legacy.GetComponent<RectTransform>();
        legacyRect.anchorMin = Vector2.zero;
        legacyRect.anchorMax = Vector2.one;
        legacyRect.offsetMin = Vector2.zero;
        legacyRect.offsetMax = Vector2.zero;

        // 참고: 현재 Store.prefab에는 StoreManager/StoreSet/InventoryStore 컴포넌트가
        // 붙어 있지 않다(확인됨). 아래 세 줄은 다른 버전의 Store 프리팹이 이 컴포넌트를
        // 들고 있을 경우를 대비한 방어 코드이며, 지금은 대상이 없어 아무 동작도 하지
        // 않는다. 실제 슬롯 바인딩은 BattleLegacyStoreViewAdapter가 이름 기반으로
        // "StoreItems" 하위의 ShopItem01 (1)/(2)/(3)를 직접 찾아 처리한다.
        // Event_Store is copied from the production main scene. Keep its visuals and
        // persistent tween callbacks, but remove the main-scene economy behaviours so
        // only the battle shop rules can change cards or gold.
        foreach (StoreManager component in legacy.GetComponentsInChildren<StoreManager>(true)) Destroy(component);
        foreach (StoreSet component in legacy.GetComponentsInChildren<StoreSet>(true)) Destroy(component);
        foreach (StoreCardOwn component in legacy.GetComponentsInChildren<StoreCardOwn>(true)) Destroy(component);
        foreach (InventoryStore component in legacy.GetComponentsInChildren<InventoryStore>(true)) Destroy(component);
        foreach (EquipStore component in legacy.GetComponentsInChildren<EquipStore>(true)) Destroy(component);
        foreach (sellCard component in legacy.GetComponentsInChildren<sellCard>(true)) Destroy(component);

        foreach (CanvasGroup group in legacy.GetComponentsInChildren<CanvasGroup>(true))
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        if (!BattleLegacyStoreViewAdapter.TryBind(
                legacy, SlotCount, out BattleLegacyStoreViewAdapter.Binding binding))
        {
            Destroy(root);
            canvas = null;
            return false;
        }

        offerButtons = new Button[SlotCount];
        offerImages = new Image[SlotCount];
        offerTexts = new TMP_Text[SlotCount];
        offerPriceTexts = new TMP_Text[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            int slot = i;
            Button button = binding.Buttons[i];
            // Remove only runtime listeners. Unity keeps the prefab's persistent
            // DOTween callbacks, whose non-visual StoreManager targets were removed.
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Buy(slot));
            offerButtons[i] = button;
            offerImages[i] = binding.CardImages[i];
            if (offerImages[i] != null)
                offerImages[i].rectTransform.localScale = new Vector3(OfferImageScale, OfferImageScale, 1f);
            offerTexts[i] = binding.CardNames[i];
            offerPriceTexts[i] = binding.CardPrices[i];
        }

        goldText = binding.GoldText ?? FindNamedComponent<TMP_Text>(legacy.transform, "CurrentGoldText");
        Button reroll = binding.RerollButton ?? FindNamedComponent<Button>(legacy.transform, "RerollButton");
        if (reroll != null)
        {
            rerollButton = reroll;
            // Preserve persistent animation callbacks and replace runtime logic.
            reroll.onClick.RemoveAllListeners();
            reroll.onClick.AddListener(Reroll);
            rerollText = binding.RerollText ?? reroll.GetComponentInChildren<TMP_Text>(true);
        }

        RectTransform rootRect = root.GetComponent<RectTransform>();
        Button close = CreateButton(rootRect, "CLOSE", new Vector2(420f, 235f), new Vector2(130f, 48f));
        close.onClick.AddListener(Close);
        if (goldText == null)
            goldText = CreateText(rootRect, "Gold", new Vector2(-420f, 235f), new Vector2(260f, 48f), 24f);
        if (rerollText == null)
        {
            Button fallbackReroll = CreateButton(rootRect, string.Empty, new Vector2(0f, -440f), new Vector2(220f, 52f));
            fallbackReroll.onClick.AddListener(Reroll);
            rerollText = fallbackReroll.GetComponentInChildren<TMP_Text>(true);
        }
        root.SetActive(false);
        return true;
    }

    private static T FindNamedComponent<T>(Transform root, string objectName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            if (component != null && component.gameObject.name == objectName) return component;
        return null;
    }

    private static Transform FindNamedTransform(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != null && child.gameObject.name == objectName) return child;
        return null;
    }

    private static void SetNamedObjectActive(Transform root, string objectName, bool active)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.gameObject.name != objectName) continue;
            child.gameObject.SetActive(active);
        }
    }

    private void EnsureEquipmentConfirmationView()
    {
        if (equipmentConfirmPanel != null || canvas == null) return;

        RectTransform panel = new GameObject(
            "Equipment Purchase Confirmation",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            .GetComponent<RectTransform>();
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(720f, 420f);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.98f);
        equipmentConfirmPanel = panel.gameObject;

        TMP_Text title = CreateText(panel, "Title", new Vector2(0f, 160f), new Vector2(660f, 55f), 30f);
        title.text = "EQUIPMENT COMPARISON";
        equipmentConfirmText = CreateText(panel, "Comparison", new Vector2(0f, 25f), new Vector2(650f, 230f), 23f);

        equipmentConfirmButton = CreateButton(panel, "BUY & REPLACE", new Vector2(-150f, -155f), new Vector2(250f, 58f));
        equipmentConfirmButton.onClick.AddListener(ConfirmEquipmentPurchase);
        equipmentLeftButton = CreateButton(panel, "EQUIP LEFT", new Vector2(-230f, -155f), new Vector2(200f, 58f));
        equipmentLeftButton.onClick.AddListener(ConfirmEquipmentPurchaseLeft);
        equipmentRightButton = CreateButton(panel, "EQUIP RIGHT", new Vector2(0f, -155f), new Vector2(200f, 58f));
        equipmentRightButton.onClick.AddListener(ConfirmEquipmentPurchaseRight);
        equipmentCancelButton = CreateButton(panel, "CANCEL", new Vector2(150f, -155f), new Vector2(180f, 58f));
        equipmentCancelButton.onClick.AddListener(CancelEquipmentPurchase);
        equipmentConfirmPanel.SetActive(false);
    }

    private static TMP_Text CreateText(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        TMP_Text text = rect.GetComponent<TextMeshProUGUI>(); text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(RectTransform parent, string label, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(label + " Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        rect.GetComponent<Image>().color = new Color(0.22f, 0.27f, 0.38f, 1f);
        Button button = rect.GetComponent<Button>();
        if (!string.IsNullOrEmpty(label)) { TMP_Text text = CreateText(rect, "Label", Vector2.zero, size, 24f); text.text = label; }
        else { TMP_Text text = CreateText(rect, "Label", Vector2.zero, size, 24f); text.text = string.Empty; }
        return button;
    }
}
