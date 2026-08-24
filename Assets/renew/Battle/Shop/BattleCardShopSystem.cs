using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Store 타일용 카드 전용 상점 MVP. 장비/인벤토리/자동 장착 로직을 포함하지 않는다.</summary>
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
    /// (리롤할 때마다 오르며, stores에 저장돼 있어 상점을 나갔다 다시 들어와도 유지된다).
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
    /// 상점 타일(MapInfo)별 StoreState 캐시. 같은 상점 타일에 처음 들어갈 때만 새로 만들고
    /// 그 뒤로는 재사용한다 — 그래서 한 번 정해진 진열 목록이 재방문해도 안 바뀐다.
    /// </summary>
    private readonly Dictionary<MapInfo, StoreState> stores = new Dictionary<MapInfo, StoreState>();
    /// <summary>
    /// TryEnter가 한 번 성공한 상점 타일을 다시 저장해, 같은 타일에 재진입 자체를 막는 데 쓴다
    /// (2026-08-22 추가, 사용자 확인 — Chest의 openedTiles와 같은 패턴).
    /// </summary>
    private readonly HashSet<MapInfo> enteredStores = new HashSet<MapInfo>();
    [Header("카드 데이터")]
    [SerializeField] private BattleCardDatabase battleCardDatabase;
    [SerializeField] private CardDatabase originalCardDatabase;
    /// <summary>Resources의 BattleEquipmentDatabaseReference에서 Awake 때 채워지는 장비 원본 목록.</summary>
    private EquipDatabase equipmentDatabase;
    /// <summary>Resources의 BattleShopConfig(등급/부위 가중치, 카드·장비 슬롯 수, 리롤 초깃값 등 상점 밸런스 설정).</summary>
    private BattleShopConfig shopConfig;
    private MapInfo currentStore;
    private StoreState currentState;
    private Canvas canvas;
    /// <summary>상점 UI 전체의 루트 오브젝트. null이면 아직 EnsureView로 생성/바인딩 전이라는 뜻.</summary>
    private GameObject viewRoot;
    private TMP_Text goldText;
    private TMP_Text rerollText;
    private Button rerollButton;
    private Button[] offerButtons;
    private Image[] offerImages;
    private TMP_Text[] offerTexts;
    private TMP_Text[] offerPriceTexts;
    private int pendingEquipmentSlot = -1;
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
    private InventoryStore[] ownedCardSlots;
    /// <summary>보유 장비(좌/우손·몸통·머리) 슬롯들(RefreshOwnedInventory가 매번 다시 채움).</summary>
    private EquipStore[] ownedEquipmentSlots;
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
    private EquipState selectedSellEquipState;
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

    /// <summary>Resources 폴더에서 장비 데이터베이스와 상점 설정(BattleShopConfig)을 한 번 로드해둔다.</summary>
    private void Awake()
    {
        equipmentDatabase = BattleEquipmentDatabaseReference.Load()?.Database;
        shopConfig = BattleShopConfig.Load();
    }

    /// <summary>기존 호출부 호환용으로 카드 Database 참조를 명시적으로 교체한다.</summary>
    // (2026-08-22 정리, 사용자 확인: Configure(BattleCardDatabase, CardDatabase) 삭제됨 - 저장소
    // 전체에서 호출부 0개였다. battleCardDatabase/originalCardDatabase는 이미 [SerializeField]로
    // 인스펙터에서 직접 채워지므로 별도 재설정 메서드가 필요 없다.)

    /// <summary>
    /// 상점 타일에 진입을 시도한다. 이미 한 번 들어갔던 타일(enteredStores)이면 즉시 실패해
    /// 같은 상점 타일에 재진입 자체가 안 되게 막는다(2026-08-22 추가, 사용자 확인 — Chest의
    /// "한 번 연 상자는 다시 못 엶"과 같은 설계). 처음 들어가는 타일이면 <see cref="GenerateOffers"/>로
    /// 판매 목록을 새로 뽑아 <see cref="stores"/>에 저장한다.
    /// </summary>
    public bool TryEnter(MapInfo tile)
    {
        if (tile == null || tile.Type != TileType.Store || enteredStores.Contains(tile)) return false;
        EnsureView();
        if (viewRoot == null)
        {
            Debug.LogError("[Shop] 상점 UI를 생성하지 못했습니다.", this);
            return false;
        }

        enteredStores.Add(tile);
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
        BattleGameManager.Instance?.SetShopOpen(true);
        RefreshView();
        Debug.Log($"[Shop] {tile.name} 상점 진입", tile);
        return true;
    }

    /// <summary>
    /// 이 상점 타일의 6개 슬롯에 무엇을 진열할지 한 번에 결정한다. 카드/장비 슬롯 개수(shopConfig)만큼
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
        List<int> cardCandidates = BuildEligibleCards();
        List<int> equipmentCandidates = new List<int>();
        if (equipmentDatabase != null)
            for (int i = 1; i < equipmentDatabase.equip.Count; i++) equipmentCandidates.Add(i);
        // cardFallbacks/equipmentFallbacks: 위 두 목록을 소모되기 "전" 시점에 통째로 복사해둔 원본이다.
        // 아래 fallback 분기(카드/장비 후보가 바닥나 슬롯이 빈 채로 남을 때)에서만 쓰이며, 여기서 뽑은
        // 상품은 이미 다른 슬롯에 나온 것과 중복될 수 있다(원본 개수 부족을 메우기 위한 최후 수단이라
        // 중복 방지를 포기하는 구조). "판매 콜백"이나 "이미 2장 있는 카드" 처리와는 무관하다.
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
    /// 후보 목록에서 장비 하나를 뽑는다. 1차로 <c>shopConfig.RollEquipmentKind</c>(부위별 가중치
    /// 추첨, 현재 40/30/20/10 손/몸통/머리/양손)로 선호 부위를 정하고, 그 부위 후보가 있으면
    /// 풀을 좁힌다(없으면 전체 후보 그대로 사용). 2차로 같은 풀 안에서 "이미 장착 중=가중치1,
    /// 미장착=가중치4"로 다시 추첨해 안 써본 장비가 더 잘 나오게 한다.
    /// </summary>
    private int PickEquipmentCandidate(List<int> candidates)
    {
        if (candidates == null || candidates.Count == 0) return -1;
        // 1차 추첨: 부위(WeaponKind)를 먼저 정한다.
        WeaponKind preferredKind = shopConfig != null
            ? shopConfig.RollEquipmentKind(DataConfig.stage)
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
    private static bool IsEquipped(int equipmentIndex)
    {
        return (DataConfig.leftDa != null && DataConfig.leftDa.weaponIndex == equipmentIndex) ||
               (DataConfig.rightDa != null && DataConfig.rightDa.weaponIndex == equipmentIndex) ||
               (DataConfig.bodyDa != null && DataConfig.bodyDa.weaponIndex == equipmentIndex) ||
               (DataConfig.headDa != null && DataConfig.headDa.weaponIndex == equipmentIndex);
    }

    /// <summary>
    /// 지정한 장비 원본을 복제(Clone)해 등급을 <c>shopConfig.RollRarity</c>로 굴리고, 부위별
    /// 기본가(양손90/몸통75/머리70/한손60)에 등급 배율(rare1.6/epic2.5/legendary4)을 곱해
    /// 가격을 매긴다. 등급이 높을수록 보너스 스탯 굴림 횟수(bonusRolls)도 늘어난다
    /// (rare4/epic6/legendary10회, 매 회 STR/WIS/DEX/VIT 중 하나를 무작위로 +1).
    /// </summary>
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

        if (currentState.Kinds[slot] == OfferKind.Equipment)
        {
            BuyEquipment(slot);
            return;
        }
        if (currentState.Kinds[slot] != OfferKind.Card || currentState.OfferedCards[slot] < 0) return;

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
        int price = Mathf.Max(0, originalCard.cardCost * 2);
        // 카드 1종당 최대 2장 보유 규칙 — BuildEligibleCards가 애초에 후보에서 걸러내지만, fallback
        // 재사용(GenerateOffers)이나 다른 슬롯에서 같은 카드를 이미 산 경우까지 대비해 여기서도 다시 확인한다.
        if (ownedCount >= 2) { Debug.LogWarning($"[Shop] {originalCard.name} 보유 한도 2장", this); RefreshView(); return; }
        if (DataConfig.playerMoney < price) { Debug.LogWarning($"[Shop] 골드 부족: {price}G 필요", this); return; }

        DataConfig.playerMoney -= price;
        playerDeck.AddOwnedCard(cardIndex, 1);
        DataConfig.CardsCount[cardIndex] = playerDeck.GetOwnedCardCount(cardIndex);
        currentState.Sold[slot] = true;
        Debug.Log($"[Shop] 카드 구매: {originalCard.name} / {price}G", this);
        RefreshView();
    }

    /// <summary>
    /// 장비 슬롯 구매를 시작한다. pendingEquipmentSlot을 세팅한 뒤 바로
    /// <see cref="ConfirmEquipmentPurchaseInHand"/>(null)를 호출해 확인창 없이 즉시 구매/장착까지
    /// 이어간다(그 이유는 ConfirmEquipmentPurchaseInHand 요약 참고).
    /// </summary>
    private void BuyEquipment(int slot)
    {
        EquipData equipment = currentState.OfferedEquipment[slot];
        if (equipment == null) return;
        pendingEquipmentSlot = slot;
        ConfirmEquipmentPurchaseInHand(null);
    }

    /// <summary>
    /// 장비 구매를 실제로 처리한다. equipLeft는 항상 null로 호출된다(2026-08-22 정리, 사용자 확인:
    /// "왼손/오른손 직접 선택" 확인 UI(EnsureEquipmentConfirmationView + Confirm/Left/Right/Cancel)는
    /// 호출부가 0개라 한 번도 생성된 적 없는 죽은 코드였다 — BuyEquipment가 확인창 없이 곧바로
    /// 이 메서드를 null로 호출해, 항상 DataConfig.GetWeapon 경로(빈 손 자동 장착, 양손이 다
    /// 차있으면 왼손 강제 교체)로 구매+장착이 즉시 끝난다. equipLeft 매개변수와
    /// weaponKind == Hand 분기는 특정 손을 강제 지정하는 기능을 되살릴 때를 위해 남겨둔다.)
    /// </summary>
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

    /// <summary>
    /// ESC 등으로 대기중인 장비구매를 취소한다. pendingEquipmentSlot만 되돌리면 되는 이유는
    /// 위 ConfirmEquipmentPurchaseInHand 설명대로 확인 UI 자체가 죽은 코드라 애초에 열리는 패널이
    /// 없기 때문이다 — Update()가 이 메서드를 호출하는 건 재화 부족 등으로 ConfirmEquipmentPurchaseInHand가
    /// 조기 반환해 pendingEquipmentSlot이 slot 값 그대로 남아있는 경우를 되돌리기 위해서다.
    /// </summary>
    private void CancelEquipmentPurchase()
    {
        pendingEquipmentSlot = -1;
    }

    // (2026-08-22 정리, 사용자 확인: GetComparableEquipment/GetReplacementRefund/FormatEquipment/
    // GetSlotRefund 4개 삭제됨 - 호출부가 저장소 전체에서 0개였다. 방금 지운 EnsureEquipmentConfirmationView
    // (제목이 "EQUIPMENT COMPARISON"였음)가 완성됐다면 "현재 장착 장비 vs 구매하려는 장비" 비교 텍스트를
    // 만드는 데 썼을 헬퍼들로 추정되지만, 그 UI 자체가 한 번도 연결된 적 없어 이 헬퍼들도 같이 고아가 됐다.)

    /// <summary>
    /// 골드를 내고 6슬롯 전체를 GenerateOffers로 완전히 새로 뽑는다(일부 슬롯만 바꾸는 게 아니라
    /// 진열 전체가 리셋됨 — 안 팔린 카드/장비도 전부 사라지고 새 목록으로 대체된다). 다음 리롤 가격은
    /// 2배로 오르되 shopConfig.maximumRerollPrice(기본 160G)에서 상한이 걸린다. Close()와는 무관한
    /// 별개 동작이다 — Close()는 Reroll()을 호출하지 않는다(오해하기 쉬운 부분이라 명시해둠).
    /// </summary>
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

    /// <summary>
    /// 상점 UI를 정상적으로 닫는 내부 경로다(ESC나 닫기 버튼이 이걸 호출 — TryBindSceneStoreView에서
    /// binding.EscButton.onClick과 CreateButton으로 만든 CLOSE 버튼 둘 다 이 메서드에 연결됨).
    /// 대기 중이던 장비구매/선택 상태를 전부 취소하고, 모달 입력잠금을 풀고, 이 타일의 currentState
    /// 참조를 비운다(다음에 다른 상점 타일에 들어갈 때 실수로 이전 상태를 쓰지 않도록).
    /// </summary>
    private void Close()
    {
        CancelEquipmentPurchase();
        ResetPurchaseSelection();
        HideOfferDetails();
        if (viewRoot != null) viewRoot.SetActive(false);
        BattleGameManager.Instance?.SetShopOpen(false);
        ReleaseModalLock();
        currentStore = null;
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
        ReleaseModalLock();
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
        if (goldText != null) goldText.text = $"Gold : {DataConfig.playerMoney}G";
        if (rerollText != null) rerollText.text = $"REROLL {currentState.RerollPrice}G";
        if (rerollButton != null)
            rerollButton.interactable = DataConfig.playerMoney >= currentState.RerollPrice;
        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
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
        if (equipmentDatabase != null)
            for (int i = 1; i < equipmentDatabase.equip.Count; i++) eligibleEquipment.Add(i);

        for (int i = 0; i < SlotCount; i++)
        {
            if (state.Kinds[i] != OfferKind.None) continue;
            if (eligibleCards.Count > 0)
            {
                state.Kinds[i] = OfferKind.Card;
                state.OfferedCards[i] = eligibleCards[Random.Range(0, eligibleCards.Count)];
            }
            else if (eligibleEquipment.Count > 0)
            {
                state.Kinds[i] = OfferKind.Equipment;
                state.OfferedEquipment[i] = CreateRandomEquipment(PickEquipmentCandidate(eligibleEquipment));
            }
        }
    }

    // (2026-08-22 정리, 사용자 확인: GetRarityColor 삭제됨 - 저장소 전체에서 호출부 0개.
    // 등급별 색상을 표시하려던 흔적으로 보이나 실제로 UI에 연결된 적이 없다.)

    /// <summary>
    /// 상점 뷰가 아직 없으면(viewRoot == null) 딱 한 번만 TryBindSceneStoreView로 만든다. TryEnter가
    /// 상점 타일에 들어갈 때마다 호출하지만, 두 번째 방문부터는 viewRoot가 이미 있어 즉시 반환한다.
    /// </summary>
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
        tagText = FindComponentByName<TMP_Text>(storeRoot, "TText");
        propertyText = FindComponentByName<TMP_Text>(storeRoot, "SText");
        damageText = FindComponentByName<TMP_Text>(storeRoot, "PText");
        equipmentInfoText = FindComponentByName<TMP_Text>(storeRoot, "InText");
        hoverPreviewImage = binding.PreviewImage;
        Transform selectedImage = FindTransformByName(storeRoot, "SelectedImage");
        selectedImageRoot = selectedImage != null ? selectedImage.gameObject : null;
        selectedCardNameText = selectedImage != null
            ? FindComponentByName<TMP_Text>(selectedImage, "CardName")
            : null;

        Transform inventoryRoot = FindTransformByName(storeRoot, "Inventory");
        ownedInventoryScroll = inventoryRoot != null ? inventoryRoot.GetComponent<ScrollRect>() : null;
        ownedCardSlots = inventoryRoot != null
            ? inventoryRoot.GetComponentsInChildren<InventoryStore>(true)
            : System.Array.Empty<InventoryStore>();
        Transform equipmentRoot = FindTransformByName(storeRoot, "EquipMent");
        ownedEquipmentSlots = equipmentRoot != null
            ? equipmentRoot.GetComponentsInChildren<EquipStore>(true)
            : System.Array.Empty<EquipStore>();
        purchaseButton = FindComponentByName<Button>(storeRoot, "BuySellButton");
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
        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        if (playerDeck == null || !playerDeck.HasCard(cardIndex)) return;
        CardData card = BattleCardConnector.FindOriginalCard(cardIndex, originalCardDatabase);
        if (card == null) return;

        selectedPurchaseSlot = -1;
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
        PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
        if (playerDeck == null)
        {
            Debug.LogError("[Shop] 카드 판매 실패: PlayerDeck 참조가 없습니다.", this);
            ResetPurchaseSelection();
            return;
        }
        if (!playerDeck.HasCard(index)) { ResetPurchaseSelection(); RefreshView(); return; }
        CardData card = BattleCardConnector.FindOriginalCard(index, originalCardDatabase);
        if (card == null) { ResetPurchaseSelection(); return; }

        int refund = Mathf.Max(0, card.cardCost / 2);
        if (!playerDeck.TryRemoveOwnedCard(index, 1, out int remainingOwned))
        {
            Debug.LogWarning($"[Shop] 카드 판매 실패: 장착 수량 또는 보유 수량을 확인하세요. 카드 {index}", this);
            ResetPurchaseSelection();
            RefreshOwnedInventory();
            return;
        }
        if (remainingOwned <= 0) DataConfig.CardsCount.Remove(index);
        else DataConfig.CardsCount[index] = remainingOwned;
        CopyEquippedDeckToLegacyCardData(playerDeck);
        int remainingEquippedCopies = playerDeck.GetEquippedCopyCount(index);
        BattleGameManager.Instance?.CardDrawSystem?
            .RemoveRuntimeCardCopiesAboveEquippedCount(index, remainingEquippedCopies);
        DataConfig.playerMoney += refund;
        RefreshLinkedCardInventories();
        Debug.Log($"[Shop] 카드 판매: {card.name} / {refund}G / 남은 보유 {remainingOwned}장", this);
        ResetPurchaseSelection();
        RefreshView();
    }

    /// <summary>PlayerDeck에서 확정된 장착 덱을 기존 저장·UI 호환 목록에 복사한다.</summary>
    private static void CopyEquippedDeckToLegacyCardData(PlayerDeck playerDeck)
    {
        DataConfig.cardData.Clear();
        if (playerDeck == null || playerDeck.EquippedCards == null) return;
        foreach (int equippedCardIndex in playerDeck.EquippedCards)
            if (equippedCardIndex >= 0) DataConfig.cardData.Add(equippedCardIndex);
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

    /// <summary>판매 결과를 상점 밖 일반 카드 인벤토리에도 즉시 전달한다.</summary>
    private static void RefreshLinkedCardInventories()
    {
        foreach (InventorySetting inventory in
                 Object.FindObjectsByType<InventorySetting>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (inventory != null) inventory.RefreshDeckEditorVisuals();
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
        SetTextVisible(tagText, false, string.Empty);
        SetTextVisible(propertyText, false, string.Empty);
        SetTextVisible(damageText, false, string.Empty);
        SetTextVisible(equipmentInfoText, false, string.Empty);
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
        if (ownedCardSlots != null)
        {
            PlayerDeck playerDeck = ResolveCurrentPlayerDeck();
            List<int> ownedCardIndices = playerDeck != null
                ? new List<int>(playerDeck.OwnedCards.Keys)
                : new List<int>();
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
                // InventoryStore.OnPointerDown은 legacy 판매 체인(StoreManager/StoreSet)이 같이
                // 비활성화되면서 덩달아 죽어, 보유 카드를 눌러도 아무 반응이 없었다. 상점 진열
                // 슬롯과 같은 호버 릴레이를 재사용해 클릭 시 SELL 버튼이 뜨도록 연결한다.
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
    /// <summary>
    /// EquipStore.Init()은 DataPool.Instance를 요구해 Battle 상점에서 그대로 못 쓴다(DataPool이
    /// 갱신 안 돼 있을 수 있음). 그래서 리플렉션으로 private 필드(state/thisIMG)에 직접 접근해
    /// "지금 장착된 장비 아이콘을 그려주는 부분"만 골라 흉내낸다 — EquipStore 클래스 자체를 고치지
    /// 않고 화면 표시만 대신 처리하는 우회 방법. state는 이 슬롯이 왼손/오른손/몸통/머리 중 무엇인지,
    /// thisIMG는 아이콘을 그릴 Image 컴포넌트.
    /// </summary>
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

    /// <summary>
    /// 지정한 장비 부위(state)에 지금 장착된 장비의 데이터베이스 인덱스를 반환한다(0=미장착).
    /// RefreshOwnedEquipmentSlot 하나에서만 쓰이는 작은 매핑 헬퍼라 그 바로 아래에 놓여 있다.
    /// </summary>
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

    /// <summary>
    /// 텍스트 오브젝트를 값과 함께 켜거나(visible=true) 통째로 숨긴다(visible=false, 이때는 value를
    /// 빈 문자열로 넘기는 게 관례). ShowOfferDetails/HideOfferDetails가 4개 텍스트를 한 줄씩 켜고 끄는 데 씀.
    /// </summary>
    private static void SetTextVisible(TMP_Text target, bool visible, string value)
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

    // (2026-08-22 정리, 사용자 확인: TryCreateLegacyView 삭제됨 - EnsureView()는 TryBindSceneStoreView()만
    // 호출하고 이 메서드는 어디서도 호출되지 않는 완전한 죽은 코드였다. 씬에 이미 배치된 Event_Store
    // 인스턴스를 못 찾는 경우 EnsureView()는 그냥 오류 로그만 남기고 실패한다.)

    /// <summary>
    /// root의 모든 자손(비활성 포함) 중에서 GameObject 이름이 objectName과 정확히 일치하는 T 컴포넌트를
    /// 찾는다. 레거시 프리팹(Event_Store 등)은 Inspector 참조 없이 이름만으로 슬롯/버튼/텍스트를
    /// 찾아 쓰는 구조라, TryBindSceneStoreView가 이 방식으로 필요한 UI 요소들을 전부 연결한다.
    /// 이름 검색이라 프리팹 구조가 바뀌면(오브젝트 이름 변경) 조용히 null을 반환하니 주의.
    /// </summary>
    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            if (component != null && component.gameObject.name == objectName) return component;
        return null;
    }

    /// <summary>FindComponentByName과 같은 방식의 이름 검색이지만 컴포넌트가 아니라 Transform 자체를 찾는다.</summary>
    private static Transform FindTransformByName(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != null && child.gameObject.name == objectName) return child;
        return null;
    }

}
