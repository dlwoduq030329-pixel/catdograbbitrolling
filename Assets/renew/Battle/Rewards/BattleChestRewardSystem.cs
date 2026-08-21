using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BattleChestRewardType { Card, Equipment, Gold }

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
    private GameObject equipmentChoicePanel;
    private TMP_Text equipmentChoiceText;
    private TMP_Text goldRewardText;
    private Button equipDefaultButton;
    private Button equipLeftButton;
    private Button equipRightButton;
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

    private bool TryGetReward(MapInfo tile, out PendingReward reward)
    {
        if (pendingRewards.TryGetValue(tile, out reward) && reward != null) return true;

        List<CardData> cards = BuildEligibleCards();
        List<int> equipment = BuildEligibleEquipment();
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

    private List<CardData> BuildEligibleCards()
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
                ClaimEquipment(null);
                break;
            case BattleChestRewardType.Gold:
                ClaimGoldReward();
                break;
        }
    }

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
        if (equipmentChoicePanel != null) equipmentChoicePanel.SetActive(false);
    }

    private bool CanClaim(BattleChestRewardType type)
    {
        return rewardReady && currentTile != null && currentReward != null &&
               currentReward.Type == type && !openedTiles.Contains(currentTile);
    }

    private void ClaimCardReward()
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
    }

    private void OpenEquipmentChoice()
    {
        if (!CanClaim(BattleChestRewardType.Equipment) || currentReward.Equipment == null) return;
        EquipData equipment = currentReward.Equipment;
        bool hand = equipment.weaponKind == WeaponKind.Hand;
        equipmentChoiceText.text = $"{equipment.cardname} [{equipment.weapon}]\n" +
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
    }

    private void CompleteCurrentReward()
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
    }

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

    private static Button CreateButton(RectTransform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = new GameObject(label + " Button", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(220f, 58f);
        rect.GetComponent<Image>().color = new Color(0.22f, 0.27f, 0.38f, 1f);
        Button button = rect.GetComponent<Button>();
        button.onClick.AddListener(action);
        TMP_Text text = CreateText(rect, "Label", Vector2.zero, rect.sizeDelta, 24f);
        text.text = label;
        return button;
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
