using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BattleChestRewardType { Card, Equipment, Gold }

/// <summary>
/// 맵의 보상 상자(Box 타일)를 열고, 카드/장비/골드 중 하나를 무작위로 지급하는 UI·상태를 담당한다.
/// 타일별로 한 번 결정된 보상은 <c>pendingRewards</c>에 저장돼 다시 열어도(문 닫고 다시 클릭 등) 같은
/// 보상을 유지한다. 상자가 열려 있는 동안은 <c>BattleGameManager.LockBattleInputForOverlay</c>로
/// 뒤쪽 전투 조작을 잠그고, 닫히면 <c>UnlockBattleInputAfterOverlay</c>로 되돌린다(Player 사망 시에는
/// <see cref="ForceClose"/>로 강제 정리).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleChestRewardSystem : MonoBehaviour
{
    private sealed class PendingReward
    {
        public BattleChestRewardType Type;
        public CardData Card;
        public EquipData Equipment;
        public int Gold;
    }

    private const float DropToReadySeconds = 2.05f;
    private const float RewardDisplaySeconds = 2f;
    private const int GoldFallbackAmount = 25;

    private readonly HashSet<MapInfo> openedTiles = new HashSet<MapInfo>();
    private readonly Dictionary<MapInfo, PendingReward> pendingRewards = new Dictionary<MapInfo, PendingReward>();
    [Header("카드 데이터")]
    [SerializeField] private BattleCardDatabase battleCardDatabase;
    [SerializeField] private CardDatabase originalCardDatabase;
    private EquipDatabase equipmentDatabase;
    private BattleShopConfig shopConfig;
    private Canvas canvas;
    private Button dropButton;
    private Button openButton;
    private Image[] rewardImages;
    private GameObject lidClosed;
    private GameObject lidOpened;
    private TMP_Text goldRewardText;
    private MapInfo currentTile;
    private PendingReward currentReward;
    private Coroutine openingRoutine;
    private bool holdsModalLock;
    private bool rewardReady;

    private void Awake()
    {
        equipmentDatabase = BattleEquipmentDatabaseReference.Load()?.Database;
        shopConfig = BattleShopConfig.Load();
    }

    /// <summary>기존 호출부 호환용으로 카드 Database 참조를 명시적으로 교체한다.</summary>
    public void Configure(BattleCardDatabase battleCards, CardDatabase originalCards)
    {
        battleCardDatabase = battleCards;
        originalCardDatabase = originalCards;
    }

    /// <summary>
    /// Box 타일을 클릭했을 때 호출한다. 이미 다른 상자가 열려 있거나(currentTile != null), 대상이
    /// Box 타일이 아니거나, 이미 연 상자면 즉시 false를 반환하고 아무 것도 하지 않는다.
    /// 성공하면 보상을 결정(또는 이전에 결정된 보상을 재사용)하고 UI를 표시한 뒤 입력을 잠근다.
    /// </summary>
    public bool TryOpen(MapInfo tile)
    {
        if (tile == null || tile.Type != TileType.Box || openedTiles.Contains(tile) || currentTile != null)
            return false;
        if (!TryGetReward(tile, out PendingReward reward)) return false;

        EnsureView();
        if (canvas == null || rewardImages == null || rewardImages.Length == 0)
        {
            Debug.LogError("[Chest] Chest UI could not be created.", this);
            return false;
        }

        currentTile = tile;
        currentReward = reward;
        rewardReady = false;
        PrepareRewardImages(reward);
        canvas.gameObject.SetActive(true);
        AcquireModalLock();
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        openingRoutine = StartCoroutine(PlayOpenSequence());
        return true;
    }

    /// <summary>
    /// 이 타일이 이번에 어떤 보상을 줄지 결정만 하고 <c>pendingRewards</c>에 캐시한다.
    /// DataConfig(카드 보유 수, 골드, 장비 슬롯) 등 실제 플레이어 데이터는 이 단계에서 건드리지 않는다 —
    /// 실제 지급은 나중에 <see cref="ClaimCardReward"/>/<see cref="ClaimGoldReward"/>/<see cref="ClaimEquipment"/>가 한다.
    /// 이미 결정된 타일이면(다시 열거나 강제로 닫혔다 재오픈) 새로 굴리지 않고 캐시된 값을 그대로 돌려준다.
    /// </summary>
    private bool TryGetReward(MapInfo tile, out PendingReward reward)
    {
        if (pendingRewards.TryGetValue(tile, out reward) && reward != null) return true;

        List<CardData> cards = CollectCardRewardCandidates();
        List<int> equipment = CollectEquipmentRewardIndices();
        bool chooseCard = cards.Count > 0 && (equipment.Count == 0 || Random.value < 0.5f);
        if (chooseCard)
        {
            reward = new PendingReward
            {
                Type = BattleChestRewardType.Card,
                Card = cards[Random.Range(0, cards.Count)]
            };
        }
        else if (equipment.Count > 0)
        {
            reward = new PendingReward
            {
                Type = BattleChestRewardType.Equipment,
                Equipment = CreateEquipmentReward(equipment[Random.Range(0, equipment.Count)])
            };
        }
        else
        {
            reward = new PendingReward { Type = BattleChestRewardType.Gold, Gold = GoldFallbackAmount };
        }

        pendingRewards[tile] = reward;
        return true;
    }

    /// <summary>
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
    {
        EquipData reward = equipmentDatabase.equip[index].Clone();
        reward.weapon = shopConfig != null ? shopConfig.RollRarity(DataConfig.stage) : weaponSt.Common;
        int basePrice = reward.weaponKind == WeaponKind.TwoHand ? 90 :
            reward.weaponKind == WeaponKind.Body ? 75 : reward.weaponKind == WeaponKind.Head ? 70 : 60;
        reward.cost = reward.weapon == weaponSt.Legendary ? basePrice * 4 :
            reward.weapon == weaponSt.Epic ? Mathf.RoundToInt(basePrice * 2.5f) :
            reward.weapon == weaponSt.Rare ? Mathf.RoundToInt(basePrice * 1.6f) : basePrice;
        int rolls = reward.weapon == weaponSt.Legendary ? 10 :
            reward.weapon == weaponSt.Epic ? 6 : reward.weapon == weaponSt.Rare ? 4 : 0;
        for (int i = 0; i < rolls; i++)
        {
            switch (Random.Range(0, 4))
            {
                case 0: reward.stroffset++; break;
                case 1: reward.wisoffset++; break;
                case 2: reward.dexoffset++; break;
                case 3: reward.vitoffset++; break;
            }
        }
        return reward;
    }

    /// <summary>
    /// 레거시 상자 프리팹(TreasureChest_Start)의 Drop/Open 버튼 클릭을 코드로 그대로 흉내 내
    /// 원래 붙어 있던 연출(애니메이션·사운드)을 재사용한다. Drop 클릭 후 DropToReadySeconds만큼
    /// 기다렸다가 Open 클릭 + 뚜껑 이미지 전환 + 보상 이미지 표시를 하고, RewardDisplaySeconds만큼
    /// 더 보여준 뒤 <see cref="AutoClaimCurrentReward"/>로 자동 지급한다.
    /// </summary>
    private IEnumerator PlayOpenSequence()
    {
        if (dropButton != null) dropButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(DropToReadySeconds);
        if (currentTile == null) yield break;
        if (openButton != null) openButton.onClick.Invoke();
        SetRewardImagesActive(true);
        if (lidClosed != null) lidClosed.SetActive(false);
        if (lidOpened != null) lidOpened.SetActive(true);
        rewardReady = true;
        yield return new WaitForSecondsRealtime(RewardDisplaySeconds);
        AutoClaimCurrentReward();
        openingRoutine = null;
    }

    /// <summary>보상을 2초간 보여준 뒤 종류에 맞게 자동 지급하고 상자 이벤트를 끝낸다.</summary>
    private void AutoClaimCurrentReward()
    {
        if (!rewardReady || currentReward == null) return;

        switch (currentReward.Type)
        {
            case BattleChestRewardType.Card:
                ClaimCardReward();
                break;
            case BattleChestRewardType.Equipment:
                ClaimEquipment();
                break;
            case BattleChestRewardType.Gold:
                ClaimGoldReward();
                break;
        }
    }

    /// <summary>
    /// 상자를 열기 전, 보상 이미지 슬롯들을 미리 이번 보상에 맞게 채워 두되 화면에는 아직 표시하지 않는다
    /// (PlayOpenSequence가 뚜껑이 열리는 타이밍에 SetActive(true)로 실제로 보여준다).
    /// 카드/장비 보상이면 각각의 스프라이트를, 골드 보상이면 스프라이트 없이 금색 틴트만 준다.
    /// 이미지에 달려 있던 버튼은 리스너를 모두 지우고 비활성화한다 — 지급은 전부
    /// <see cref="AutoClaimCurrentReward"/>의 타이머로만 일어나고 클릭으로 트리거되지 않는다.
    /// </summary>
    private void PrepareRewardImages(PendingReward reward)
    {
        for (int i = 0; i < rewardImages.Length; i++)
        {
            Image image = rewardImages[i];
            if (image == null) continue;
            image.sprite = reward.Type == BattleChestRewardType.Card ? reward.Card.myCardSprite :
                reward.Type == BattleChestRewardType.Equipment ? reward.Equipment.myEquipSprite : null;
            image.color = reward.Type == BattleChestRewardType.Gold
                ? new Color(0.95f, 0.75f, 0.12f, 1f) : Color.white;
            image.preserveAspect = true;
            image.gameObject.SetActive(false);

            Button button = image.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }
        }

        if (goldRewardText != null)
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
    {
        return rewardReady && currentTile != null && currentReward != null &&
               currentReward.Type == type && !openedTiles.Contains(currentTile);
    }

    /// <summary>카드 보상을 실제로 지급한다(<c>DataConfig.AddDic</c> — 여기서 처음 실제 보유 카드 수가 늘어난다).</summary>
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
    }

    /// <summary>
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
    }

    /// <summary>
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
    }

    /// <summary>
    /// Player 사망 등으로 전투가 즉시 정지될 때 <c>BattleGameManager</c>가 호출한다.
    /// 열려 있는 보상 상자 UI와 입력 잠금을 정상 닫기(Close)와 동일하게 정리한다.
    /// </summary>
    public void ForceClose()
    {
        Close();
    }

    private void AcquireModalLock()
    {
        if (holdsModalLock) return;
        BattleGameManager.Instance?.LockBattleInputForOverlay();
        holdsModalLock = true;
    }

    private void ReleaseModalLock()
    {
        if (!holdsModalLock) return;
        holdsModalLock = false;
        BattleGameManager.Instance?.UnlockBattleInputAfterOverlay();
    }

    private void OnDisable()
    {
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        openingRoutine = null;
        ReleaseModalLock();
    }

    private void EnsureView()
    {
        if (canvas != null) return;
        BattleLegacyChestPrefabReference reference = BattleLegacyChestPrefabReference.Load();
        if (reference == null || reference.Prefab == null) return;

        GameObject root = new GameObject("Battle Legacy Chest", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 330;
        // 레거시 프리팹의 OnEnable/입력 로직이 전투용 보상 흐름과 동시에 실행되지 않도록
        // 복제하기 전에 부모를 비활성화한다. 외형과 애니메이션 버튼만 재사용한다.
        root.SetActive(false);
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject legacyChest = Instantiate(reference.Prefab, root.transform, false);
        // 전투에서는 BattleChestRewardSystem만 보상 지급과 종료를 담당한다.
        // 레거시 Treasure/GetItem이 클릭을 함께 받으면 중복 지급 또는 이벤트 잠금이 발생한다.
        foreach (Treasure legacyTreasure in legacyChest.GetComponentsInChildren<Treasure>(true))
            legacyTreasure.enabled = false;
        foreach (GetItem legacyRewardInput in legacyChest.GetComponentsInChildren<GetItem>(true))
            legacyRewardInput.enabled = false;

        RectTransform rect = legacyChest.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        dropButton = FindNamedComponent<Button>(legacyChest.transform, "Button_Drop");
        openButton = FindNamedComponent<Button>(legacyChest.transform, "Button_Open");
        if (dropButton != null) dropButton.gameObject.SetActive(false);
        if (openButton != null) openButton.gameObject.SetActive(false);
        lidClosed = FindNamedGameObject(legacyChest.transform, "Image_Lid_Closed");
        lidOpened = FindNamedGameObject(legacyChest.transform, "Image_Lid_Opened");

        List<Image> images = new List<Image>();
        foreach (Image image in legacyChest.GetComponentsInChildren<Image>(true))
            if (image.gameObject.name.StartsWith("Image_Item")) images.Add(image);
        rewardImages = images.ToArray();

        if (rewardImages.Length > 0)
        {
            goldRewardText = CreateText(rewardImages[0].rectTransform, "Gold Reward", Vector2.zero,
                rewardImages[0].rectTransform.rect.size, 42f);
            goldRewardText.color = Color.white;
            goldRewardText.gameObject.SetActive(false);
        }
    }

    private static TMP_Text CreateText(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TMP_Text text = rect.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private void SetRewardImagesActive(bool active)
    {
        if (rewardImages == null) return;
        for (int i = 0; i < rewardImages.Length; i++)
            if (rewardImages[i] != null) rewardImages[i].gameObject.SetActive(active && i == 0);
    }

    private static GameObject FindNamedGameObject(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.gameObject.name == objectName) return child.gameObject;
        return null;
    }

    private static T FindNamedComponent<T>(Transform root, string objectName) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            if (component != null && component.gameObject.name == objectName) return component;
        return null;
    }
}
