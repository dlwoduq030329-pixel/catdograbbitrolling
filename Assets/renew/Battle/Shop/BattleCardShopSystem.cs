using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Store 타일의 카드·장비 진열, 구매·판매, 리롤과 플레이어 보유 목록 표시를 담당한다.</summary>
[DisallowMultipleComponent]
public sealed class BattleCardShopSystem : MonoBehaviour
{
    private const int SlotCount = 6;
    // Item01 슬롯 크기는 158x268이고 카드 이미지 원본은 100x150.8이다. 이 배율(1.5배)이면
    // 이미지가 가로 150/세로 227 정도로 커져서 슬롯을 거의 채우면서도, 항상 보이는 하단
    // 가격 텍스트 영역까지는 침범하지 않는다.
    private const float OfferImageScale = 1.5f;
    private enum OfferKind { None, Card, Equipment }
    /// <summary>
    /// 상점 타일 하나(=StoreState 하나)의 6슬롯 진열 상태. Kinds[i]가 그 슬롯이 카드인지 장비인지
    /// 아무것도 없는지를 결정하고, 실제 내용은 종류에 맞춰 OfferedCards[i](카드 인덱스, 없으면 -1)
    /// 또는 OfferedEquipment[i](장비 인스턴스, 없으면 null) 둘 중 하나에만 값이 들어간다.
    /// Sold[i]는 그 슬롯이 이미 팔렸는지, RerollPrice는 이 상점에서 다음 리롤에 낼 골드다
    /// (리롤할 때마다 오르며, 상점이 닫힐 때까지 현재 방문 상태에 유지된다).
    /// </summary>
    private sealed class StoreState
    {
        internal readonly OfferKind[] Kinds = new OfferKind[SlotCount];
        internal readonly int[] OfferedCards = { -1, -1, -1, -1, -1, -1 };
        internal readonly EquipData[] OfferedEquipment = new EquipData[SlotCount];
        internal readonly bool[] Sold = new bool[SlotCount];
        internal int RerollPrice = 10;
    }

    /// <summary>
    /// TryEnter가 한 번 성공한 상점 타일을 다시 저장해, 같은 타일에 재진입 자체를 막는 데 쓴다
    /// (2026-08-22 추가, 사용자 확인 — Chest의 openedTiles와 같은 패턴).
    /// </summary>
    private readonly HashSet<MapInfo> enteredStores = new HashSet<MapInfo>();
    [Header("카드 데이터")]
    [SerializeField] private BattleCardDatabase battleCardDatabase;
    [SerializeField] private CardDatabase originalCardDatabase;
    [Header("장비 및 밸런스 데이터")]
    [Tooltip("상점에 진열할 장비의 원본 목록입니다.")]
    [SerializeField] private EquipDatabase equipmentDatabase;
    [Tooltip("상품 슬롯 수, 리롤 가격, 장비 추첨 가중치를 보관하는 데이터입니다.")]
    [SerializeField] private BattleShopBalanceData shopBalanceData;
    [Header("상점 화면")]
    [SerializeField] private BattleShopView shopView;
    private StoreState currentState;
    /// <summary>상점 UI 전체의 루트 오브젝트. null이면 아직 EnsureView로 생성/바인딩 전이라는 뜻.</summary>
    private GameObject viewRoot;
    private TMP_Text goldText;
    private TMP_Text rerollText;
    private Button rerollButton;
    private Button[] offerButtons;
    private Image[] offerImages;
    private TMP_Text[] offerTexts;
    private TMP_Text[] offerPriceTexts;
    private bool holdsModalLock;
    // 아래 4개(tagText/propertyText/damageText/equipmentInfoText)는 ShowOfferDetails가 채우는
    // "지금 가리키고 있는 상품" 상세 텍스트들이다.
    private TMP_Text tagText;
    private TMP_Text propertyText;
    private TMP_Text damageText;
    private TMP_Text equipmentInfoText;
    /// <summary>ShowOfferDetails/SetPreviewImage가 채우는 상품 미리보기 큰 이미지.</summary>
    private Image hoverPreviewImage;
    private GameObject selectedImageRoot;
    private TMP_Text selectedCardNameText;
    /// <summary>보유 카드 인벤토리 슬롯들(RefreshOwnedInventory가 매번 다시 채움).</summary>
    private BattleShopOwnedCardSlotView[] ownedCardSlotPool;
    /// <summary>보유 장비(좌/우손·몸통·머리) 슬롯들(RefreshOwnedInventory가 매번 다시 채움).</summary>
    private BattleShopOwnedEquipmentSlotView[] ownedEquipmentSlots;
    private ScrollRect ownedInventoryScroll;
    /// <summary>하나뿐인 공용 BUY/SELL 버튼. purchaseButtonMode에 따라 라벨과 동작이 바뀐다.</summary>
    private Button purchaseButton;
    private int selectedPurchaseSlot = -1;
    /// <summary>
    /// 공용 purchaseButton이 지금 어떤 동작을 할지 나타낸다. None은 "아무것도 선택 안 됨"
    /// (버튼이 숨겨진 기본 상태)이고, BuyOffer/SellCard/SellEquipment는 각각 진열 상품 구매 확정,
    /// 보유 카드 판매, 보유 장비 판매를 뜻한다 — 즉 하나의 버튼이 상황에 따라 4가지로 갈리는 상태값.
    /// </summary>
    private enum PurchaseButtonMode { None, BuyOffer, SellCard, SellEquipment }
    private PurchaseButtonMode purchaseButtonMode = PurchaseButtonMode.None;
    private int selectedSellCardIndex = -1;
    private PlayerEquipmentSlotType selectedSellEquipmentSlot;
    private bool hasSelectedSellEquip;

    /// <summary>
    /// 상점 UI가 열려 있는 동안 매 프레임 ESC 입력만 감지한다. 장비 구매 확인창이나
    /// 카드/장비 선택(구매·판매 미리보기)이 떠 있으면 상점 전체를 닫는 대신 그 선택만 취소하고,
    /// 아무것도 선택돼 있지 않을 때만 실제로 상점을 닫는다(단계적 ESC 처리).
    /// </summary>
    private void Update()
    {
        if (viewRoot == null || !viewRoot.activeSelf || !Input.GetKeyDown(KeyCode.Escape)) return;

        // 장비 구매 확인창 또는 카드/장비 선택(구매·판매 상세 미리보기)이 열려 있으면
        // ESC로 상점 전체를 나가는 대신 그 선택만 취소한다. 아무것도 선택된 게 없을 때만
        // 상점을 닫는다.
        if (purchaseButtonMode != PurchaseButtonMode.None)
        {
            ResetPurchaseSelection();
            HideOfferDetails();
            return;
        }
        Close();
    }

    /// <summary>
    /// 상점 타일에 진입을 시도한다. 이미 한 번 들어갔던 타일(enteredStores)이면 즉시 실패해
    /// 같은 상점 타일에 재진입 자체가 안 되게 막는다(2026-08-22 추가, 사용자 확인 — Chest의
    /// "한 번 연 상자는 다시 못 엶"과 같은 설계). 처음 들어가는 타일이면 <see cref="GenerateOffers"/>로
    /// 해당 방문에 사용할 판매 목록을 생성한다.
    /// </summary>
    public bool TryEnter(MapInfo tile)
    {
        if (tile == null || tile.Type != TileType.Store || enteredStores.Contains(tile)) return false;
        if (!HasRequiredShopData()) return false;
        EnsureView();
        if (viewRoot == null)
        {
            Debug.LogError("[Shop] 상점 UI를 생성하지 못했습니다.", this);
            return false;
        }

        currentState = new StoreState
        {
            RerollPrice = shopBalanceData != null ? shopBalanceData.initialRerollPrice : 10
        };
        GenerateOffers(currentState);
        enteredStores.Add(tile);
        viewRoot.SetActive(true);
        AcquireModalLock();
        BattleGameManager.Instance?.SetShopOpen(true);

        RefreshView();
        Debug.Log($"[Shop] {tile.name} 상점 진입", tile);
        return true;
    }

