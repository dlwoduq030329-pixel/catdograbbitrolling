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
        assert count == 1, (path, i, count, old[:150])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

p = "Assets/renew/Battle/Shop/BattleCardShopSystem.cs"
apply(p, [
    # 1) StoreState 클래스 + 필드에 설명 추가
    (
'''    private sealed class StoreState
    {
        internal readonly OfferKind[] Kinds = new OfferKind[SlotCount];
        internal readonly int[] OfferedCards = { -1, -1, -1, -1, -1, -1 };
        internal readonly EquipData[] OfferedEquipment = new EquipData[SlotCount];
        internal readonly bool[] Sold = new bool[SlotCount];
        internal int RerollPrice = 10;
    }

    private readonly Dictionary<MapInfo, StoreState> stores = new Dictionary<MapInfo, StoreState>();
    [Header("카드 데이터")]
    [SerializeField] private BattleCardDatabase battleCardDatabase;
    [SerializeField] private CardDatabase originalCardDatabase;
    private EquipDatabase equipmentDatabase;
    private BattleShopConfig shopConfig;
    private MapInfo currentStore;
    private StoreState currentState;
    private Canvas canvas;
    private GameObject viewRoot;''',
'''    /// <summary>
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
    private GameObject viewRoot;'''
    ),
    # 2) 나머지 필드들에 설명 추가
    (
'''    private Button[] offerButtons;
    private Image[] offerImages;
    private TMP_Text[] offerTexts;
    private TMP_Text[] offerPriceTexts;
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
    private bool hasSelectedSellEquip;''',
'''    private Button[] offerButtons;
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
    private bool hasSelectedSellEquip;'''
    ),
    # 3) Configure() 삭제 (죽은 코드 - 저장소 전체 참조 0, battleCardDatabase/originalCardDatabase는
    #    이미 [SerializeField]로 인스펙터에서 직접 설정됨)
    (
'''    public void Configure(BattleCardDatabase battleCards, CardDatabase originalCards)
    {
        battleCardDatabase = battleCards;
        originalCardDatabase = originalCards;
    }

    /// <summary>''',
'''    // (2026-08-22 정리, 사용자 확인: Configure(BattleCardDatabase, CardDatabase) 삭제됨 - 저장소
    // 전체에서 호출부 0개였다. battleCardDatabase/originalCardDatabase는 이미 [SerializeField]로
    // 인스펙터에서 직접 채워지므로 별도 재설정 메서드가 필요 없다.)

    /// <summary>'''
    ),
    # 4) TryEnter: 재진입 차단 추가 + 요약 갱신
    (
'''    /// <summary>
    /// 상점 타일에 진입을 시도한다. 이 타일을 처음 방문했다면 <see cref="GenerateOffers"/>로
    /// 판매 목록을 새로 뽑아 <see cref="stores"/>에 저장하고, 이미 방문한 적 있다면 저장해둔
    /// 상태(StoreState)를 그대로 재사용한다 — 같은 상점 타일을 다시 들어가도 목록이 안 바뀌는 이유.
    /// </summary>
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
        viewRoot.SetActive(true);''',
'''    /// <summary>
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
        viewRoot.SetActive(true);'''
    ),
])
