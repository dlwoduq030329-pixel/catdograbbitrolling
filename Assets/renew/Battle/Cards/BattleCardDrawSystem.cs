using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ?íŒ¨?ì„œ ì¹´ë“œë¥?? íƒ???œê°„???•ë³´ë¥??¬ìš© ?•ì • ?œì ê¹Œì? ë³´ê??œë‹¤.
/// ?€??? íƒ ì¤??íŒ¨ê°€ ë°”ë€Œì—ˆ?”ì? ?•ì¸?????ˆë„ë¡??íŒ¨ ?„ì¹˜?€ ì¹´ë“œ ?¸ë±?¤ë? ?¨ê»˜ ?€?¥í•˜ë©?
/// ?•ì • ?„ì—??ì¹´ë“œë¥??íŒ¨?ì„œ ?œê±°?˜ê±°??MPë¥??Œë¹„?˜ì? ?ŠëŠ”??
/// </summary>
public sealed class SelectedCardUseInfo
{
    /// <summary>? íƒ ?¹ì‹œ ì¹´ë“œê°€ ?ˆë˜ ?íŒ¨ ëª©ë¡ ?„ì¹˜. ?•ì • ??ê°™ì? ?„ì¹˜?¸ì? ?¤ì‹œ ê²€?¬í•œ??</summary>
    public int HandSlotIndex { get; }
    /// <summary>?ë³¸ ì¹´ë“œ?€ ? íƒ ?¹ì‹œ ì¹´ë“œë¥??ë³„?˜ëŠ” ?¸ë±??</summary>
    public int CardIndex { get; }
    /// <summary>?€??? í˜•, ë²”ìœ„ ?•íƒœ, ?¨ê³¼ ëª©ë¡ì²˜ëŸ¼ ì¹´ë“œ ê³ ìœ  ê·œì¹™???´ê¸´ ?„íˆ¬ ?°ì´??</summary>
    public BattleCardData CardData { get; }
    /// <summary>ì¹´ë“œ ?´ë¦„, ?¬ê±°ë¦? MP ë¹„ìš©, ê¸°ë³¸ ?„ë ¥ì²˜ëŸ¼ ì¹´ë“œ ?¬ìš©??ê³µí†µ?¼ë¡œ ?„ìš”???‰ë™ ?•ë³´.</summary>
    public BattleActionRequest ActionInfo { get; }

    /// <summary>? íƒ ?œê°„???íŒ¨ ?„ì¹˜Â·ì¹´ë“œ ?°ì´?°Â·í• ???ìš© ?‰ë™ ?•ë³´ë¥??˜ë‚˜ë¡?ê³ ì •?œë‹¤.</summary>
    internal SelectedCardUseInfo(
        int handIndex,
        int cardIndex,
        BattleCardData cardData,
        BattleActionRequest actionInfo)
    {
        HandSlotIndex = handIndex;
        CardIndex = cardIndex;
        CardData = cardData;
        ActionInfo = actionInfo;
    }
}

/// <summary>
/// ?Œë ˆ?´ì–´ ?„íˆ¬ ?? ?íŒ¨, ë²„ë¦¼ ?”ë??€ ???œì‘ ?œë¡œ?°ë? ê´€ë¦¬í•œ??
/// ì¹´ë“œ UI??HandCardsChanged ?´ë²¤?¸ì? ? íƒ ì¹´ë“œ ?•ë³´ ?ì„±/?¬ìš© ?„ë£Œ APIë§??¬ìš©?œë‹¤.
/// </summary>
public class BattleCardDrawSystem : MonoBehaviour
{
    [Header("?„íˆ¬ ì¹´ë“œ ?°ì´??")]
    [InspectorName("?„íˆ¬ ì¹´ë“œ ?°ì´?°ë² ?´ìŠ¤")]
    [SerializeField] private BattleCardDatabase battleCardDatabase;
    [InspectorName("?ë³¸ ì¹´ë“œ ?°ì´?°ë² ?´ìŠ¤")]
    [SerializeField] private CardDatabase originalCardDatabase;

    [Header("?œë¡œ??ê·œì¹™")]
    [InspectorName("ìµœë? ?íŒ¨ ??")]
    [SerializeField, Min(1)] private int handLimit = 5;
    [InspectorName("?„íˆ¬ ?œì‘ ?????ê¸°")]
    [SerializeField] private bool shuffleAtBattleStart = true;

    [Header("?”ë²„ê·??¤ì •")]
    [Tooltip("?œì„±?”í•˜ë©??ë§¤ QAë¥??„í•´ ?„íˆ¬ ì¹´ë“œ ?°ì´?°ë² ?´ìŠ¤???±ë¡??ëª¨ë“  ì¹´ë“œë¥????¥ì”© ?Œì??©ë‹ˆ??")]
    [InspectorName("ëª¨ë“  ì¹´ë“œ ?Œì?")]
    [SerializeField] private bool ownAllCardsInDebugMode = true;

    // ?„ì§ ë½‘ì? ?Šì? ?¥ì°© ì¹´ë“œ ?¸ë±?? ëª©ë¡??ë§ˆì?ë§??ì†Œë¶€?????¥ì”© ?íŒ¨ë¡??´ë™?œë‹¤.
    private readonly List<int> cardsInDrawPile = new List<int>();
    // ?„ì¬ ?íŒ¨ ?¬ë¡¯ ?œì„œ?€ ?™ì¼??ì¹´ë“œ ?¸ë±??ëª©ë¡. -1?€ ?¬ìš©?˜ì–´ ë¹„ì–´ ?ˆëŠ” ?¬ë¡¯?´ë‹¤.
    private readonly List<int> cardsInHand = new List<int>();
    // cardsInHand?€ ê°™ì? ?„ì¹˜ë¥??¬ìš©?˜ëŠ” ë³‘ë ¬ ëª©ë¡. ê°??íŒ¨ ?¬ë¡¯??MP ? ì¸?‰ì„ ?€?¥í•œ??
    private readonly List<int> mpDiscountForEachHandSlot = new List<int>();
    // ?¬ìš©?ˆê±°???´ì´ ?ë‚  ???¨ì? ?¥ì°© ì¹´ë“œ. ?œë¡œ???”ë?ê°€ ë¹„ë©´ ?¤ì‹œ ?ì—¬ ?´ë™?œë‹¤.
    private readonly List<int> cardsInDiscardPile = new List<int>();
    // ë²„ì„¯ ?¨ê³¼ì²˜ëŸ¼ ?¥ì°© ??ë°–ì—???ì„±??ì¹´ë“œ???íŒ¨ ?„ì¹˜. ë²„ë¦´ ???±ìœ¼ë¡??˜ëŒ?„ê?ì§€ ?Šê²Œ êµ¬ë¶„?œë‹¤.
    private readonly HashSet<int> temporaryCardSlotsInHand = new HashSet<int>();
    // PlayerDeck???¥ì°© ì¹´ë“œê°€ ?„íˆ¬ ?œë¡œ???”ë?ë¡?ë³µì‚¬?ëŠ”ì§€ ?˜í??¸ë‹¤.
    private bool isBattleDeckInitialized;