    /// <summary>
    /// 카드·장비·밸런스 데이터가 모두 인스펙터에 연결됐는지 진입 전에 검사한다.
    /// 참조가 빠진 상태로 빈 상점을 열거나 상품 생성 중 NullReferenceException이 발생하는 것을 막는다.
    /// </summary>
    private bool HasRequiredShopData()
    {
        bool hasCardData = battleCardDatabase != null && originalCardDatabase != null;
        bool hasEquipmentData = equipmentDatabase != null && equipmentDatabase.equip != null;
        bool hasBalanceData = shopBalanceData != null;
        if (hasCardData && hasEquipmentData && hasBalanceData) return true;

        Debug.LogError(
            $"[Shop] 필수 데이터 참조 누락 — BattleCardDatabase:{hasCardData}, " +
            $"CardDatabase:{originalCardDatabase != null}, EquipDatabase:{hasEquipmentData}, " +
            $"BattleShopBalanceData:{hasBalanceData}",
            this);
        return false;
    }

    /// <summary>
    /// 이 상점 타일의 6개 슬롯에 무엇을 진열할지 한 번에 결정한다. 카드/장비 슬롯 개수(shopBalanceData)만큼
    /// 종류를 배치한 뒤 무작위로 섞고, 슬롯마다 해당 종류의 후보를 하나씩 뽑아 소모한다(같은 상품이
    /// 한 상점에 중복으로 뜨지 않도록 후보 목록에서 제거). 후보가 부족해 빈 슬롯이 남으면
    /// cardFallbacks/equipmentFallbacks(소모되지 않은 원본 목록)에서 다시 뽑아 채운다.
    /// </summary>
    private void GenerateOffers(StoreState state)
    {
        // cardCandidates: 이번 진열에 쓸 수 있는 카드 인덱스 목록(BuildEligibleCards, 보유 2장 미만 등
        // 조건을 만족하는 카드). equipmentCandidates: 장비 데이터베이스의 1번(0번은 "미장착"이라 제외)부터
        // 끝까지 전부 — 카드와 달리 별도 자격 조건 없이 전체 장비가 후보다. 두 목록 다 아래 for문에서
        // 슬롯 하나를 채울 때마다 RemoveAt/Remove로 실제로 줄어든다(그래야 같은 상품이 한 상점 안에서
        // 중복으로 안 뜬다).
        List<int> cardCandidates = BuildEligibleCards(); // 카드 후보는 보유 2장 미만 등 조건을 만족하는 카드만 포함
        List<int> equipmentCandidates = new List<int>(); // 장비 후보는 장비 데이터베이스 전체(0번은 "미장착"이라 제외)로 시작
        CollectValidEquipmentIndices(equipmentCandidates); // 0번 제외한 data만
        // cardFallbacks/equipmentFallbacks: 위 두 목록을 소모되기 "전" 시점에 통째로 복사해둔 원본이다.
        // 아래 fallback 분기(카드/장비 후보가 바닥나 슬롯이 빈 채로 남을 때)에서만 쓰이며, 여기서 뽑은
        // 상품은 이미 다른 슬롯에 나온 것과 중복될 수 있다(원본 개수 부족을 메우기 위한 최후 수단이라
        // 중복 방지를 포기하는 구조). "판매 콜백"이나 "이미 2장 있는 카드" 처리와는 무관하다.
        List<int> cardFallbacks = new List<int>(cardCandidates);
        List<int> equipmentFallbacks = new List<int>(equipmentCandidates);

        List<OfferKind> layout = new List<OfferKind>(SlotCount);
        int cardSlots = shopBalanceData != null ? shopBalanceData.cardSlots : 3;
        int equipmentSlots = shopBalanceData != null ? shopBalanceData.equipmentSlots : 3;
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

            // 데이터베이스가 작으면 지금 조건에 맞는 고유 상품이 6개보다 적을 수 있다.
            // 화면에 보이는 상점 슬롯을 빈칸으로 남겨두지 않기 위해, 카드를 먼저 재사용하고
            // 카드 보상이 아예 없을 때만 장비로 채운다.
            //
            // 주의: 위 카드/장비 우선순위 스왑(바로 위 if/else if)이 있어서, 이 fallback은
            // "카드 후보와 장비 후보가 동시에 바닥났을 때"만 실행된다 — 장비 후보(equipmentCandidates)는
            // 데이터베이스 전체 장비 수만큼 있어서 한 상점(6슬롯)에서 바닥나는 일이 사실상 없으므로,
            // 실제로는 카드 후보(보유 중인 카드가 이미 많아 뽑을 카드가 없는 경우)가 원인이 되는
            // 경우가 거의 전부다. 그런데도 카드를 먼저 재사용하게 되어 있어, 이 fallback이 도는 순간엔
            // 장비가 뽑힐 확률이 더 낮다 — "장비가 아예 안 나온다"는 체감의 원인일 수 있다(발생 자체는
            // 드묾: 카드/장비 둘 다 동시에 완전히 바닥나야 함).
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

    /// <summary>
    /// 후보 목록에서 장비 하나를 뽑는다. 1차로 <c>BattleEquipmentRewardRoller.RollEquipmentKind</c>(부위별 가중치
    /// 추첨, 현재 40/30/20/10 손/몸통/머리/양손)로 선호 부위를 정하고, 그 부위 후보가 있으면
    /// 풀을 좁힌다(없으면 전체 후보 그대로 사용). 2차로 같은 풀 안에서 "이미 장착 중=가중치1,
    /// 미장착=가중치4"로 다시 추첨해 안 써본 장비가 더 잘 나오게 한다.
    /// </summary>
    private int PickEquipmentCandidate(List<int> candidates)
    {
        if (candidates == null || candidates.Count == 0) return -1;
        // 1차 추첨: 부위(WeaponKind)를 먼저 정한다.
        int currentStage = BattleGameManager.Instance != null ? BattleGameManager.Instance.CurrentStage : 1;
        WeaponKind preferredKind = shopBalanceData != null
            ? BattleEquipmentRewardRoller.RollEquipmentKind(shopBalanceData, currentStage)
            : equipmentDatabase.equip[candidates[0]].weaponKind;
        // 후보 중 그 부위만 걸러낸 풀. 그 부위 재고가 하나도 없으면(kindFilteredPool.Count == 0)
        // 부위 제한을 포기하고 원래 후보 전체(candidates)를 그대로 쓴다.
        List<int> kindFilteredPool = candidates.FindAll(index => equipmentDatabase.equip[index].weaponKind == preferredKind);
        if (kindFilteredPool.Count == 0) kindFilteredPool = candidates;

        // 2차 추첨: 같은 풀 안에서 "이미 장착 중=가중치1, 미장착=가중치4"로 다시 뽑는다.
        int totalEquipWeight = 0;
        for (int i = 0; i < kindFilteredPool.Count; i++) totalEquipWeight += IsEquipped(kindFilteredPool[i]) ? 1 : 4;
        int weightedRoll = Random.Range(0, Mathf.Max(1, totalEquipWeight));
        for (int i = 0; i < kindFilteredPool.Count; i++)
        {
            weightedRoll -= IsEquipped(kindFilteredPool[i]) ? 1 : 4;
            if (weightedRoll < 0) return kindFilteredPool[i];
        }
        return kindFilteredPool[0];
    }

    /// <summary>이 장비 인덱스가 지금 왼손/오른손/몸통/머리 중 하나로 장착되어 있는지 확인한다.</summary>
    private bool IsEquipped(int equipmentIndex)
    {
        PlayerWeapon playerWeapon = ResolveCurrentPlayerWeapon();
        return playerWeapon != null &&
               (playerWeapon.LeftArm.CurrentEquipment?.weaponIndex == equipmentIndex ||
                playerWeapon.RightArm.CurrentEquipment?.weaponIndex == equipmentIndex ||
                playerWeapon.Body.CurrentEquipment?.weaponIndex == equipmentIndex ||
                playerWeapon.Head.CurrentEquipment?.weaponIndex == equipmentIndex);
    }

    /// <summary>
    /// 지정한 장비 원본을 복제(Clone)해 등급을 <c>BattleEquipmentRewardRoller.RollRarity</c>로 굴리고, 부위별
    /// 기본가(양손90/몸통75/머리70/한손60)에 등급 배율(rare1.6/epic2.5/legendary4)을 곱해
    /// 가격을 매긴다. 등급이 높을수록 보너스 스탯 굴림 횟수(bonusRolls)도 늘어난다
    /// (rare4/epic6/legendary10회, 매 회 STR/DEX/INT/WIS/CAR/VIT 중 하나를 무작위로 +1).
    /// </summary>
    private EquipData CreateRandomEquipment(int equipmentIndex)
    {
        List<EquipData> equipment = equipmentDatabase.equip;
        EquipData offer = equipment[equipmentIndex].Clone();
        weaponSt rarity = shopBalanceData != null
            ? BattleEquipmentRewardRoller.RollRarity(shopBalanceData,
                BattleGameManager.Instance != null ? BattleGameManager.Instance.CurrentStage : 1)
            : (weaponSt)Random.Range(0, Mathf.Clamp(
                BattleGameManager.Instance != null ? BattleGameManager.Instance.CurrentStage : 1, 1, 4));
        offer.weapon = rarity;

        int bonusRolls;
        int basePrice = GetEquipmentBasePrice(offer.weaponKind);
        switch (offer.weapon)
        {
            case weaponSt.Rare:
                offer.cost = Mathf.RoundToInt(basePrice * GetRarePriceMultiplier());
                bonusRolls = shopBalanceData != null ? shopBalanceData.rareBonusStatRolls : 4;
                break;
            case weaponSt.Epic:
                offer.cost = Mathf.RoundToInt(basePrice * GetEpicPriceMultiplier());
                bonusRolls = shopBalanceData != null ? shopBalanceData.epicBonusStatRolls : 6;
                break;
            case weaponSt.Legendary:
                offer.cost = Mathf.RoundToInt(basePrice * GetLegendaryPriceMultiplier());
                bonusRolls = shopBalanceData != null ? shopBalanceData.legendaryBonusStatRolls : 10;
                break;
            default: offer.cost = basePrice; bonusRolls = 0; break;
        }

        for (int i = 0; i < bonusRolls; i++)
        {
            switch (Random.Range(0, 6))
            {
                case 0: offer.stroffset++; break;
                case 1: offer.dexoffset++; break;
                case 2: offer.intoffset++; break;
                case 3: offer.wisoffset++; break;
                case 4: offer.caroffset++; break;
                case 5: offer.vitoffset++; break;
            }
        }

        return offer;
    }

    /// <summary>
    /// 상점에 카드로 진열될 수 있는 후보를 모은다 — battleCardDatabase의 각 카드가 originalCardDatabase에도
    /// 실제로 존재하고(BattleCardConnector로 연결 확인), 이미 2장을 보유하지 않은 경우만 후보로 포함한다
    /// (카드 보유 한도 2장은 Buy에서도 같은 기준으로 다시 확인함).
    /// </summary>
    private List<int> BuildEligibleCards()
    {
        List<int> result = new List<int>();
        if (battleCardDatabase == null || originalCardDatabase == null) return result;
        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        foreach (BattleCardData battleCard in battleCardDatabase.Cards)
        {
            if (battleCard == null || battleCard.legacyCardIndex < 0) continue;
            int owned = playerDeck != null ? playerDeck.GetOwnedCardCount(battleCard.legacyCardIndex) : 0;
            if (owned >= 2) continue;
            if (BattleCardConnector.FindOriginalCard(battleCard.legacyCardIndex, originalCardDatabase) != null)
                result.Add(battleCard.legacyCardIndex);
        }
        return result;
    }

    /// <summary>
    /// 슬롯 클릭 시 구매를 처리하는 진입점이다. 장비 슬롯이면 <see cref="BuyEquipment"/>로 위임하고,
    /// 카드 슬롯이면 가격(카드 원가의 2배)과 보유 한도(2장)를 확인한 뒤 골드를 차감하고 카드를 지급한다.
    /// </summary>
    private void Buy(int slot)
    {
        // 이미 팔린 슬롯이거나 범위 밖이면 아무 것도 안 한다(연타 방지 겸 방어 코드).
        if (currentState == null || slot < 0 || slot >= SlotCount || currentState.Sold[slot]) return;

        if (currentState.Kinds[slot] == OfferKind.Equipment) // 장비를 구매하려는 경우
        {
            BuyEquipment(slot);
            return;
        }
        if (currentState.Kinds[slot] != OfferKind.Card || currentState.OfferedCards[slot] < 0) return; // 카드 슬롯이 아니거나 카드 인덱스가 유효하지 않으면 아무 것도 안 한다.

        int cardIndex = currentState.OfferedCards[slot];
        CardData originalCard = BattleCardConnector.FindOriginalCard(cardIndex, originalCardDatabase);
        if (originalCard == null) return;
        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        if (playerDeck == null)
        {
            Debug.LogError("[Shop] 카드 구매 실패: PlayerDeck 참조가 없습니다.", this);
            return;
        }
        int ownedCount = playerDeck.GetOwnedCardCount(cardIndex);
        int price = GetCardPurchasePrice(originalCard);
        // 카드 1종당 최대 2장 보유 규칙 — BuildEligibleCards가 애초에 후보에서 걸러내지만, fallback
        // 재사용(GenerateOffers)이나 다른 슬롯에서 같은 카드를 이미 산 경우까지 대비해 여기서도 다시 확인한다.
        if (ownedCount >= 2) { Debug.LogWarning($"[Shop] {originalCard.name} 보유 한도 2장", this); RefreshView(); return; }
        PlayerWallet wallet = ResolveCurrentPlayerWallet();
        if (wallet == null) { Debug.LogError("[Shop] 카드 구매 실패: PlayerWallet 참조가 없습니다.", this); return; }
        if (!wallet.TrySpendGold(price)) { Debug.LogWarning($"[Shop] 골드 부족: {price}G 필요", this); return; }
        playerDeck.AddOwnedCard(cardIndex, 1);
        currentState.Sold[slot] = true;
        Debug.Log($"[Shop] 카드 구매: {originalCard.name} / {price}G", this);
        RefreshView();
    }

    /// <summary>
    /// 장비 슬롯을 구매하고 PlayerWeapon의 장비 종류별 자동 장착 규칙으로 즉시 장착한다.
    /// </summary>
    private void BuyEquipment(int slot)
    {
        PurchaseAndEquipEquipment(slot);
    }

    /// <summary>
    /// 장비·지갑·장착 가능 여부를 먼저 모두 검증한 뒤 골드를 차감하고 장착한다.
    /// 기존 장비는 PlayerWeapon이 반환하며, 상점 판매 비율만큼 자동 정산한다.
    /// </summary>
    private void PurchaseAndEquipEquipment(int slot)
    {
        if (currentState == null || slot < 0 || slot >= SlotCount) return;
        EquipData equipment = currentState.OfferedEquipment[slot];
        if (equipment == null || currentState.Sold[slot]) return;
        if (!IsSupportedEquipmentKind(equipment.weaponKind))
        {
            Debug.LogError($"[Shop] 지원하지 않는 장비 종류라 구매할 수 없습니다: {equipment.weaponKind}", this);
            return;
        }
        int price = Mathf.Max(0, equipment.cost);
        PlayerWallet wallet = ResolveCurrentPlayerWallet();
        if (wallet == null)
        {
            Debug.LogError("[Shop] 장비 구매 실패: PlayerWallet 참조가 없습니다.", this);
            return;
        }
        if (!wallet.CanAfford(price))
        {
            Debug.LogWarning($"[Shop] Not enough gold. Equipment costs {price}G.", this);
            return;
        }

        PlayerWeapon playerWeapon = ResolveCurrentPlayerWeapon();
        if (playerWeapon == null)
        {
            Debug.LogError("[Shop] 장비 구매 실패: 등록된 PlayerWeapon이 없습니다.", this);
            return;
        }

        if (!wallet.TrySpendGold(price)) return;
        EquipData removedPrimaryEquipment;
        EquipData removedSecondaryEquipment = null;
        playerWeapon.EquipEquipmentAutomatically(
            equipment,
            out removedPrimaryEquipment,
            out removedSecondaryEquipment);

        // 구매로 강제 교체된 장비는 Player가 판매를 선택한 것이 아니므로 원가 전액을 돌려준다.
        // 직접 SELL을 누른 장비만 shopBalanceData.equipmentSaleRefundRate를 적용한다.
        RefundReplacedEquipmentAtFullValue(wallet, removedPrimaryEquipment);
        RefundReplacedEquipmentAtFullValue(wallet, removedSecondaryEquipment);
        currentState.Sold[slot] = true;
        Debug.Log($"[Shop] Equipment purchased: {equipment.cardname} ({equipment.weapon}) / {price}G", this);
        RefreshView();
    }

    // (2026-08-22 정리, 사용자 확인: GetComparableEquipment/GetReplacementRefund/FormatEquipment/
    // GetSlotRefund 4개 삭제됨 - 호출부가 저장소 전체에서 0개였다. 방금 지운 EnsureEquipmentConfirmationView
    // (제목이 "EQUIPMENT COMPARISON"였음)가 완성됐다면 "현재 장착 장비 vs 구매하려는 장비" 비교 텍스트를
    // 만드는 데 썼을 헬퍼들로 추정되지만, 그 UI 자체가 한 번도 연결된 적 없어 이 헬퍼들도 같이 고아가 됐다.)

    /// <summary>
    /// 골드를 내고 6슬롯 전체를 GenerateOffers로 완전히 새로 뽑는다(일부 슬롯만 바꾸는 게 아니라
    /// 진열 전체가 리셋됨 — 안 팔린 카드/장비도 전부 사라지고 새 목록으로 대체된다). 다음 리롤 가격은
    /// 2배로 오르되 shopBalanceData.maximumRerollPrice(기본 160G)에서 상한이 걸린다. Close()와는 무관한
    /// 별개 동작이다 — Close()는 Reroll()을 호출하지 않는다(오해하기 쉬운 부분이라 명시해둠).
    /// </summary>
    private void Reroll()
    {
        if (currentState == null) return;
        PlayerWallet wallet = ResolveCurrentPlayerWallet();
        if (wallet == null)
        { Debug.LogError("[Shop] 리롤 실패: PlayerWallet 참조가 없습니다.", this); return; }
        if (!wallet.TrySpendGold(currentState.RerollPrice))
        { Debug.LogWarning($"[Shop] 리롤 골드 부족: {currentState.RerollPrice}G 필요", this); return; }
        Debug.Log($"[Shop] 상품 리롤 / {currentState.RerollPrice}G", this);
        int maximumPrice = shopBalanceData != null ? shopBalanceData.maximumRerollPrice : 160;
        currentState.RerollPrice = Mathf.Min(maximumPrice, currentState.RerollPrice * 2);
        ResetPurchaseSelection();
        GenerateOffers(currentState);
        RefreshView();
    }

    /// <summary>
    /// 상점 UI를 정상적으로 닫는 내부 경로다(ESC나 닫기 버튼이 이걸 호출 — TryBindConfiguredShopView에서
    /// binding.EscButton.onClick과 CreateButton으로 만든 CLOSE 버튼 둘 다 이 메서드에 연결됨).
    /// 대기 중이던 장비구매/선택 상태를 전부 취소하고, 모달 입력잠금을 풀고, 이 타일의 currentState
    /// 참조를 비운다(다음에 다른 상점 타일에 들어갈 때 실수로 이전 상태를 쓰지 않도록).
    /// </summary>
    private void Close()
    {
        ResetPurchaseSelection();
        HideOfferDetails();
        if (viewRoot != null) viewRoot.SetActive(false);
        BattleGameManager.Instance?.SetShopOpen(false);
        ReleaseModalLock();
        currentState = null;
    }

    /// <summary>
    /// Close()와 별개로 존재하는 "외부에서 강제로 닫기" 공개 API다. BattleGameManager가 전투 종료/
    /// 플레이어 사망 등으로 열려 있는 모든 오버레이를 한 번에 정리할 때 ChestRewardSystem.ForceClose()와
    /// 나란히 호출한다(BattleGameManager.cs:477-478). 지금은 Close()와 동작이 완전히 같지만, 나중에
    /// "정상 닫기"와 "강제 닫기"를 다르게 처리해야 할 경우(예: 강제 닫기는 확인 없이 즉시)를 위해
    /// 호출부를 분리해둔 것으로 보인다.
    /// </summary>
    public void ForceClose()
    {
        Close();
    }

    /// <summary>
    /// 상점이 열려 있는 동안 전투 입력을 잠근다(모달 UI 뒤에서 실수로 유닛을 조작하지 못하게).
    /// holdsModalLock으로 중복 잠금을 막는다 — 이미 잠근 상태에서 또 호출해도 안전.
    /// </summary>
    private void AcquireModalLock()
    {
        if (holdsModalLock) return;
        BattleGameManager.Instance?.LockBattleInputForOverlay();
        holdsModalLock = true;
    }

    /// <summary>AcquireModalLock으로 잠근 입력을 되돌린다. 잠근 적이 없으면 아무 것도 안 한다.</summary>
    private void ReleaseModalLock()
    {
        if (!holdsModalLock) return;
        holdsModalLock = false;
        BattleGameManager.Instance?.UnlockBattleInputAfterOverlay();
    }

    /// <summary>오브젝트가 비활성화될 때(씬 전환 등) 모달 잠금을 확실히 풀어둔다.</summary>
    private void OnDisable()
    {
        ResetPurchaseSelection();
        HideOfferDetails();
        BattleGameManager.Instance?.SetShopOpen(false);
        ReleaseModalLock();
        currentState = null;
    }

    /// <summary>
    /// 상점 UI 전체를 지금 상태(currentState)에 맞게 다시 그린다 — 골드/리롤가격/6슬롯 상품 이미지·
    /// 가격·품절 표시·구매 가능 여부까지 전부. 구매/판매/리롤/최초 진입 등 상태가 바뀌는 모든 지점에서
    /// 마지막에 이 메서드를 부른다(진열 데이터를 바꾸는 로직과 화면을 그리는 로직을 분리하는 패턴).
    /// </summary>
    private void RefreshView()
    {
        if (viewRoot == null || currentState == null) return;
        FillExistingEmptySlots(currentState);
        PlayerWallet wallet = ResolveCurrentPlayerWallet();
        int currentGold = wallet != null ? wallet.Gold : 0;
        if (goldText != null) goldText.text = $"Gold : {currentGold}G";
        // 리롤 아이콘 자체가 기능을 설명하므로 중복 영문은 빼고 실제 지불 금액만 표시한다.
        if (rerollText != null) rerollText.text = $"{currentState.RerollPrice}G";
        if (rerollButton != null)
            rerollButton.interactable = wallet != null && wallet.CanAfford(currentState.RerollPrice);
        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        for (int i = 0; i < SlotCount; i++)
        {
            CardData card = currentState.Kinds[i] == OfferKind.Card && currentState.OfferedCards[i] >= 0
                ? BattleCardConnector.FindOriginalCard(currentState.OfferedCards[i], originalCardDatabase) : null;
            EquipData equipment = currentState.Kinds[i] == OfferKind.Equipment
                ? currentState.OfferedEquipment[i] : null;
            bool hasOffer = card != null || equipment != null;
            int price = card != null ? GetCardPurchasePrice(card) :
                equipment != null ? Mathf.Max(0, equipment.cost) : 0;
            bool canAfford = wallet != null && wallet.CanAfford(price);
            bool atCardLimit = card != null && playerDeck != null &&
                playerDeck.GetOwnedCardCount(card.index) >= PlayerDeck.MaximumOwnedCopiesPerCard;
            if (offerButtons != null && i < offerButtons.Length && offerButtons[i] != null)
                offerButtons[i].interactable = hasOffer && !currentState.Sold[i] && canAfford && !atCardLimit;
            if (offerImages != null && i < offerImages.Length && offerImages[i] != null)
            {
                Sprite sprite = card != null ? CardArtResolver.ResolveDisplaySprite(card.myCardSprite) :
                    equipment != null ? equipment.myEquipSprite : null;
                offerImages[i].sprite = sprite;
                offerImages[i].enabled = sprite != null;

                CardCostLabelView offerCostLabel = CardCostLabelView.GetOrCreateCostLabel(offerImages[i].transform);
                if (offerCostLabel != null)
                {
                    if (card != null)
                    {
                        offerCostLabel.ShowCostLabel();
                        offerCostLabel.DisplayCardCost(card.cost, card.rare);
                    }
                    else
                    {
                        offerCostLabel.HideCostLabel();
                    }
                }
            }
            if (offerTexts != null && i < offerTexts.Length && offerTexts[i] != null)
            {
                // 상품 카드 안의 기존 설명 영역을 재사용한다. 가격만 보고 상세 패널을 매번 열어야 했던
                // 문제를 줄이기 위해 카드 효과 또는 장비 보정치를 슬롯 자체에서도 바로 확인하게 한다.
                string inlineSummary = card != null
                    ? BuildInlineCardSummary(card)
                    : equipment != null
                        ? BuildInlineEquipmentSummary(equipment)
                        : string.Empty;
                offerTexts[i].text = inlineSummary;
                offerTexts[i].gameObject.SetActive(hasOffer);
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

    /// <summary>
    /// RefreshView가 매번(구매/판매/리롤/진입 후 전부) 호출하는 안전망이다. 정상적인 흐름이라면
    /// GenerateOffers가 6슬롯을 전부 채우므로 Kinds[i] == None인 슬롯은 사실상 나오지 않는데
    /// (GenerateOffers의 fallback 주석 참고 — 카드+장비 후보가 동시에 완전히 바닥나야 발생),
    /// 혹시라도 None 슬롯이 남아 있으면 여기서 "지금 시점 기준으로" 다시 계산한 카드/장비 후보로
    /// 채운다. GenerateOffers의 fallback과 다른 점: 저기는 상점 생성 시점의 스냅샷(cardFallbacks 등)을
    /// 쓰지만, 여기는 호출될 때마다 BuildEligibleCards를 새로 불러 최신 보유 현황을 반영한다.
    /// </summary>
    private void FillExistingEmptySlots(StoreState state)
    {
        List<int> eligibleCards = BuildEligibleCards();
        List<int> eligibleEquipment = new List<int>();
        CollectValidEquipmentIndices(eligibleEquipment);

        for (int i = 0; i < SlotCount; i++)
        {
            if (state.Kinds[i] != OfferKind.None) continue;
            if (eligibleCards.Count > 0)
            {
                int selectedCandidateListIndex = Random.Range(0, eligibleCards.Count);
                state.Kinds[i] = OfferKind.Card;
                state.OfferedCards[i] = eligibleCards[selectedCandidateListIndex];
                // 여러 빈 슬롯을 복구할 때 같은 카드를 반복 선택하지 않도록 사용한 후보를 제거한다.
                eligibleCards.RemoveAt(selectedCandidateListIndex);
            }
            else if (eligibleEquipment.Count > 0)
            {
                int selectedEquipmentIndex = PickEquipmentCandidate(eligibleEquipment);
                state.Kinds[i] = OfferKind.Equipment;
                state.OfferedEquipment[i] = CreateRandomEquipment(selectedEquipmentIndex);
                // 카드와 동일하게 한 번 채운 장비는 이번 빈 슬롯 복구 후보에서 제외한다.
                eligibleEquipment.Remove(selectedEquipmentIndex);
            }
        }
    }

    /// <summary>
    /// 상점 뷰가 아직 없으면(viewRoot == null) Inspector의 BattleShopView 참조를 딱 한 번 연결한다. TryEnter가
    /// 상점 타일에 들어갈 때마다 호출하지만, 두 번째 방문부터는 viewRoot가 이미 있어 즉시 반환한다.
    /// </summary>
    private void EnsureView()
    {
        if (viewRoot != null) return;
        if (TryBindConfiguredShopView()) return;
        Debug.LogError("[Shop] BattleShopView 또는 필수 UI 참조가 연결되지 않았습니다.", this);
    }

    /// <summary>Inspector에 지정된 BattleShopView의 직접 참조를 상점 시스템에 연결한다.</summary>
    private bool TryBindConfiguredShopView()
    {
        if (shopView == null || !shopView.HasRequiredReferences(SlotCount)) return false;

        // BattleShopView가 실제 Event_Store 루트에 직접 붙어 있으므로 같은 오브젝트를 켜고 끈다.
        viewRoot = shopView.gameObject;
        BattleShopOfferSlotView[] configuredSlots = shopView.OfferSlots;
        offerButtons = new Button[SlotCount];
        offerImages = new Image[SlotCount];
        offerTexts = new TMP_Text[SlotCount];
        offerPriceTexts = new TMP_Text[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            offerButtons[i] = configuredSlots[i].SelectButton;
            offerImages[i] = configuredSlots[i].ItemImage;
            offerTexts[i] = configuredSlots[i].ItemNameText;
            offerPriceTexts[i] = configuredSlots[i].PriceText;
        }

        goldText = shopView.GoldText;
        rerollButton = shopView.RerollButton;
        rerollText = shopView.RerollPriceText;
        tagText = shopView.TargetText;
        propertyText = shopView.PropertyText;
        damageText = shopView.DamageText;
        equipmentInfoText = shopView.EquipmentInfoText;
        hoverPreviewImage = shopView.PreviewImage;
        selectedImageRoot = shopView.SelectedItemPanel;
        selectedCardNameText = shopView.SelectedItemNameText;
        ownedInventoryScroll = shopView.OwnedCardScroll;
        ownedCardSlotPool = shopView.OwnedCardSlotPool ?? System.Array.Empty<BattleShopOwnedCardSlotView>();
        ownedEquipmentSlots = shopView.OwnedEquipmentSlots ?? System.Array.Empty<BattleShopOwnedEquipmentSlotView>();
        purchaseButton = shopView.PurchaseButton;
        if (purchaseButton != null)
        {
            purchaseButton.onClick = new Button.ButtonClickedEvent();
            purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
            purchaseButton.gameObject.SetActive(false);
        }

        // EscButton은 프리팹 제작 당시부터 박혀 있던 영구(persistent) onClick을 그대로 들고 있다
        // (Event_Store를 직접 SetActive(false)만 하는 이벤트). 이 이벤트는 Close()를 거치지 않으므로
        // SetShopOpen(false)/UnlockBattleInputAfterOverlay도 호출되지 않는다. onClick 이벤트 객체를
        // 통째로 새로 만들어 교체하면 그 레거시 호출이 사라지고, 우리 쪽 정리 로직(Close)만 남는다.
        if (shopView.CloseButton != null)
        {
            shopView.CloseButton.onClick = new Button.ButtonClickedEvent();
            shopView.CloseButton.onClick.AddListener(Close);
        }

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
            // 상품 이미지뿐 아니라 이름·설명·빈 여백을 눌러도 같은 상품으로 처리해야 하므로,
            // Item01 전체를 덮는 입력 릴레이를 SlotView가 Inspector 직접 참조로 제공한다.
            BattleShopOfferHover hover = configuredSlots[i].PointerEvents;
            if (hover == null)
            {
                Debug.LogError($"[Shop] {i + 1}번 상품 슬롯에 BattleShopOfferHover가 연결되지 않았습니다.",
                    configuredSlots[i]);
                return false;
            }
            // Item01 안의 슬롯별 "Button" 오브젝트는 프리팹 기본값이 비활성 상태다(레거시 설계상
            // 다른 곳 클릭 후에야 보이게 돼 있었음), 그래서 이 버튼은 스스로 레이캐스트 클릭을
            // 받을 수 없다. 슬롯 루트는 항상 활성 상태이므로, 클릭도 같은 호버 릴레이(hover)를
            // 통해 전달한다.
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
        // (2026-08-22 정리, 사용자 확인: 여기 있던 RefreshOwnedInventory() 중복 호출 제거 - 이 메서드가
        // 끝나면 TryEnter가 곧바로 RefreshView()를 부르고, RefreshView()도 끝에서 RefreshOwnedInventory()를
        // 부르기 때문에 원래 매번 인벤토리를 두 번 그리고 있었다.)
        viewRoot.SetActive(false);
        return true;
    }

    /// <summary>
    /// 마우스가 슬롯 위에 있을 때(hover) 또는 슬롯을 클릭해 선택했을 때, 화면 오른쪽 상세 정보
    /// 영역(tagText/propertyText/damageText/equipmentInfoText + hoverPreviewImage)을 그 슬롯
    /// 내용으로 채운다. 카드/장비 중 무엇이 들어있는지에 따라 보여줄 텍스트 조합이 다르다(카드는
    /// TAG/PROPERTY/DAMAGE, 장비는 STR/DEX/VIT/WIS 스탯). 슬롯이 비어있거나 이미 팔렸으면
    /// HideOfferDetails로 정리한다. 호출부는 두 갈래다: (1) BattleShopOfferHover의 onEnter로
    /// 마우스를 올릴 때마다(단, purchaseButtonMode == None일 때만 — 뭔가 선택된 상태에서는 hover가
    /// 정보를 안 바꾼다), (2) SelectOfferForPurchase가 클릭으로 선택을 "고정"할 때 한 번.
    /// </summary>
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
            SetDetailText(tagText, true, $"TAG  {GetTargetLabel(battleCard)}");
            SetDetailText(propertyText, true, $"PROPERTY  {GetPropertyLabel(battleCard)}");
            SetDetailText(damageText, true, $"{Mathf.Max(0, card.damage)} DAMAGE");
            SetDetailText(equipmentInfoText, false, string.Empty);
            SetPreviewImage(CardArtResolver.ResolveDisplaySprite(card.myCardSprite), card.name);
            return;
        }

        if (equipment != null)
        {
            SetDetailText(tagText, false, string.Empty);
            SetDetailText(propertyText, false, string.Empty);
            SetDetailText(damageText, false, string.Empty);
            SetDetailText(equipmentInfoText, true,
                $"STR +{equipment.stroffset}\nDEX +{equipment.dexoffset}\nINT +{equipment.intoffset}\n" +
                $"WIS +{equipment.wisoffset}\nCAR +{equipment.caroffset}\nVIT +{equipment.vitoffset}\n" +
                $"공격 사거리 +{equipment.attackRange:0.##}");
            SetPreviewImage(equipment.myEquipSprite, equipment.cardname);
            return;
        }

        HideOfferDetails();
    }

    /// <summary>
    /// 상품 슬롯 안에 표시할 카드 핵심 정보를 만든다. 별도 상세 패널은 전체 설명을 담당하고,
    /// 슬롯에는 구매 비교에 필요한 MP·피해·회복만 짧게 표시한다.
    /// </summary>
    private static string BuildInlineCardSummary(CardData card)
    {
        if (card == null) return string.Empty;

        List<string> lines = new List<string> { $"MP {Mathf.Max(0, card.cost)}" };
        if (card.damage > 0) lines.Add($"DMG {card.damage}");
        if (card.heal > 0) lines.Add($"HEAL {card.heal}");
        if (!string.IsNullOrWhiteSpace(card.cardInfo)) lines.Add(card.cardInfo);
        return string.Join("\n", lines);
    }

    /// <summary>
    /// 상품 슬롯 안에 표시할 장비 핵심 정보를 만든다. 0인 보정치는 숨겨 실제로 바뀌는 능력치와
    /// 공격 사거리만 한눈에 비교할 수 있게 한다.
    /// </summary>
    private static string BuildInlineEquipmentSummary(EquipData equipment)
    {
        if (equipment == null) return string.Empty;

        List<string> bonuses = new List<string>();
        AddNonZeroStat(bonuses, "STR", equipment.stroffset);
        AddNonZeroStat(bonuses, "DEX", equipment.dexoffset);
        AddNonZeroStat(bonuses, "INT", equipment.intoffset);
        AddNonZeroStat(bonuses, "WIS", equipment.wisoffset);
        AddNonZeroStat(bonuses, "CAR", equipment.caroffset);
        AddNonZeroStat(bonuses, "VIT", equipment.vitoffset);
        if (!Mathf.Approximately(equipment.attackRange, 0f))
            bonuses.Add($"RANGE +{equipment.attackRange:0.##}");
        return string.Join("  ", bonuses);
    }

    /// <summary>0이 아닌 장비 능력치만 슬롯 요약 목록에 추가한다.</summary>
    private static void AddNonZeroStat(List<string> target, string statName, int value)
    {
        if (value != 0) target.Add($"{statName} {(value > 0 ? "+" : string.Empty)}{value}");
    }

    /// <summary>
    /// 진열 슬롯을 클릭해 "이걸 사겠다"고 선택을 확정한다(BattleShopOfferHover의 onClick으로 연결됨).
    /// purchaseButtonMode를 BuyOffer로 바꿔 공용 BUY/SELL 버튼을 "BUY"로 활성화하고, 정보 표시를
    /// 이 슬롯으로 고정한다(고정된 뒤에는 다른 슬롯에 마우스를 올려도 ShowOfferDetails의 hover 분기가
    /// purchaseButtonMode != None이라 무시됨 — 클릭으로 선택한 내용이 안 바뀌는 이유). 실제 구매는
    /// 이 메서드가 아니라 BUY 버튼 클릭 시 OnPurchaseButtonClicked → BuySelectedOffer → Buy(slot)에서
    /// 일어난다.
    /// </summary>
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
        if (IsDefaultCard(cardIndex)) return;

        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        if (playerDeck == null || !playerDeck.HasCard(cardIndex)) return;
        CardData card = BattleCardConnector.FindOriginalCard(cardIndex, originalCardDatabase);
        if (card == null) return;

        selectedPurchaseSlot = -1;
        selectedSellCardIndex = cardIndex;
        purchaseButtonMode = PurchaseButtonMode.SellCard;
        HideOfferDetails();
        SetPreviewImage(CardArtResolver.ResolveDisplaySprite(card.myCardSprite), card.name);
        if (purchaseButton == null) return;
        purchaseButton.gameObject.SetActive(true);
        TMP_Text label = purchaseButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "SELL";
    }

    /// <summary>보유 장비 슬롯을 눌렀을 때 같은 BuySellButton을 장비 판매 모드로 전환한다.</summary>
    private void SelectEquipmentSlotForSale(PlayerEquipmentSlotType slotType)
    {
        if (currentState == null) return;
        EquipData equippedEquipment = GetEquippedEquipment(slotType);
        if (equippedEquipment == null) return;

        selectedPurchaseSlot = -1;
        selectedSellCardIndex = -1;
        selectedSellEquipmentSlot = slotType;
        hasSelectedSellEquip = true;
        purchaseButtonMode = PurchaseButtonMode.SellEquipment;
        HideOfferDetails();
        SetPreviewImage(equippedEquipment.myEquipSprite, equippedEquipment.cardname);
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

    /// <summary>밸런스 데이터의 카드 환급 비율에 따라 보유 카드 1장을 판매한다.</summary>
    private void SellSelectedCard()
    {
        int index = selectedSellCardIndex;
        if (index < 0) { ResetPurchaseSelection(); return; }
        if (IsDefaultCard(index))
        {
            Debug.LogWarning($"[Shop] 기본 카드는 판매할 수 없습니다. 카드 {index}", this);
            ResetPurchaseSelection();
            return;
        }

        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        PlayerWallet wallet = ResolveCurrentPlayerWallet();
        if (playerDeck == null)
        {
            Debug.LogError("[Shop] 카드 판매 실패: PlayerDeck 참조가 없습니다.", this);
            ResetPurchaseSelection();
            return;
        }
        if (wallet == null)
        {
            Debug.LogError("[Shop] 카드 판매 실패: PlayerWallet 참조가 없습니다.", this);
            ResetPurchaseSelection();
            return;
        }
        if (!playerDeck.HasCard(index)) { ResetPurchaseSelection(); RefreshView(); return; }
        CardData card = BattleCardConnector.FindOriginalCard(index, originalCardDatabase);
        if (card == null) { ResetPurchaseSelection(); return; }

        int refund = GetCardSalePrice(card);
        if (!playerDeck.TryRemoveOwnedCard(index, 1, out int remainingOwned))
        {
            Debug.LogWarning($"[Shop] 카드 판매 실패: 장착 수량 또는 보유 수량을 확인하세요. 카드 {index}", this);
            ResetPurchaseSelection();
            RefreshOwnedInventory();
            return;
        }
        int remainingEquippedCopies = playerDeck.GetEquippedCopyCount(index);
        BattleGameManager.Instance?.CardDrawSystem?
            .RemoveRuntimeCardCopiesAboveEquippedCount(index, remainingEquippedCopies);
        wallet.AddGold(refund);
        Debug.Log($"[Shop] 카드 판매: {card.name} / {refund}G / 남은 보유 {remainingOwned}장", this);
        ResetPurchaseSelection();
        RefreshView();
    }

    /// <summary>BattleGameManager가 등록한 실제 Player의 PlayerDeck만 반환한다.</summary>
    private static PlayerDeck ResolveCurrentPlayerDeck()
    {
        return BattleGameManager.Instance != null && BattleGameManager.Instance.CurrentPlayer != null
            ? BattleGameManager.Instance.CurrentPlayer.GetComponentInParent<PlayerDeck>(true)
            : null;
    }

    /// <summary>BattleGameManager가 등록한 실제 Player의 장비 상태 원본을 반환한다.</summary>
    private static PlayerWeapon ResolveCurrentPlayerWeapon()
    {
        return BattleGameManager.Instance?.CurrentPlayerWeapon;
    }

    /// <summary>BattleGameManager가 등록한 실제 Player의 골드 지갑을 반환한다.</summary>
    private static PlayerWallet ResolveCurrentPlayerWallet()
    {
        return BattleGameManager.Instance?.CurrentPlayerWallet;
    }

    /// <summary>선택한 PlayerWeapon 슬롯의 장비를 해제하고 밸런스 데이터의 환급 비율만큼 지급한다.</summary>
    private void SellSelectedEquipment()
    {
        if (!hasSelectedSellEquip) { ResetPurchaseSelection(); return; }
        PlayerEquipmentSlotType slotType = selectedSellEquipmentSlot;
        PlayerWeapon playerWeapon = ResolveCurrentPlayerWeapon();
        PlayerWallet wallet = ResolveCurrentPlayerWallet();
        if (playerWeapon == null || wallet == null)
        {
            Debug.LogError("[Shop] 장비 판매 실패: PlayerWeapon 또는 PlayerWallet 참조가 없습니다.", this);
            ResetPurchaseSelection();
            return;
        }

        EquipData removedEquipment = playerWeapon.UnequipSlot(slotType);
        int saleGold = AddEquipmentSaleGold(wallet, removedEquipment);
        Debug.Log($"[Shop] 장비 판매: {slotType} / {saleGold}G", this);

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

    /// <summary>
    /// 오른쪽 상세 정보 영역을 전부 비운다(ShowOfferDetails가 켠 텍스트 4개 + 미리보기 이미지).
    /// 호출부가 많은 이유는 "정보를 지워야 하는 상황"이 여러 갈래라서다 — 마우스가 슬롯을 벗어날 때
    /// (BattleShopOfferHover의 onExit, purchaseButtonMode == None일 때만), 상점을 닫을 때(Close),
    /// 구매/판매를 확정하거나 취소해서 선택이 풀릴 때(BuySelectedOffer/ResetPurchaseSelection 계열),
    /// 슬롯이 비어있는 상태로 ShowOfferDetails가 불렸을 때. 전부 "지금 화면에 뭔가 상세정보가
    /// 떠 있으면 안 되는 시점"이라는 공통점이 있다.
    /// </summary>
    private void HideOfferDetails()
    {
        SetDetailText(tagText, false, string.Empty);
        SetDetailText(propertyText, false, string.Empty);
        SetDetailText(damageText, false, string.Empty);
        SetDetailText(equipmentInfoText, false, string.Empty);
        SetPreviewImage(null, string.Empty);
    }

    /// <summary>
    /// 오른쪽 상세정보 영역의 큰 미리보기 이미지+이름 텍스트를 채우거나(sprite != null) 비운다
    /// (sprite == null이면 전부 숨김). ShowOfferDetails/HideOfferDetails/SelectInventoryCardForSale/
    /// SelectEquipmentSlotForSale 등 "지금 뭘 보여줄지 바뀌는" 모든 지점에서 공통으로 이 메서드를 거친다
    /// — 미리보기 이미지를 켜고 끄는 로직이 이 한 곳에만 있다.
    /// </summary>
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
        if (ownedCardSlotPool != null)
        {
            PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
            List<int> ownedCardIndices = playerDeck != null
                ? new List<int>(playerDeck.OwnedCards.Keys)
                : new List<int>();
            ownedCardIndices.Sort();
            for (int i = 0; i < ownedCardSlotPool.Length; i++)
            {
                BattleShopOwnedCardSlotView slot = ownedCardSlotPool[i];
                if (slot == null) continue;
                bool hasCard = i < ownedCardIndices.Count;
                slot.gameObject.SetActive(hasCard);
                if (!hasCard) continue;

                int cardIndex = ownedCardIndices[i];
                CardData card = BattleCardConnector.FindOriginalCard(cardIndex, originalCardDatabase);
                slot.Display(
                    card,
                    playerDeck.GetOwnedCardCount(cardIndex),
                    playerDeck.GetEquippedCopyCount(cardIndex));
                // 기본 카드(0~4번)는 게임 시작 덱을 구성하는 필수 카드이므로 판매 클릭을 연결하지 않는다.
                // 그 외 카드는 레거시 판매 체인 대신 상점용 클릭 릴레이로 SELL 선택을 연결한다.
                BattleShopOfferHover hover = BattleComponentResolver.GetOrAdd<BattleShopOfferHover>(slot.gameObject, null);
                hover.Bind(
                    null,
                    null,
                    IsDefaultCard(cardIndex) ? null : () => SelectInventoryCardForSale(cardIndex));
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
        Sprite emptySlotSprite = equipmentDatabase != null && equipmentDatabase.equip != null &&
            equipmentDatabase.equip.Count > 0
            ? equipmentDatabase.equip[0]?.myEquipSprite
            : null;
        foreach (BattleShopOwnedEquipmentSlotView slot in ownedEquipmentSlots)
        {
            if (slot == null) continue;
            slot.gameObject.SetActive(true);
            EquipData equippedEquipment = GetEquippedEquipment(slot.SlotType);
            slot.Display(equippedEquipment, emptySlotSprite);
            BattleShopOfferHover hover = BattleComponentResolver.GetOrAdd<BattleShopOfferHover>(slot.gameObject, null);
            hover.Bind(null, null, equippedEquipment != null
                ? () => SelectEquipmentSlotForSale(slot.SlotType)
                : null);
        }
    }

    /// <summary>
    /// PlayerDeck.InitializeDefaultCards와 같은 기준으로 시작 카드 인덱스 0~4를 판정한다.
    /// 기본 카드는 상점 인벤토리에 표시하되 판매 선택과 실제 판매를 모두 막는다.
    /// </summary>
    private bool IsDefaultCard(int cardIndex)
    {
        int defaultCardTypeCount = shopBalanceData != null ? shopBalanceData.defaultCardTypeCount : 5;
        return cardIndex >= 0 && cardIndex < defaultCardTypeCount;
    }

    /// <summary>PlayerWeapon에서 지정한 부위의 현재 장비를 반환한다.</summary>
    private static EquipData GetEquippedEquipment(PlayerEquipmentSlotType slotType)
    {
        PlayerWeapon playerWeapon = ResolveCurrentPlayerWeapon();
        if (playerWeapon == null) return null;
        switch (slotType)
        {
            case PlayerEquipmentSlotType.LeftArm: return playerWeapon.LeftArm.CurrentEquipment;
            case PlayerEquipmentSlotType.RightArm: return playerWeapon.RightArm.CurrentEquipment;
            case PlayerEquipmentSlotType.Head: return playerWeapon.Head.CurrentEquipment;
            case PlayerEquipmentSlotType.Body: return playerWeapon.Body.CurrentEquipment;
            default: return null;
        }
    }

    /// <summary>장비 하나를 기존 상점 규칙인 구매가의 50%로 정산하고 실제 지급액을 반환한다.</summary>
    private int AddEquipmentSaleGold(PlayerWallet wallet, EquipData equipment)
    {
        if (wallet == null || equipment == null) return 0;
        float refundRate = shopBalanceData != null ? shopBalanceData.equipmentSaleRefundRate : 0.5f;
        int saleGold = Mathf.Max(0, Mathf.FloorToInt(equipment.cost * refundRate));
        wallet.AddGold(saleGold);
        return saleGold;
    }

    /// <summary>
    /// 새 장비 구매 때문에 자동으로 밀려난 기존 장비의 원가를 전액 반환한다.
    /// Player가 SELL을 선택한 경우의 할인 환급은 <see cref="AddEquipmentSaleGold"/>가 별도로 담당한다.
    /// </summary>
    private static int RefundReplacedEquipmentAtFullValue(PlayerWallet wallet, EquipData equipment)
    {
        if (wallet == null || equipment == null) return 0;

        int refundGold = Mathf.Max(0, equipment.cost);
        wallet.AddGold(refundGold);
        return refundGold;
    }

    /// <summary>EquipDatabase의 0번 빈 슬롯과 null 항목을 제외한 실제 장비 인덱스만 수집한다.</summary>
    private void CollectValidEquipmentIndices(List<int> targetIndices)
    {
        if (targetIndices == null || equipmentDatabase == null || equipmentDatabase.equip == null) return;
        for (int equipmentIndex = 1; equipmentIndex < equipmentDatabase.equip.Count; equipmentIndex++)
        {
            if (equipmentDatabase.equip[equipmentIndex] != null)
                targetIndices.Add(equipmentIndex);
        }
    }

    /// <summary>현재 PlayerWeapon이 처리할 수 있는 네 장비 종류인지 구매 전에 검증한다.</summary>
    private static bool IsSupportedEquipmentKind(WeaponKind equipmentKind) =>
        equipmentKind == WeaponKind.Hand ||
        equipmentKind == WeaponKind.Head ||
        equipmentKind == WeaponKind.Body ||
        equipmentKind == WeaponKind.TwoHand;

    private int GetCardPurchasePrice(CardData card)
    {
        if (card == null) return 0;
        float multiplier = shopBalanceData != null ? shopBalanceData.cardPurchasePriceMultiplier : 2f;
        return Mathf.Max(0, Mathf.RoundToInt(card.cardCost * multiplier));
    }

    private int GetCardSalePrice(CardData card)
    {
        if (card == null) return 0;
        float refundRate = shopBalanceData != null ? shopBalanceData.cardSaleRefundRate : 0.5f;
        return Mathf.Max(0, Mathf.FloorToInt(card.cardCost * refundRate));
    }

    private int GetEquipmentBasePrice(WeaponKind equipmentKind)
    {
        if (shopBalanceData == null)
            return equipmentKind == WeaponKind.TwoHand ? 90 :
                equipmentKind == WeaponKind.Body ? 75 : equipmentKind == WeaponKind.Head ? 70 : 60;

        switch (equipmentKind)
        {
            case WeaponKind.TwoHand: return shopBalanceData.twoHandBasePrice;
            case WeaponKind.Body: return shopBalanceData.bodyBasePrice;
            case WeaponKind.Head: return shopBalanceData.headBasePrice;
            default: return shopBalanceData.handBasePrice;
        }
    }

    private float GetRarePriceMultiplier() =>
        shopBalanceData != null ? shopBalanceData.rarePriceMultiplier : 1.6f;

    private float GetEpicPriceMultiplier() =>
        shopBalanceData != null ? shopBalanceData.epicPriceMultiplier : 2.5f;

    private float GetLegendaryPriceMultiplier() =>
        shopBalanceData != null ? shopBalanceData.legendaryPriceMultiplier : 4f;

    /// <summary>
    /// 이미 Inspector에 연결된 상세정보 TMP에 값을 쓰고 표시 여부만 바꾼다.
    /// 오브젝트를 생성하거나 크기·위치를 변경하지 않으며, ShowOfferDetails/HideOfferDetails가 공유한다.
    /// </summary>
    private static void SetDetailText(TMP_Text target, bool visible, string value)
    {
        if (target == null) return;
        target.text = value;
        target.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 카드의 대상(targetType: Self/Enemy/Ally/Character/Tile/AllEnemies)을 상세정보 TAG 줄에
    /// 표시할 영문 라벨로 바꾼다. ShowOfferDetails에서만 씀.
    /// </summary>
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

    /// <summary>
    /// 카드의 속성(cardType: PhysicalDamage/MagicDamage/그 외=서포트)을 상세정보 PROPERTY 줄에
    /// 표시할 영문 라벨로 바꾼다. ShowOfferDetails에서만 씀.
    /// </summary>
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

}
