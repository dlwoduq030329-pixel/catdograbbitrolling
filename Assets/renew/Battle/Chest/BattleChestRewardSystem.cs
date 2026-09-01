using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>골드는 모든 상자에 공통 지급되므로 메인 보상 타입은 카드와 장비만 구분한다.</summary>
public enum BattleChestRewardType { Card, Equipment }

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

    // 레거시 Drop 연출이 끝나는 2.05초 뒤 보상을 공개하고, 전체 약 3초 시점에 자동 지급·종료한다.
    private const float DropAnimationSeconds = 2f;
    private const float RewardVisibleSeconds = 2f;
    private const int CommonGoldRewardAmount = 25;

    private readonly HashSet<MapInfo> openedTiles = new HashSet<MapInfo>();
    private readonly Dictionary<MapInfo, PendingReward> pendingRewards = new Dictionary<MapInfo, PendingReward>();
    [Header("카드 데이터")]
    [SerializeField] private BattleCardDatabase battleCardDatabase;
    [SerializeField] private CardDatabase originalCardDatabase;
    [Header("장비 및 밸런스 데이터")]
    [Tooltip("상자에서 지급할 장비의 원본 목록입니다.")]
    [SerializeField] private EquipDatabase equipmentDatabase;
    [Tooltip("상점과 공유하는 장비 등급 및 부위 추첨 가중치입니다.")]
    [SerializeField] private BattleShopBalanceData shopBalanceData;
    [Header("상자 연출 UI")]
    [Tooltip("Battle 전용으로 복사한 상자 연출 프리팹입니다. 레거시 원본이 아니라 renew/Battle 아래의 사본을 직접 참조합니다.")]
    [SerializeField] private GameObject chestOverlayPrefab;
    private Canvas chestOverlayCanvas;
    private BattleChestView chestView;
    private MapInfo currentTile;
    private PendingReward currentReward;
    private Coroutine openingRoutine;
    private bool holdsModalLock;
    private bool rewardReady;

    /// <summary>
    /// 현재 상자 이벤트 화면이 열려 있는지 외부에 알리는 상태값이다.
    /// TryOpen 성공 시 true가 되고, 정상 지급 완료·강제 종료·컴포넌트 비활성화 시 false로 돌아간다.
    /// </summary>
    public bool ChestOpen { get; private set; }

    /// <summary>
    /// Box 타일을 클릭했을 때 호출한다. 이미 다른 상자가 열려 있거나(currentTile != null), 대상이
    /// Box 타일이 아니거나, 이미 연 상자면 즉시 false를 반환하고 아무 것도 하지 않는다.
    /// 성공하면 보상을 결정(또는 이전에 결정된 보상을 재사용)하고 UI를 표시한 뒤 입력을 잠근다.
    /// </summary>
    public bool TryOpen(MapInfo tile)
    {
        if (tile == null || tile.Type != TileType.Box || openedTiles.Contains(tile) || currentTile != null)
            return false;
        PendingReward reward = GetOrCreatePendingReward(tile);
        if (reward == null)
            return false;

        EnsureChestOverlayViewCreated();
        if (chestOverlayCanvas == null || chestView == null)
        {
            Debug.LogError("[Chest] Chest UI could not be created.", this);
            return false;
        }

        currentTile = tile;
        currentReward = reward;
        rewardReady = false;
        ChestOpen = true;
        chestOverlayCanvas.gameObject.SetActive(true);
        chestView.Show();
        PrepareRewardView(reward);
        AcquireModalLock();
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        openingRoutine = StartCoroutine(PlayOpenSequence());
        return true;
    }

    /// <summary>
    /// 이 타일이 이번에 어떤 보상을 줄지 결정만 하고 <c>pendingRewards</c>에 캐시한다.
    /// PlayerDeck·PlayerWallet·PlayerWeapon 등 실제 플레이어 데이터는 이 단계에서 건드리지 않는다 —
    /// 실제 지급은 나중에 <see cref="GrantCurrentRewardAndCloseChest"/>가 공통 골드와 메인 보상을 함께 처리한다.
    /// 이미 결정된 타일이면(다시 열거나 강제로 닫혔다 재오픈) 새로 굴리지 않고 캐시된 값을 그대로 돌려준다.
    /// </summary>
    private PendingReward GetOrCreatePendingReward(MapInfo tile)
    {
        if (pendingRewards.TryGetValue(tile, out PendingReward cachedReward) && cachedReward != null)
            return cachedReward;

        List<CardData> cards = CollectCardRewardCandidates();
        List<int> equipment = CollectEquipmentRewardIndices();
        if (cards.Count == 0 && equipment.Count == 0)
        {
            Debug.LogError("[Chest] 지급 가능한 카드와 장비가 모두 없어 상자 보상을 생성할 수 없습니다.", tile);
            return null;
        }

        bool chooseCard = cards.Count > 0 && (equipment.Count == 0 || Random.value < 0.5f);
        if (chooseCard)
        {
            cachedReward = new PendingReward
            {
                Type = BattleChestRewardType.Card,
                Card = cards[Random.Range(0, cards.Count)]
            };
        }
        else
        {
            cachedReward = new PendingReward
            {
                Type = BattleChestRewardType.Equipment,
                Equipment = CreateEquipmentReward(equipment[Random.Range(0, equipment.Count)])
            };
        }

        cachedReward.Gold = CommonGoldRewardAmount;
        pendingRewards[tile] = cachedReward;
        return cachedReward;
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
        PlayerDeck playerDeck = GetCurrentPlayerDeck();
        foreach (BattleCardData battleCard in battleCardDatabase.Cards)
        {
            if (battleCard == null || battleCard.legacyCardIndex < 0) continue;
            int owned = playerDeck != null
                ? playerDeck.GetOwnedCardCount(battleCard.legacyCardIndex)
                : PlayerDeck.MaximumOwnedCopiesPerCard;
            CardData original = BattleCardConnector.FindOriginalCard(battleCard.legacyCardIndex, originalCardDatabase);
            if (owned < PlayerDeck.MaximumOwnedCopiesPerCard && original != null) result.Add(original);
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
    /// 뽑힌 장비 인덱스의 원본 EquipData를 복제한 뒤, 상점과 같은 <c>BattleEquipmentRewardRoller</c>로
    /// 등급을 굴리고, 등급에 따라 가격(부위별 기본가 x 등급 배수)과 스탯 보너스(등급별 굴림 횟수만큼
    /// STR/DEX/INT/WIS/CAR/VIT 중 하나씩 +1)를 부여한다. 상점 장비와 같은 등급 확률표를 재사용하기 위해
    /// 인스펙터에 연결된 shopBalanceData의 가중치를 사용한다.
    /// </summary>
    private EquipData CreateEquipmentReward(int index)
    {
        EquipData reward = equipmentDatabase.equip[index].Clone();
        int currentStage = BattleGameManager.Instance != null ? BattleGameManager.Instance.CurrentStage : 1;
        reward.weapon = BattleEquipmentRewardRoller.RollRarity(shopBalanceData, currentStage);
        int basePrice = reward.weaponKind == WeaponKind.TwoHand ? 90 :
            reward.weaponKind == WeaponKind.Body ? 75 : reward.weaponKind == WeaponKind.Head ? 70 : 60;
        reward.cost = reward.weapon == weaponSt.Legendary ? basePrice * 4 :
            reward.weapon == weaponSt.Epic ? Mathf.RoundToInt(basePrice * 2.5f) :
            reward.weapon == weaponSt.Rare ? Mathf.RoundToInt(basePrice * 1.6f) : basePrice;
        int rolls = reward.weapon == weaponSt.Legendary ? 10 :
            reward.weapon == weaponSt.Epic ? 6 : reward.weapon == weaponSt.Rare ? 4 : 0;
        for (int i = 0; i < rolls; i++)
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

    /// <summary>
    /// 레거시 상자 프리팹(TreasureChest_Start)의 숨겨진 이벤트 버튼을 코드로 호출해
    /// 원래 붙어 있던 애니메이션·사운드만 재사용한다. Drop 연출이 끝나면 뚜껑과 보상을 표시하고,
    /// 상자를 연 시점부터 총 약 3초가 되면 <see cref="GrantCurrentRewardAndCloseChest"/>로 자동 지급한다.
    /// </summary>
    private IEnumerator PlayOpenSequence()
    {
        chestView.PlayDropAnimation();
        yield return new WaitForSecondsRealtime(DropAnimationSeconds);
        if (currentTile == null) yield break;
        chestView.PlayOpenAnimation();
        chestView.RevealPreparedReward();
        rewardReady = true;
        yield return new WaitForSecondsRealtime(RewardVisibleSeconds);
        GrantCurrentRewardAndCloseChest();
        openingRoutine = null;
    }

    /// <summary>
    /// 공통 골드와 카드 또는 장비 보상을 한 번만 지급한 뒤 상자 이벤트를 끝낸다.
    /// 메인 보상을 지급할 Player 컴포넌트가 없으면 골드도 먼저 지급하지 않아 부분 지급과 재시도 중복을 막는다.
    /// </summary>
    private void GrantCurrentRewardAndCloseChest()
    {
        if (!IsCurrentRewardReadyToClaim()) return;

        PlayerWallet playerWallet = BattleGameManager.Instance?.CurrentPlayerWallet;
        if (playerWallet == null)
        {
            Debug.LogError("[Chest] 공통 골드 보상 지급에 필요한 PlayerWallet이 없습니다.", currentTile);
            return;
        }

        PlayerDeck playerDeck = null;
        PlayerWeapon playerWeapon = null;
        if (currentReward.Type == BattleChestRewardType.Card)
        {
            playerDeck = GetCurrentPlayerDeck();
            if (playerDeck == null || currentReward.Card == null)
            {
                Debug.LogError("[Chest] 카드 보상 지급에 필요한 PlayerDeck 또는 CardData가 없습니다.", currentTile);
                return;
            }
        }
        else
        {
            playerWeapon = BattleGameManager.Instance?.CurrentPlayerWeapon;
            if (playerWeapon == null || currentReward.Equipment == null)
            {
                Debug.LogError("[Chest] 장비 보상 지급에 필요한 PlayerWeapon 또는 EquipData가 없습니다.", currentTile);
                return;
            }
        }

        rewardReady = false;
        AddGoldRewardToPlayer(playerWallet, currentReward.Gold);

        switch (currentReward.Type)
        {
            case BattleChestRewardType.Card:
                AddCardRewardToPlayerDeck(playerDeck, currentReward.Card);
                break;
            case BattleChestRewardType.Equipment:
                EquipRewardToPlayer(playerWeapon, currentReward.Equipment);
                break;
        }

        CompleteCurrentReward();
    }

    /// <summary>
    /// 상자를 열기 전, 보상 이미지 슬롯들을 미리 이번 보상에 맞게 채워 두되 화면에는 아직 표시하지 않는다
    /// (PlayOpenSequence가 뚜껑이 열리는 타이밍에 SetActive(true)로 실제로 보여준다).
    /// 카드/장비 보상이면 각각의 스프라이트를, 골드 보상이면 스프라이트 없이 금색 틴트만 준다.
    /// 이미지에 달려 있던 버튼은 리스너를 모두 지우고 비활성화한다 — 지급은 전부
    /// <see cref="GrantCurrentRewardAndCloseChest"/>의 타이머로만 일어나고 클릭으로 트리거되지 않는다.
    /// </summary>
    private void PrepareRewardView(PendingReward reward)
    {
        Sprite rewardSprite = reward.Type == BattleChestRewardType.Card
            ? reward.Card.myCardSprite
            : reward.Equipment.myEquipSprite;
        chestView.PrepareItemReward(rewardSprite);

    }

    /// <summary>
    /// 지금 이 보상 종류를 실제로 지급해도 되는 상태인지 검사한다. 뚜껑이 열려 보상이 준비됐고,
    /// 현재 열린 타일·보상이 있고, 요청한 타입과 실제 보상 타입이 같고, 아직 이 타일을 수령한 적이
    /// 없어야 true다. Claim* 계열 메서드가 실제 데이터를 건드리기 전에 공통으로 거치는 방어 검사다.
    /// </summary>
    private bool IsCurrentRewardReadyToClaim()
    {
        return rewardReady && currentTile != null && currentReward != null &&
               !openedTiles.Contains(currentTile);
    }

    /// <summary>카드 보상을 PlayerDeck에 추가하고 기존 저장·UI 호환 수량에는 확정 결과만 복사한다.</summary>
    private void AddCardRewardToPlayerDeck(PlayerDeck playerDeck, CardData cardReward)
    {
        int cardIndex = cardReward.index;
        playerDeck.AddOwnedCard(cardIndex, 1);
        Debug.Log($"[Chest] Card reward: {cardReward.name}", currentTile);
    }

    /// <summary>BattleGameManager에 등록된 Player 본체에서 영구 카드 데이터 컴포넌트를 가져온다.</summary>
    private static PlayerDeck GetCurrentPlayerDeck()
    {
        GameObject player = BattleGameManager.Instance != null
            ? BattleGameManager.Instance.CurrentPlayer
            : null;
        return player != null ? player.GetComponentInParent<PlayerDeck>(true) : null;
    }

    /// <summary>모든 상자에서 공통으로 주는 골드를 플레이어 재화에 더한다.</summary>
    private static void AddGoldRewardToPlayer(PlayerWallet playerWallet, int goldAmount)
    {
        playerWallet.AddGold(Mathf.Max(0, goldAmount));
    }

    /// <summary>
    /// BattleGameManager가 등록한 실제 Player의 PlayerWeapon에 장비 보상을 전달한다.
    /// 밀려난 장비는 인벤토리 대신 구매가의 50%로 현장 자동 판매한다.
    /// </summary>
    private void EquipRewardToPlayer(PlayerWeapon playerWeapon, EquipData equipment)
    {
        playerWeapon.EquipEquipmentAutomatically(
            equipment,
            out EquipData removedPrimaryEquipment,
            out EquipData removedSecondaryEquipment);
        SellRemovedEquipment(removedPrimaryEquipment);
        SellRemovedEquipment(removedSecondaryEquipment);
        Debug.Log($"[Chest] Equipment reward: {equipment.cardname} [{equipment.weapon}]", currentTile);
    }

    /// <summary>교체 과정에서 밀려난 장비 하나를 기존 판매 규칙인 구매가의 50%로 정산한다.</summary>
    private static void SellRemovedEquipment(EquipData removedEquipment)
    {
        if (removedEquipment == null) return;
        int saleGold = Mathf.Max(0, removedEquipment.cost / 2);
        BattleGameManager.Instance?.CurrentPlayerWallet?.AddGold(saleGold);
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
        ChestOpen = false;
        chestView?.Hide();
        if (chestOverlayCanvas != null) chestOverlayCanvas.gameObject.SetActive(false);
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
        ChestOpen = false;
        ReleaseModalLock();
    }

    /// <summary>
    /// 상자 오버레이 UI를 최초 1회만 런타임에 만든다.
    /// 새 ScreenSpaceOverlay Canvas 아래에 Battle 전용 상자 프리팹을 복제하고,
    /// 프리팹 루트에 Inspector로 연결된 BattleChestView 하나만 받아 사용한다.
    /// 두 번째 상자부터는 이미 만든 Canvas와 View를 그대로 재사용한다.
    /// </summary>
    private void EnsureChestOverlayViewCreated()
    {
        if (chestOverlayCanvas != null) return;
        if (chestOverlayPrefab == null)
        {
            Debug.LogError("[Chest] Battle 전용 상자 연출 프리팹 참조가 없습니다.", this);
            return;
        }

        GameObject root = new GameObject("Battle Chest Overlay", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        chestOverlayCanvas = root.GetComponent<Canvas>();
        chestOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        chestOverlayCanvas.sortingOrder = 330;
        // View가 모든 참조를 받은 뒤 Show()에서 초기화하도록 복제 전 부모 Canvas를 비활성화한다.
        root.SetActive(false);
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject chestOverlayInstance = Instantiate(chestOverlayPrefab, root.transform, false);
        chestView = chestOverlayInstance.GetComponent<BattleChestView>();
        if (chestView == null)
        {
            Debug.LogError("[Chest] 상자 연출 프리팹 루트에 BattleChestView가 없습니다.", chestOverlayInstance);
            return;
        }

        RectTransform rect = chestOverlayInstance.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