    /// <summary>?ìœ¼ë¡?ë½‘ì„ ì¹´ë“œê°€ ?¤ì–´ ?ˆëŠ” ?œë¡œ???”ë?.</summary>
    public IReadOnlyList<int> DrawPileCards => cardsInDrawPile;
    /// <summary>?„ì¬ ?”ë©´???œì‹œ?˜ëŠ” ?íŒ¨ ì¹´ë“œ.</summary>
    public IReadOnlyList<int> HandCards => cardsInHand;
    /// <summary>?¬ìš©??ë§ˆì¹˜ê³??¤ìŒ ?¬ì„ê¸°ë? ê¸°ë‹¤ë¦¬ëŠ” ì¹´ë“œ.</summary>
    public IReadOnlyList<int> DiscardPileCards => cardsInDiscardPile;
    /// <summary>?„íˆ¬ ì¤??™ì‹œ??? ì??????ˆëŠ” ìµœë? ?íŒ¨ ??</summary>
    public int HandLimit => handLimit;
    /// <summary>?íŒ¨ UIê°€ ?¨ê³¼Â·?€?Â·ë²”??ê°™ì? Renew ?„íˆ¬ ì¹´ë“œ ?•ë³´ë¥??½ì„ ???¬ìš©?œë‹¤.</summary>
    public BattleCardDatabase Database => battleCardDatabase;
    /// <summary>?íŒ¨ UIê°€ ê¸°ì¡´ ì¹´ë“œ ?´ë¦„Â·?´ë?ì§€ ?•ë³´ë¥?ì°¾ì„ ???¬ìš©?˜ëŠ” ?ë³¸ ?°ì´?°ë² ?´ìŠ¤.</summary>
    public CardDatabase OriginalDatabase => originalCardDatabase;
    /// <summary>ì§€?•í•œ ?íŒ¨ ?¬ë¡¯???±ì—??ë½‘ì? ì¹´ë“œê°€ ?„ë‹ˆ???¨ê³¼ë¡??„ì‹œ ?ì„±??ì¹´ë“œ?¸ì? ?•ì¸?œë‹¤.</summary>
    public bool IsTemporaryCardSlot(int handSlotIndex) => temporaryCardSlotsInHand.Contains(handSlotIndex);

    /// <summary>?œë¡œ?°Â·ì‚¬?©Â·ë²„ë¦¬ê¸° ?±ìœ¼ë¡??„ì¬ ?íŒ¨ êµ¬ì„±???¬ë¼ì¡Œì„ ?????íŒ¨ë¥??„ë‹¬?œë‹¤.</summary>
    public event Action<IReadOnlyList<int>> HandCardsChanged;
    /// <summary>ì¹´ë“œ ???¥ì´ ?íŒ¨???¤ì–´??ì§í›„ ?´ë‹¹ ì¹´ë“œ ?¸ë±?¤ë? ?„ë‹¬?œë‹¤. ê°œë³„ ?œë¡œ???°ì¶œ ?°ê²°?©ì´??</summary>
    public event Action<int> CardDrawn;

    /// <summary>ì»´í¬?ŒíŠ¸ ?œì„±?????Œë ˆ?´ì–´ ???œì‘ ?´ë²¤??êµ¬ë…???œë„?œë‹¤.</summary>
    private void OnEnable()
    {
        TrySubscribeTurnManager();
    }

    /// <summary>ê²Œì„ ê´€ë¦¬ì ì´ˆê¸°???œì„œê°€ ??? ê²½ìš°ë¥??€ë¹„í•´ ?œì‘ ??êµ¬ë…???¤ì‹œ ?•ì¸?œë‹¤.</summary>
    private void Start()
    {
        // BattleGameManagerë³´ë‹¤ ë¨¼ì? ?œì„±?”ëœ ê²½ìš°ë¥?ë³´ì™„?œë‹¤.
        TrySubscribeTurnManager();
    }

