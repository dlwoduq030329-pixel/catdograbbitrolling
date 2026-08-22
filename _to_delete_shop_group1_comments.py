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

p = "Assets/renew/Battle/Shop/BattleCardShopSystem.cs"
apply(p, [
    # 1) Update 요약
    (
'''    private void Update()
    {
        if (viewRoot == null || !viewRoot.activeSelf || !Input.GetKeyDown(KeyCode.Escape)) return;''',
'''    /// <summary>
    /// 상점 UI가 열려 있는 동안 매 프레임 ESC 입력만 감지한다. 장비 구매 확인창이나
    /// 카드/장비 선택(구매·판매 미리보기)이 떠 있으면 상점 전체를 닫는 대신 그 선택만 취소하고,
    /// 아무것도 선택돼 있지 않을 때만 실제로 상점을 닫는다(단계적 ESC 처리).
    /// </summary>
    private void Update()
    {
        if (viewRoot == null || !viewRoot.activeSelf || !Input.GetKeyDown(KeyCode.Escape)) return;'''
    ),
    # 2) Awake 요약
    (
'''    private void Awake()
    {
        equipmentDatabase = BattleEquipmentDatabaseReference.Load()?.Database;
        shopConfig = BattleShopConfig.Load();
    }''',
'''    /// <summary>Resources 폴더에서 장비 데이터베이스와 상점 설정(BattleShopConfig)을 한 번 로드해둔다.</summary>
    private void Awake()
    {
        equipmentDatabase = BattleEquipmentDatabaseReference.Load()?.Database;
        shopConfig = BattleShopConfig.Load();
    }'''
    ),
    # 3) TryEnter 요약
    (
'''    public bool TryEnter(MapInfo tile)
    {''',
'''    /// <summary>
    /// 상점 타일에 진입을 시도한다. 이 타일을 처음 방문했다면 <see cref="GenerateOffers"/>로
    /// 판매 목록을 새로 뽑아 <see cref="stores"/>에 저장하고, 이미 방문한 적 있다면 저장해둔
    /// 상태(StoreState)를 그대로 재사용한다 — 같은 상점 타일을 다시 들어가도 목록이 안 바뀌는 이유.
    /// </summary>
    public bool TryEnter(MapInfo tile)
    {'''
    ),
    # 4) GenerateOffers 요약 + 영문 주석 한글화
    (
'''    private void GenerateOffers(StoreState state)
    {''',
'''    /// <summary>
    /// 이 상점 타일의 6개 슬롯에 무엇을 진열할지 한 번에 결정한다. 카드/장비 슬롯 개수(shopConfig)만큼
    /// 종류를 배치한 뒤 무작위로 섞고, 슬롯마다 해당 종류의 후보를 하나씩 뽑아 소모한다(같은 상품이
    /// 한 상점에 중복으로 뜨지 않도록 후보 목록에서 제거). 후보가 부족해 빈 슬롯이 남으면
    /// cardFallbacks/equipmentFallbacks(소모되지 않은 원본 목록)에서 다시 뽑아 채운다.
    /// </summary>
    private void GenerateOffers(StoreState state)
    {'''
    ),
    (
'''            // A small database can contain fewer than six unique, currently eligible
            // products. Never leave a visible shop slot empty: reuse a card first and
            // equipment only when no card reward is available.''',
'''            // 데이터베이스가 작으면 지금 조건에 맞는 고유 상품이 6개보다 적을 수 있다.
            // 화면에 보이는 상점 슬롯을 빈칸으로 남겨두지 않기 위해, 카드를 먼저 재사용하고
            // 카드 보상이 아예 없을 때만 장비로 채운다.'''
    ),
])