    /// <summary>ë¹„í™œ?±í™” ?????´ë²¤??êµ¬ë…???´ì œ???œë¡œ?°ê? ì¤‘ë³µ ?¤í–‰?˜ì? ?Šê²Œ ?œë‹¤.</summary>
    private void OnDisable()
    {
        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.PlayerTurnStarted -= RefreshHandForNewPlayerTurn;
        }
    }

    /// <summary>
    /// ?Œë ˆ?´ì–´ ?±ë¡ ê³¼ì •?ì„œ ?„ë‹¬ë°›ì? ?¤ì œ PlayerDeck???¬ìš©???„íˆ¬ ?±ê³¼ ?”ë²„ê·?ë³´ìœ ?‰ì„ ì´ˆê¸°?”í•œ??
    /// ??ê²€?‰ìœ¼ë¡?PlayerDeck???€??ì°¾ì? ?Šìœ¼ë¯€ë¡??¸ì¶œ?ëŠ” ?±ë¡???±ì„ ë°˜ë“œ??ëª…ì‹œ?ìœ¼ë¡??„ë‹¬?´ì•¼ ?œë‹¤.
    /// </summary>
    public void InitializeBattleCardCycle(PlayerDeck registeredPlayerDeck)
    {
        if (registeredPlayerDeck == null)
        {
            Debug.LogError(
                "?„íˆ¬ ??ì´ˆê¸°???¤íŒ¨: BattlePlayerRegistrationService?ì„œ ?±ë¡??PlayerDeck???„ë‹¬?˜ì? ?Šì•˜?µë‹ˆ??",
                this);
            return;
        }

        // ?´ì „ ?„íˆ¬???¬ì´ˆê¸°í™” ?œì ??ì¹´ë“œ ?„ì¹˜ ?•ë³´ê°€ ?ì´ì§€ ?Šë„ë¡?ëª¨ë“  ?°í????íƒœë¥?ë¹„ìš´??
        cardsInDrawPile.Clear();
        cardsInHand.Clear();
        mpDiscountForEachHandSlot.Clear();
        cardsInDiscardPile.Clear();
        temporaryCardSlotsInHand.Clear();

        // ?´í›„ ë¡œì§?€ ??ê²€???†ì´ ?Œë ˆ?´ì–´ ?±ë¡ ?œë¹„?¤ê? ?„ë‹¬???™ì¼??PlayerDeckë§??¬ìš©?œë‹¤.
        PlayerDeck sourceDeck = registeredPlayerDeck;
        if (ownAllCardsInDebugMode)
        {
            // ?ë§¤ QA??ë³´ìœ ?‰ë§Œ ?˜ë¦¬ë©??¤ì œ ?œë¡œ???€?ì? ?„ë˜ ?¥ì°© ??ë³µì‚¬ ?¨ê³„?ì„œ ê²°ì •?œë‹¤.
            GrantEveryCardForShopTesting(sourceDeck);
        }
        // PlayerDeck???¥ì°© ?¬ë¡¯ë§??„íˆ¬ ?œë¡œ???”ë?ë¡?ë³µì‚¬?œë‹¤.
        CopyEquippedCardsIntoDrawPile(sourceDeck);

        if (cardsInDrawPile.Count == 0)
        {
            Debug.LogError(
                "?„íˆ¬ ??ì´ˆê¸°???¤íŒ¨: PlayerDeck.deckCardforUI??? íš¨???¥ì°© ì¹´ë“œê°€ ?†ìŠµ?ˆë‹¤. " +
                "ë³´ìœ  ì¹´ë“œ ?„ì²´ë¥??„ì˜ ?œë¡œ???±ìœ¼ë¡??¬ìš©?˜ì? ?ŠìŠµ?ˆë‹¤.",
                this);
        }

        if (shuffleAtBattleStart)
        {
            // ì²??íŒ¨ê°€ ?¥ì°© ?¬ë¡¯ ?œì„œ?€ë¡?ê³ ì •?˜ì? ?Šë„ë¡??„íˆ¬ ?œì‘ ????ë²??ëŠ”??
            Shuffle(cardsInDrawPile);
        }

        // ???œì ë¶€?????œì‘ ?´ë²¤?¸ëŠ” ?¬ì´ˆê¸°í™” ?€???¨ì? ?íŒ¨ ë²„ë¦¬ê¸°ì? ?¬ë“œë¡œìš°ë¥??˜í–‰?œë‹¤.
        isBattleDeckInitialized = true;
        DrawCardsUntilHandIsFull();
    }

    /// <summary>
    /// ?”ë²„ê·??„íˆ¬?ì„œ ?ë§¤ QAë¥??„í•´ ëª¨ë“  ?„íˆ¬ ì¹´ë“œ??ë³´ìœ  ?˜ëŸ‰ë§?2?¥ìœ¼ë¡??¤ì •?©ë‹ˆ??
    /// ?¤ì œ ?íŒ¨ ?œë¡œ???±ì? ??ëª©ë¡???„ë‹ˆ??PlayerDeck.deckCardforUI 10ì¹¸ë§Œ ?¬ìš©?©ë‹ˆ??
    /// ?¤ì œ ?€???°ì´?°ëŠ” ë³€ê²½í•˜ì§€ ?Šê³  ?„ì¬ ?¤í–‰ ì¤‘ì¸ ë©”ëª¨ë¦?ê°’ë§Œ ë³€ê²½í•©?ˆë‹¤.
    /// </summary>
    private void GrantEveryCardForShopTesting(PlayerDeck sourceDeck)
    {
        if (battleCardDatabase == null)
        {
            Debug.LogWarning("?”ë²„ê·?ì¹´ë“œ ì§€ê¸??¤íŒ¨: ?„íˆ¬ ì¹´ë“œ ?°ì´?°ë² ?´ìŠ¤ê°€ ?°ê²°?˜ì? ?Šì•˜?µë‹ˆ??", this);
            return;
        }

        foreach (BattleCardData card in battleCardDatabase.Cards)
        {
            if (card == null || card.legacyCardIndex < 0 ||
                BattleCardConnector.FindOriginalCard(card.legacyCardIndex, originalCardDatabase) == null)
            {
                continue;
            }

            int cardIndex = card.legacyCardIndex;
            if (sourceDeck != null)
            {
                sourceDeck.AddOwnedCard(cardIndex, PlayerDeck.MaximumOwnedCopiesPerCard);
                DataConfig.CardsCount[cardIndex] = sourceDeck.GetOwnedCardCount(cardIndex);
            }
            else
            {
                DataConfig.CardsCount[cardIndex] = PlayerDeck.MaximumOwnedCopiesPerCard;
            }
        }

        Debug.Log("?”ë²„ê·?ì¹´ë“œ ë³´ìœ ??ì§€ê¸??„ë£Œ: ? íš¨ ì¹´ë“œë³?2??", this);
    }

    /// <summary>PlayerDeck???¥ì°© 10ì¹¸ì—??? íš¨??ì¹´ë“œë§??œì„œ?€ë¡??„íˆ¬ ?œë¡œ???”ë???ë³µì‚¬?œë‹¤.</summary>
    private void CopyEquippedCardsIntoDrawPile(PlayerDeck sourceDeck)
    {
        if (sourceDeck == null || sourceDeck.EquippedCards == null) return;
        foreach (int cardIndex in sourceDeck.EquippedCards)
        {
            if (cardIndex < 0 ||
                BattleCardConnector.FindOriginalCard(cardIndex, originalCardDatabase) == null ||
                battleCardDatabase == null ||
                battleCardDatabase.FindByLegacyCardIndex(cardIndex) == null)
            {
                continue;
            }
            cardsInDrawPile.Add(cardIndex);
        }
        Debug.Log($"?¥ì°© ??ë³µì‚¬ ?„ë£Œ: deckCardforUI ê¸°ì? {cardsInDrawPile.Count}??", this);
    }

    /// <summary>
    /// ?„ì¬ ?íŒ¨ê°€ ìµœë? ?íŒ¨ ?˜ê? ???Œê¹Œì§€ ???¥ì”© ?œë¡œ?°í•œ ???„ì„±???íŒ¨ë¥?UI????ë²??„ë‹¬?œë‹¤.
    /// ê°œë³„ ì¹´ë“œê°€ ?¤ì–´?¤ëŠ” ?œê°„?€ TryDrawOneCard ?´ë???CardDrawn ?´ë²¤?¸ê? ë³„ë„ë¡??Œë¦°??
    /// </summary>
    public void DrawCardsUntilHandIsFull()
    {
        while (cardsInHand.Count < handLimit)
        {
            // ?œë¡œ???”ë??€ ?¬í™œ?©í•  ë²„ë¦¼ ?”ë?ê°€ ëª¨ë‘ ë¹„ì—ˆ?¤ë©´ ??ì±„ìš¸ ???†ìœ¼ë¯€ë¡?ë°˜ë³µ???ë‚¸??
            if (!TryDrawOneCard())
            {
                break;
            }
        }

        // ì¹´ë“œë³??´ë²¤?¸ì? ë³„ê°œë¡?ìµœì¢… ?íŒ¨ ëª©ë¡?€ ?œë¡œ?°ê? ëª¨ë‘ ?ë‚œ ????ë²ˆë§Œ ê°±ì‹ ?œë‹¤.
        HandCardsChanged?.Invoke(cardsInHand);
    }

    /// <summary>
    /// ?Œë ˆ?´ì–´ê°€ ?„ë¥¸ ?íŒ¨ ?¬ë¡¯?ì„œ ì¹´ë“œ ?¬ìš©???„ìš”???•ë³´ë¥??½ì–´ ?˜ë‚˜ë¡?ë¬¶ëŠ”??
    /// ì¹´ë“œ ?°ì´??ì¡°íšŒ?€ ?íŒ¨ ?„ìš© MP ? ì¸ ê³„ì‚°ê¹Œì?ë§??˜í–‰?˜ë©°,
    /// ì¹´ë“œë¥??íŒ¨?ì„œ ?œê±°?˜ê±°??MPë¥?ì°¨ê°?˜ì? ?ŠëŠ”?? ?¤ì œ ?Œë¹„???¬ìš© ?•ì • ??ë³„ë„ ?¨ìˆ˜ê°€ ?´ë‹¹?œë‹¤.
    /// </summary>
    /// <param name="handSlotIndex">?Œë ˆ?´ì–´ê°€ ? íƒ???íŒ¨ ëª©ë¡???„ì¹˜.</param>
    /// <param name="selectedCardInfo">?±ê³µ ???¬ë¡¯Â·ì¹´ë“œ ë²ˆí˜¸Â·ì¹´ë“œ ê·œì¹™Â·? ì¸ ?ìš© ?‰ë™ ?•ë³´ë¥?ë°˜í™˜?œë‹¤.</param>
    /// <returns>ì¹´ë“œ ?¬ìš© ?•ë³´ë¥??•ìƒ?ìœ¼ë¡?ë§Œë“¤?ˆìœ¼ë©?true, ?¬ë¡¯?´ë‚˜ ì¹´ë“œ ?°ì´?°ê? ?˜ëª»?ìœ¼ë©?false.</returns>
    public bool TryGetCardUseInfoFromHandSlot(int handSlotIndex, out SelectedCardUseInfo selectedCardInfo)
    {
        // ?¤íŒ¨???¸ì¶œ?ê? ?´ì „ ê°’ì„ ?¤ìˆ˜ë¡??¬ì‚¬?©í•˜ì§€ ?Šë„ë¡?ì¶œë ¥ê°’ì„ ë¨¼ì? ë¹„ìš´??
        selectedCardInfo = null;
        // ?íŒ¨ ëª©ë¡ ë°”ê¹¥???„ë¥¸ ?…ë ¥?€ ì¹´ë“œ ? íƒ?¼ë¡œ ì²˜ë¦¬?????†ë‹¤.
        if (handSlotIndex < 0 || handSlotIndex >= cardsInHand.Count)
        {
            return false;
        }

        // ?íŒ¨ ?¬ë¡¯?ëŠ” ì¹´ë“œ ê°ì²´ ?€???ë³¸ ì¹´ë“œë¥?ì°¾ê¸° ?„í•œ ?•ìˆ˜ ?¸ë±?¤ê? ?€?¥ë˜???ˆë‹¤.
        int selectedCardIndex = cardsInHand[handSlotIndex];
        // -1?€ ?´ë? ?¬ìš©?˜ì–´ ë¹„ì–´ ?ˆëŠ” ?¬ë¡¯?´ë?ë¡??¤ì‹œ ? íƒ?????†ë‹¤.
        if (selectedCardIndex < 0)
        {
            return false;
        }
        // ì¹´ë“œ ë²ˆí˜¸ë¥??€?Â·ë²”?„Â·íš¨ê³?ëª©ë¡???´ê¸´ Renew ?„íˆ¬ ì¹´ë“œ ?°ì´?°ë¡œ ì°¾ëŠ”??
        BattleCardData cardData = battleCardDatabase != null
            ? battleCardDatabase.FindByLegacyCardIndex(selectedCardIndex)
            : null;

        // ê¸°ì¡´ ì¹´ë“œ ?´ë¦„/?˜ì¹˜?€ Renew ì¹´ë“œ ê·œì¹™???€??? íƒ ?œìŠ¤?œì´ ê³µí†µ?¼ë¡œ ?½ëŠ” ?‰ë™ ?•ë³´ë¡?ë³€?˜í•œ??
        if (!BattleCardConnector.TryCreateActionRequest(
                cardData,
                originalCardDatabase,
                out BattleActionRequest actionInfo))
        {
            Debug.LogError($"ì¹´ë“œ ?‰ë™ ?•ë³´ ?ì„± ?¤íŒ¨: ?„íˆ¬ ì¹´ë“œ ?¸ë±??{selectedCardIndex}", this);
            return false;
        }

        // ? ì¸ ëª©ë¡?€ ?íŒ¨?€ ê°™ì? ?„ì¹˜ë¥??¬ìš©?œë‹¤. ?¼ë°˜ ì¹´ë“œ??0, ?„ì¬ ?„ì‹œ ë²„ì„¯ ì¹´ë“œ??1?´ë‹¤.
        int handSlotMpDiscount = handSlotIndex < mpDiscountForEachHandSlot.Count
            ? mpDiscountForEachHandSlot[handSlotIndex]
            : 0;
        if (handSlotMpDiscount > 0)
        {
            // BattleActionRequest???½ê¸° ?„ìš©?´ë?ë¡??´ë²ˆ ?íŒ¨ ì¹´ë“œ?ë§Œ ? ì¸?????‰ë™ ?•ë³´ë¥?ë§Œë“ ??
            actionInfo = new BattleActionRequest(
                actionInfo.DisplayName,
                actionInfo.ActionType,
                actionInfo.RangeTiles,
                Mathf.Max(0, actionInfo.MPCost - handSlotMpDiscount),
                actionInfo.Power);
        }

        // ?•ì • ??ê°™ì? ì¹´ë“œ?¸ì? ?¬ê??¬í•  ???ˆë„ë¡??¬ë¡¯ ?„ì¹˜?€ ì¹´ë“œ ë²ˆí˜¸???¨ê»˜ ê³ ì •?œë‹¤.
        selectedCardInfo = new SelectedCardUseInfo(
            handSlotIndex,
            selectedCardIndex,
            cardData,
            actionInfo);
        return true;
    }

    /// <summary>ì¹´ë“œ ?‰ë™ ?•ì • ???´ë‹¹ ì¹´ë“œ ???¥ì„ ?íŒ¨?ì„œ ?œê±°?˜ê³  ë²„ë¦¼ ?”ë?ë¡?ë³´ë‚¸??</summary>
    public bool TryMoveUsedCardToDiscardPile(SelectedCardUseInfo selectedCardInfo)
    {
        // ? íƒ ?•ë³´ê°€ ?†ìœ¼ë©??´ë–¤ ?íŒ¨ ì¹´ë“œë¥??Œë¹„?´ì•¼ ?˜ëŠ”ì§€ ?ë‹¨?????†ë‹¤.
        if (selectedCardInfo == null)
        {
            return false;
        }

        int handSlotIndex = selectedCardInfo.HandSlotIndex;
        // ?€??? íƒ ?„ì¤‘ ?íŒ¨ê°€ ë°”ë€Œì—ˆ?????ˆìœ¼ë¯€ë¡?? íƒ ?¹ì‹œ ?¬ë¡¯ê³?ì¹´ë“œ ë²ˆí˜¸ë¥?ëª¨ë‘ ?¤ì‹œ ?•ì¸?œë‹¤.
        if (handSlotIndex < 0 ||
            handSlotIndex >= cardsInHand.Count ||
            cardsInHand[handSlotIndex] != selectedCardInfo.CardIndex)
        {
            return false;
        }

        // RemoveAt?¼ë¡œ ì¹´ë“œë¥??¹ê¸°ì§€ ?Šê³  -1ë¡?ë¹„ì›Œ ?ë©´ ?¨ì? ì¹´ë“œ ?„ì¹˜ê°€ ë°”ë€Œì? ?Šìœ¼ë©?
        // UI???¤ìŒ ?Œë ˆ?´ì–´ ???„ê¹Œì§€ ?¬ìš©??ì¹´ë“œ ?ë¦¬ë¥??´ë‘¡ê²?? ì??????ˆë‹¤.
        // ?½ì´ˆ ë²„ì„¯ ?±ìœ¼ë¡??ì„±??ë³´ë„ˆ??ì¹´ë“œ???¥ì°© ???Œì†???„ë‹ˆë¯€ë¡? ë²„ë¦¼ ?”ë?ë¡?ë³´ë‚´ë©?
        // MoveDiscardPileBackToDrawPile()???µí•´ ?´í›„ ?´ì—??ê³„ì† ?œë¡œ???±ì„ ?Œë©° ?˜ì˜¤ê²??œë‹¤.
        // ?¥ì°© ??ì¹´ë“œë§?ê³„ì† ?œí™˜?˜ë„ë¡?ë³´ë„ˆ??ì¹´ë“œ???¬ìš© ???„ì „???Œë©¸?œí‚¨??
        bool wasTemporaryCard = temporaryCardSlotsInHand.Contains(handSlotIndex);
        cardsInHand[handSlotIndex] = -1;
        temporaryCardSlotsInHand.Remove(handSlotIndex);
        if (handSlotIndex < mpDiscountForEachHandSlot.Count)
        {
            mpDiscountForEachHandSlot[handSlotIndex] = 0;
        }
        if (!wasTemporaryCard)
        {
            // ?¥ì°© ?±ì—??ë½‘ì? ?¼ë°˜ ì¹´ë“œë§?ë²„ë¦¼ ?”ë?ë¡?ë³´ë‚´ ?´í›„ ???¬ìˆœ?˜ì— ?¬í•¨?œë‹¤.
            cardsInDiscardPile.Add(selectedCardInfo.CardIndex);
        }
        // ?„ì‹œ ?ì„± ì¹´ë“œ??ë²„ë¦¼ ?”ë????¤ì–´ê°€ì§€ ?Šì•˜?¼ë?ë¡????œì ???„ì „???Œë©¸?œë‹¤.
        HandCardsChanged?.Invoke(cardsInHand);
        return true;
    }

    /// <summary>
    /// ì¹´ë“œ ?ë§¤ë¡?PlayerDeck???¥ì°© ?¬ë¡¯???ë™ ?´ì œ???? ?„íˆ¬???¨ì? ê°™ì? ì¹´ë“œ ?˜ë? ?¤ì œ ?¥ì°© ?˜ì— ë§ì¶˜??
    /// ?œë¡œ???”ë? ??ë²„ë¦¼ ?”ë? ???íŒ¨ ?œì„œë¡?ì´ˆê³¼ ì¹´ë“œë§??œê±°?˜ë©° ?„ì‹œ ?ì„± ì¹´ë“œ???¥ì°© ???˜ì— ?¬í•¨?˜ì? ?ŠëŠ”??
    /// ?ë§¤???¥ì°© ?˜ë? ?˜ë¦¬ì§€ ?Šìœ¼ë¯€ë¡????¨ìˆ˜??ë¶€ì¡±í•œ ì¹´ë“œë¥??ˆë¡œ ì¶”ê??˜ì? ?ŠëŠ”??
    /// </summary>
    public void RemoveRuntimeCardCopiesAboveEquippedCount(int cardIndex, int equippedCardCount)
    {
        // ë³´ìœ ?‰ì´ ?„ë‹ˆ??PlayerDeck.EquippedCards???¤ì œë¡??¨ì? ?¥ìˆ˜ë¥?ê¸°ì??¼ë¡œ ?œê±°?‰ì„ ê³„ì‚°?œë‹¤.
        int copiesToRemove = CountEquippedCardCopiesInBattle(cardIndex) - Mathf.Max(0, equippedCardCount);
        // ?„ì§ ?”ë©´???˜ì˜¤ì§€ ?Šì? ì¹´ë“œë¶€???œê±°???„ì¬ ?íŒ¨ ë³€?”ê? ìµœì†Œ?”ë˜?„ë¡ ?œë‹¤.
        while (copiesToRemove > 0 && cardsInDrawPile.Remove(cardIndex)) copiesToRemove--;
        // ?œë¡œ???”ë?ë§Œìœ¼ë¡?ë¶€ì¡±í•˜ë©??´ë? ?¬ìš©??ì¹´ë“œê°€ ?ˆëŠ” ë²„ë¦¼ ?”ë??ì„œ ?œê±°?œë‹¤.
        while (copiesToRemove > 0 && cardsInDiscardPile.Remove(cardIndex)) copiesToRemove--;

        bool handChanged = false;
        // ?œë¡œ?°Â·ë²„ë¦??”ë??ì„œ ëª¨ë‘ ?œê±°?˜ê³ ??ì´ˆê³¼ë¶„ì´ ?¨ì•˜?¤ë©´ ?„ì¬ ?”ë©´???íŒ¨?ì„œ???œê±°?´ì•¼ ?œë‹¤.
        // ë§ˆì?ë§??¬ë¡¯ë¶€????ˆœ?¼ë¡œ ê²€?¬í•˜ë©??ìª½??ë°°ì¹˜??ì¹´ë“œ ?„ì¹˜ë¥?ìµœë???? ì??????ˆë‹¤.
        for (int handSlotIndex = cardsInHand.Count - 1;
             handSlotIndex >= 0 && copiesToRemove > 0;
             handSlotIndex--)
        {
            // ?ë§¤??ì¹´ë“œ ë²ˆí˜¸?€ ?¤ë¥¸ ?¬ë¡¯?€ ?œê±° ?€?ì´ ?„ë‹ˆ??
            // ê°™ì? ì¹´ë“œ ë²ˆí˜¸?¼ë„ ë²„ì„¯ ?¨ê³¼ ?±ìœ¼ë¡??„ì‹œ ?ì„±??ì¹´ë“œ??PlayerDeck ?¥ì°© ì¹´ë“œê°€ ?„ë‹ˆë¯€ë¡?ê±´ë„ˆ?´ë‹¤.
            if (cardsInHand[handSlotIndex] != cardIndex ||
                temporaryCardSlotsInHand.Contains(handSlotIndex))
            {
                continue;
            }

            // RemoveAt???¬ìš©?˜ë©´ ??ì¹´ë“œ?¤ì˜ ?¬ë¡¯ ë²ˆí˜¸ê°€ ?„ë? ë°”ë€Œë?ë¡? ?„ì¬ ?¬ë¡¯ë§?-1ë¡??œì‹œ??ë¹„ìš´??
            cardsInHand[handSlotIndex] = -1;
            // ì¹´ë“œê°€ ?¬ë¼ì§??¬ë¡¯??MP ? ì¸ë§??¨ì? ?Šë„ë¡?ê°™ì? ?„ì¹˜??? ì¸ê°’ë„ ?¨ê»˜ ì´ˆê¸°?”í•œ??
            if (handSlotIndex < mpDiscountForEachHandSlot.Count)
            {
                mpDiscountForEachHandSlot[handSlotIndex] = 0;
            }
            // ?¤ì œ ?íŒ¨ê°€ ë°”ë€Œì—ˆ?Œì„ ê¸°ë¡??ë°˜ë³µ???ë‚œ ??UI ê°±ì‹  ?´ë²¤?¸ë? ??ë²ˆë§Œ ?¸ì¶œ?œë‹¤.
            handChanged = true;
            // ?¥ì°© ?˜ë³´??ë§ì•˜??ì¹´ë“œ ???¥ì„ ?œê±°?ˆìœ¼ë¯€ë¡??¨ì? ?œê±° ëª©í‘œë¥?????ì¤„ì¸??
            copiesToRemove--;
        }
        // ?íŒ¨?ì„œ ?¤ì œ ?œê±°ê°€ ?ˆì—ˆ???Œë§Œ ìµœì¢… ?íŒ¨ ëª©ë¡??UI???„ë‹¬?œë‹¤.
        // ?œë¡œ?°Â·ë²„ë¦??”ë?ë§?ë°”ë€?ê²½ìš° ?”ë©´???íŒ¨??ê°™ìœ¼ë¯€ë¡?ë¶ˆí•„?”í•œ UI ê°±ì‹ ???˜ì? ?ŠëŠ”??
        if (handChanged)
        {
            HandCardsChanged?.Invoke(cardsInHand);
        }
    }

    /// <summary>
    /// ?„íˆ¬???œë¡œ?°Â·ë²„ë¦??”ë??€ ?íŒ¨???¨ì? ?¹ì • ?¥ì°© ì¹´ë“œ ?˜ë? ?©ì‚°?œë‹¤.
    /// ì¹´ë“œ ?¨ê³¼ë¡??ì„±???„ì‹œ ?íŒ¨ ì¹´ë“œ??PlayerDeck ?¥ì°© ???Œì†???„ë‹ˆë¯€ë¡?ê³„ì‚°?ì„œ ?œì™¸?œë‹¤.
    /// </summary>
    private int CountEquippedCardCopiesInBattle(int cardIndex)
    {
        int battleEquippedCardCount = 0;

        // ?„ì§ ë½‘ì? ?Šì? ?œë¡œ???”ë??ì„œ ê°™ì? ì¹´ë“œ ë²ˆí˜¸ê°€ ëª????¨ì•„ ?ˆëŠ”ì§€ ?¼ë‹¤.
        // ê°™ì? ì¹´ë“œ??ìµœë? ë³´ìœ  ?˜ëŸ‰ë§Œí¼ ì¤‘ë³µ?????ˆìœ¼ë¯€ë¡?ë°œê²¬???Œë§ˆ?????¥ì”© ?”í•œ??
        foreach (int drawPileCardIndex in cardsInDrawPile)
        {
            if (drawPileCardIndex == cardIndex)
            {
                battleEquippedCardCount++;
            }
        }

        // ?´ë? ?¬ìš©?ˆì?ë§??±ì´ ë¹„ë©´ ?¤ì‹œ ?ì¼ ë²„ë¦¼ ?”ë??ì„œ??ê°™ì? ì¹´ë“œ ë²ˆí˜¸ë¥??¼ë‹¤.
        foreach (int discardPileCardIndex in cardsInDiscardPile)
        {
            if (discardPileCardIndex == cardIndex)
            {
                battleEquippedCardCount++;
            }
        }

        // ?„ì¬ ?íŒ¨??ì¹´ë“œ ?¨ê³¼ë¡??ì„±???„ì‹œ ì¹´ë“œê°€ ?ì¼ ???ˆìœ¼ë¯€ë¡??¬ë¡¯ ?„ì¹˜ê¹Œì? ?¨ê»˜ ê²€?¬í•œ??
        for (int handSlotIndex = 0; handSlotIndex < cardsInHand.Count; handSlotIndex++)
        {
            // ?„ì‹œ ?ì„± ?¬ë¡¯?€ PlayerDeck ?¥ì°© ???Œì†???„ë‹ˆë¯€ë¡??ë§¤ ???¥ì°© ??ë¹„êµ?ì„œ ?œì™¸?œë‹¤.
            // ?¼ë°˜ ?íŒ¨ ?¬ë¡¯?´ë©´??ì¹´ë“œ ë²ˆí˜¸ê¹Œì? ê°™ì„ ?Œë§Œ ?¥ì°© ì¹´ë“œ ???¥ìœ¼ë¡?ê³„ì‚°?œë‹¤.
            if (!temporaryCardSlotsInHand.Contains(handSlotIndex) &&
                cardsInHand[handSlotIndex] == cardIndex)
            {
                battleEquippedCardCount++;
            }
        }

        // ??ì¹´ë“œ ?„ì¹˜???©ì–´ì§??™ì¼ ?¥ì°© ì¹´ë“œ???„ì²´ ?¥ìˆ˜ë¥??¸ì¶œ?ì—ê²?ë°˜í™˜?œë‹¤.
        return battleEquippedCardCount;
    }

    /// <summary>
    /// ?¬ìš©??ë²„ì„¯ ì¹´ë“œ???íŒ¨ ?ë¦¬??ë¬´ì‘???„ì‹œ ì¹´ë“œë¥??ì„±?˜ê³  ?´ë²ˆ ?´ì—ë§?MP ë¹„ìš©??1 ??¶˜??
    /// ?„ë³´ ì¹´ë“œë¥?ê¸°ì¡´ ?°ì´?°ì˜ ?¬ê???ë¬¸ì?´ë³„ë¡?ë¬¶ì? ???¬ê????˜ë‚˜, ?´ë‹¹ ?¬ê??„ì˜ ì¹´ë“œ ?˜ë‚˜ë¥?ì°¨ë?ë¡?ë¬´ì‘??? íƒ?œë‹¤.
    /// ?ì„± ì¹´ë“œ??PlayerDeck ?¥ì°© ?±ì— ì¶”ê??˜ì? ?Šìœ¼ë©??¬ìš©?˜ê±°???´ì´ ?ë‚˜ë©??„ì „???Œë©¸?œë‹¤.
    /// </summary>
    public bool GenerateWeirdMushroomCard(SelectedCardUseInfo usedMushroomCardInfo)
    {
        // ?¬ìš©??ë²„ì„¯ ?•ë³´ê°€ ?†ìœ¼ë©??ê¸° ?ì‹ ???„ë³´?ì„œ ?œì™¸?˜ê±°???ì„± ?„ì¹˜ë¥?ê²°ì •?????†ë‹¤.
        // ?„íˆ¬ ì¹´ë“œ DBê°€ ?†ìœ¼ë©?ë¬´ì‘???ì„± ?„ë³´ ?ì²´ë¥?ë§Œë“¤ ???†ë‹¤.
        if (usedMushroomCardInfo == null || battleCardDatabase == null)
        {
            return false;
        }

        // Key??ê¸°ì¡´ ì¹´ë“œ ?°ì´?°ì— ?€?¥ëœ ?¬ê???ë¬¸ì?´ì´ê³?Value??ê·??¬ê??„ì— ?í•œ ì¹´ë“œ ?¸ë±??ëª©ë¡?´ë‹¤.
        // ?? "common" -> [0, 2, 5], "rare" -> [1, 7]
        Dictionary<string, List<int>> cardIndexesGroupedByRarity =
            new Dictionary<string, List<int>>();

        // ?„íˆ¬ ì¹´ë“œ DB ?„ì²´ë¥??œíšŒ?˜ë©´???ì„± ê°€?¥í•œ ì¹´ë“œë¥??¬ê??„ë³„ ëª©ë¡?¼ë¡œ ë¶„ë¥˜?œë‹¤.
        foreach (BattleCardData card in battleCardDatabase.Cards)
        {
            // ?¬ê??„ëŠ” Renew BattleCardDataê°€ ?„ë‹ˆ??ê¸°ì¡´ CardData??ë¬¸ì?´ë¡œ ?€?¥ë˜???ˆì–´ ?ë³¸???¨ê»˜ ì°¾ëŠ”??
            CardData original = card != null
                ? BattleCardConnector.FindOriginalCard(card.legacyCardIndex, originalCardDatabase)
                : null;

            // ë¹„ì–´ ?ˆê±°??? íš¨???¸ë±?¤ê? ?†ëŠ” ì¹´ë“œ, ë°©ê¸ˆ ?¬ìš©??ë²„ì„¯ ?ê¸° ?ì‹ ,
            // ê¸°ì¡´ ?°ì´?°ê? ?†ì–´ ?¬ê??„ë? ?????†ëŠ” ì¹´ë“œ???ì„± ?„ë³´?ì„œ ?œì™¸?œë‹¤.
            if (card == null || card.legacyCardIndex < 0 ||
                card.legacyCardIndex == usedMushroomCardInfo.CardIndex ||
                original == null)
            {
                continue;
            }

            // ê¸°ì¡´ rare ê°’ì´ nullÂ·ë¹?ë¬¸ì?´Â·ê³µë°±ì´ë©?ë¶„ë¥˜?ì„œ ?„ë½?˜ì? ?Šë„ë¡??¼ë°˜ ?±ê¸‰?¼ë¡œ ì·¨ê¸‰?œë‹¤.
            // ?„ì¬ CardData.rareê°€ enum???„ë‹Œ string?´ë?ë¡?"common"??ë¬¸ì?´ë¡œ ?¬ìš©?œë‹¤.
            string cardRarity = string.IsNullOrWhiteSpace(original.rare)
                ? "common"
                : original.rare;

            // ???¬ê??„ê? ì²˜ìŒ ?±ì¥?ˆë‹¤ë©?ì¹´ë“œ ?¸ë±?¤ë? ?´ì„ ë¹?ëª©ë¡??ë§Œë“¤???•ì…”?ˆë¦¬???±ë¡?œë‹¤.
            if (!cardIndexesGroupedByRarity.TryGetValue(
                    cardRarity,
                    out List<int> cardIndexesForThisRarity))
            {
                cardIndexesForThisRarity = new List<int>();
                cardIndexesGroupedByRarity.Add(cardRarity, cardIndexesForThisRarity);
            }

            // ?„ì¬ ì¹´ë“œ ë²ˆí˜¸ë¥??´ë‹¹ ?¬ê??„ì˜ ë¬´ì‘???ì„± ?„ë³´??ì¶”ê??œë‹¤.
            cardIndexesForThisRarity.Add(card.legacyCardIndex);
        }

        // ? íš¨???„ë³´ê°€ ?˜ë‚˜???†ìœ¼ë©??œë¤ ? íƒ???˜í–‰?????†ë‹¤.
        if (cardIndexesGroupedByRarity.Count == 0)
        {
            return false;
        }

        // ë¨¼ì? ì¡´ì¬?˜ëŠ” ?¬ê???ì¢…ë¥˜ ì¤??˜ë‚˜ë¥??™ì¼ ?•ë¥ ë¡?? íƒ?œë‹¤.
        // ì¹´ë“œ ê°œìˆ˜ ë¹„ë?ê°€ ?„ë‹ˆë¯€ë¡?common 20?? rare 2?¥ì´?´ë„ ?¬ê???? íƒ ?•ë¥ ?€ ê°ê° 50%??
        List<string> availableRarities = new List<string>(cardIndexesGroupedByRarity.Keys);
        string randomlySelectedRarity =
            availableRarities[UnityEngine.Random.Range(0, availableRarities.Count)];

        // ? íƒ???¬ê??„ì— ?í•œ ì¹´ë“œ ë²ˆí˜¸ ëª©ë¡?ì„œ ìµœì¢… ?ì„± ì¹´ë“œ ???¥ì„ ?™ì¼ ?•ë¥ ë¡?? íƒ?œë‹¤.
        List<int> cardIndexesInSelectedRarity =
            cardIndexesGroupedByRarity[randomlySelectedRarity];
        int generatedCardIndex = cardIndexesInSelectedRarity[
            UnityEngine.Random.Range(0, cardIndexesInSelectedRarity.Count)];

        // ë°©ê¸ˆ ?¬ìš©??ë²„ì„¯???ˆë˜ ?íŒ¨ ?„ì¹˜ë¥???ì¹´ë“œê°€ ?¤ì–´ê°?? íš¨???¬ë¡¯ ë²”ìœ„ë¡??œí•œ?œë‹¤.
        int mushroomHandSlotIndex = Mathf.Clamp(
            usedMushroomCardInfo.HandSlotIndex,
            0,
            Mathf.Max(0, cardsInHand.Count - 1));

        // ?•ìƒ ?¬ìš© ?ë¦„?ì„œ??ë²„ì„¯ ?Œë¹„ ?¨ê³„ê°€ ???¬ë¡¯??-1ë¡?ë¹„ì›Œ ?ë?ë¡?ê°™ì? ?ë¦¬????ì¹´ë“œë¥?ì±„ìš´??
        if (mushroomHandSlotIndex < cardsInHand.Count && cardsInHand[mushroomHandSlotIndex] < 0)
        {
            cardsInHand[mushroomHandSlotIndex] = generatedCardIndex;
            mpDiscountForEachHandSlot[mushroomHandSlotIndex] = 1;
        }
        else
        {
            // ë³´ì™„ ê²½ë¡œ: ë²„ì„¯ ?ë¦¬ê°€ ë¹„ì–´ ?ˆì? ?Šìœ¼ë©?ê°™ì? ?„ì¹˜?????¬ë¡¯???½ì…?œë‹¤.
            // ??ê²½ë¡œ??ê¸°ì¡´ ?„ì‹œ ?¬ë¡¯ ë²ˆí˜¸ë¥?ë°€ ???ˆìœ¼ë¯€ë¡?CARD-HAND-04 ê¸°ìˆ ë¶€ì±„ë¡œ ì¶”ì ?œë‹¤.
            cardsInHand.Insert(mushroomHandSlotIndex, generatedCardIndex);
            mpDiscountForEachHandSlot.Insert(mushroomHandSlotIndex, 1);
        }

        // ???¬ë¡¯?€ ?¥ì°© ???Œì†???„ë‹ˆë¯€ë¡??ë§¤ ?˜ëŸ‰ ê³„ì‚°ê³?ë²„ë¦¼ ?”ë? ?¬ìˆœ?˜ì—???œì™¸?œë‹¤.
        temporaryCardSlotsInHand.Add(mushroomHandSlotIndex);
        // ?ì„±??ì¹´ë“œ?€ ? ì¸ ?íƒœê°€ ì¦‰ì‹œ ?”ë©´??ë°˜ì˜?˜ë„ë¡?ìµœì¢… ?íŒ¨ë¥???ë²??„ë‹¬?œë‹¤.
        HandCardsChanged?.Invoke(cardsInHand);
        return true;
    }

    /// <summary>
    /// ?œë¡œ???”ë???ë§ˆì?ë§?ì¹´ë“œ ???¥ì„ ?íŒ¨ë¡??´ë™?œë‹¤.
    /// ?œë¡œ???”ë?ê°€ ë¹„ì—ˆ?¼ë©´ ë¨¼ì? ë²„ë¦¼ ?”ë?ë¥??ì–´ ?¬ì‚¬?©í•˜ë©? ?‘ìª½ ëª¨ë‘ ë¹„ì—ˆ?¼ë©´ falseë¥?ë°˜í™˜?œë‹¤.
    /// ?±ê³µ ??ì¹´ë“œ ?¬ë¡¯ê³?MP ? ì¸ ?¬ë¡¯??ê°™ì? ?„ì¹˜??ì¶”ê??˜ê³  CardDrawn ?´ë²¤?¸ë¡œ ë½‘íŒ ì¹´ë“œ ?¸ë±?¤ë? ?Œë¦°??
    /// </summary>
    private bool TryDrawOneCard()
    {
        // ?„ì§ ë½‘ì? ?Šì? ì¹´ë“œê°€ ?†ë‹¤ë©? ?¬ìš©??ë§ˆì¹œ ì¹´ë“œê°€ ëª¨ì¸ ë²„ë¦¼ ?”ë?ë¥????œë¡œ???”ë?ë¡?ë°”ê¿” ë³¸ë‹¤.
        // MoveDiscardPileBackToDrawPile?€ ë²„ë¦¼ ?”ë?ê°€ ë¹„ì–´ ?ˆìœ¼ë©??„ë¬´ê²ƒë„ ?˜ì? ?Šê³  ê·¸ë?ë¡??Œì•„?¨ë‹¤.
        if (cardsInDrawPile.Count == 0)
        {
            MoveDiscardPileBackToDrawPile();
        }

        // ?¬í™œ?©ì„ ?œë„???¤ì—??0?¥ì´ë©??œë¡œ???”ë??€ ë²„ë¦¼ ?”ë?ê°€ ëª¨ë‘ ë¹„ì–´ ?ˆë‹¤???»ì´??
        // ?¸ì¶œ??DrawCardsUntilHandIsFull?€ falseë¥?ë°›ìœ¼ë©??íŒ¨ ì±„ìš°ê¸?ë°˜ë³µ??ì¤‘ë‹¨?œë‹¤.
        if (cardsInDrawPile.Count == 0)
        {
            return false;
        }

        // List??? íš¨???¸ë±?¤ëŠ” 0ë¶€??Count - 1ê¹Œì??´ë?ë¡?Count - 1??ë§ˆì?ë§?ì¹´ë“œ???„ì¹˜??
        // ???œìŠ¤?œì? ëª©ë¡??ë§ˆì?ë§??ì†Œë¥??¤ì œ ì¹´ë“œ ?±ì˜ ë§???ì¹´ë“œë¡??•í•˜ê³?ê·??„ì¹˜ë¶€??ë½‘ëŠ”??
        int lastIndex = cardsInDrawPile.Count - 1;
        // ?œë¡œ???”ë???ë§??„ì— ?ˆë˜ ì¹´ë“œ ë²ˆí˜¸ë¥??½ëŠ”??
        int drawnCardIndex = cardsInDrawPile[lastIndex];
        // ê°™ì? ì¹´ë“œê°€ ?œë¡œ???”ë??€ ?íŒ¨???™ì‹œ??ì¡´ì¬?˜ì? ?Šë„ë¡??ë˜ ?”ë??ì„œ???œê±°?œë‹¤.
        cardsInDrawPile.RemoveAt(lastIndex);
        // ë½‘ì? ì¹´ë“œ ë²ˆí˜¸ë¥??íŒ¨??ë§ˆì?ë§??¬ë¡¯??ì¶”ê??œë‹¤.
        cardsInHand.Add(drawnCardIndex);

        // MP ? ì¸ ëª©ë¡?€ cardsInHand?€ ê°™ì? ?¬ë¡¯ ë²ˆí˜¸ë¥??¬ìš©?˜ëŠ” ë³‘ë ¬ ëª©ë¡?´ë‹¤.
        // ?¼ë°˜ ?œë¡œ??ì¹´ë“œ??? ì¸ ?¨ê³¼ê°€ ?†ìœ¼ë¯€ë¡???ì¹´ë“œ?€ ê°™ì? ?„ì¹˜??ê¸°ë³¸ê°?0??ì¶”ê??œë‹¤.
        // ?? cardsInHand[2]??ë¹„ìš© ? ì¸?€ mpDiscountForEachHandSlot[2]?ì„œ ?½ëŠ”??
        mpDiscountForEachHandSlot.Add(0);

        // ???¥ì˜ ?œë¡œ?°ê? ?ë‚œ ?œê°„??ì¹´ë“œ ?°ì¶œ?´ë‚˜ ë¡œê·¸ê°€ ë°›ì„ ???ˆë„ë¡?ë½‘íŒ ì¹´ë“œ ë²ˆí˜¸ë¥??Œë¦°??
        // ?„ì²´ ?íŒ¨ UI ê°±ì‹ ?€ ?¬ëŸ¬ ?¥ì„ ëª¨ë‘ ë½‘ì? ??DrawCardsUntilHandIsFull??ë³„ë„ë¡???ë²??¸ì¶œ?œë‹¤.
        CardDrawn?.Invoke(drawnCardIndex);
        // ì¹´ë“œ ???¥ì´ ?•ìƒ?ìœ¼ë¡??íŒ¨???¤ì–´ê°”ìŒ???¸ì¶œ?ì—ê²?ë°˜í™˜?œë‹¤.
        return true;
    }

    /// <summary>?œë¡œ???±ì´ ë¹„ì—ˆ????ë²„ë¦¼ ?”ë?ë¥???²¨ ?ê³  ?ˆë¡œ???œë¡œ???±ìœ¼ë¡?ë§Œë“ ??</summary>
    private void MoveDiscardPileBackToDrawPile()
    {
        // ë²„ë¦¼ ?”ë?ê¹Œì? ë¹„ì–´ ?ˆìœ¼ë©????œë¡œ???”ë?ë¡???¸¸ ì¹´ë“œê°€ ?†ë‹¤.
        // ë¹?ëª©ë¡??AddRange?€ Shuffle ?ì²´??ê°€?¥í•˜ì§€ë§? ?„ë¬´ ?‘ì—…???„ìš” ?†ë‹¤??ì¡°ê±´???¬ê¸°??ëª…í™•???ë‚¸??
        if (cardsInDiscardPile.Count == 0)
        {
            return;
        }

        // ?¬ìš©??ë§ˆì¹œ ëª¨ë“  ?¥ì°© ì¹´ë“œë¥????œë¡œ???”ë?ë¡??˜ëŒë¦°ë‹¤.
        cardsInDrawPile.AddRange(cardsInDiscardPile);
        // ê°™ì? ì¹´ë“œê°€ ???”ë????™ì‹œ??ì¡´ì¬?˜ì? ?Šë„ë¡??´ë™???ë‚œ ë²„ë¦¼ ?”ë?ë¥?ë¹„ìš´??
        cardsInDiscardPile.Clear();
        // ë§??œí™˜ë§ˆë‹¤ ê°™ì? ?œì„œë¡??¤ì‹œ ë½‘íˆì§€ ?Šë„ë¡????œë¡œ???”ë?ë¥??ëŠ”??
        Shuffle(cardsInDrawPile);
    }

    /// <summary>
    /// ???Œë ˆ?´ì–´ ?´ì´ ?œì‘?˜ë©´ ?´ì „ ?´ì— ?¨ì? ?¼ë°˜ ì¹´ë“œë¥?ë²„ë¦¼ ?”ë?ë¡???¸°ê³??íŒ¨ ?œí•œê¹Œì? ?¤ì‹œ ë½‘ëŠ”??
    /// ?„íˆ¬ ??ìµœì´ˆ êµ¬ì„±?€ ???¨ìˆ˜ê°€ ?„ë‹ˆ??BattlePlayerRegistrationService??PlayerDeck ?±ë¡ ?¨ê³„?ì„œ ?ë‚˜ ?ˆì–´???œë‹¤.
    /// </summary>
    private void RefreshHandForNewPlayerTurn()
    {
        if (!isBattleDeckInitialized)
        {
            Debug.LogError(
                "???œì‘ ?œë¡œ???¤íŒ¨: PlayerDeck ?±ë¡ë³´ë‹¤ ë¨¼ì? ?Œë ˆ?´ì–´ ?´ì´ ?œì‘?˜ì—ˆ?µë‹ˆ?? " +
                "BattlePlayerRegistrationService???±ë¡ ?œì„œë¥??•ì¸?˜ì„¸??",
                this);
            return;
        }

        // ?´ì „ ?´ì— ?¬ìš©?˜ì? ?Šê³  ?¨ê¸´ ?¥ì°© ì¹´ë“œë§?ë²„ë¦¼ ?”ë?ë¡??´ë™?œë‹¤. ?„ì‹œ ?ì„± ì¹´ë“œ???Œë©¸?œë‹¤.
        MoveRemainingHandToDiscardPile();
        // ë¹??íŒ¨ë¥?ìµœë? ?íŒ¨ ?˜ê¹Œì§€ ì±„ìš´ ??ìµœì¢… ëª©ë¡??UI???„ë‹¬?œë‹¤.
        DrawCardsUntilHandIsFull();
    }

    /// <summary>????ì§ì „ ?¨ì•„ ?ˆë˜ ?¥ì°© ì¹´ë“œ??ë²„ë¦¼ ?”ë?ë¡???¸°ê³??„ì‹œ ?ì„± ì¹´ë“œ???œê±°?œë‹¤.</summary>
    private void MoveRemainingHandToDiscardPile()
    {
        if (cardsInHand.Count == 0)
        {
            return;
        }

        // ?íŒ¨???¨ì? ë³´ë„ˆ???ì„±) ì¹´ë“œ???¥ì°© ???Œì†???„ë‹ˆë¯€ë¡?ë²„ë¦¼ ?”ë?ë¡?ë³´ë‚´ì§€ ?ŠëŠ”??
        // ê·¸ë?ë¡?ë³´ë‚´ë©??´í›„ ?´ì— ?¥ì°©?˜ì? ?Šì? ì¹´ë“œê°€ ê³„ì† ?œë¡œ?°ë˜???ì¸???œë‹¤.
        for (int i = 0; i < cardsInHand.Count; i++)
            if (cardsInHand[i] >= 0 && !temporaryCardSlotsInHand.Contains(i))
                cardsInDiscardPile.Add(cardsInHand[i]);
        cardsInHand.Clear();
        mpDiscountForEachHandSlot.Clear();
        temporaryCardSlotsInHand.Clear();
    }

    /// <summary>?„íˆ¬ ê²Œì„ ê´€ë¦¬ìê°€ ì¤€ë¹„ë˜???ˆìœ¼ë©????œì‘ ?´ë²¤?¸ë? ì¤‘ë³µ ?†ì´ ?°ê²°?œë‹¤.</summary>
    private void TrySubscribeTurnManager()
    {
        if (BattleGameManager.Instance == null)
        {
            return;
        }

        BattleGameManager.Instance.PlayerTurnStarted -= RefreshHandForNewPlayerTurn;
        BattleGameManager.Instance.PlayerTurnStarted += RefreshHandForNewPlayerTurn;
    }

    /// <summary>?¼ì…”-?ˆì´ì¸?ë°©ì‹?¼ë¡œ ?„ë‹¬??ì¹´ë“œ ?¸ë±??ëª©ë¡???œìë¦¬ì—??ë¬´ì‘?„ë¡œ ?ëŠ”??</summary>
    private static void Shuffle(List<int> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (cards[i], cards[swapIndex]) = (cards[swapIndex], cards[i]);
        }
    }
}
