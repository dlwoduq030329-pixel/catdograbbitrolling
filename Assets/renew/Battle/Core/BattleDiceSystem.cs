using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>주사위 결과에 따라 카드 효과에 적용할 등급입니다.</summary>
public enum DiceResultGrade
{
    [InspectorName("일반")] Normal = 0,
    [InspectorName("실패")] Fail = 1,
    [InspectorName("대성공")] Perfect = 2
}

/// <summary>카드의 주사위 성공 확률에 사용할 캐릭터 능력치입니다.</summary>
public enum CardRollStat
{
    [InspectorName("미지정 - 카드 유형으로 임시 결정")] Auto = 0,
    [InspectorName("STR")] Strength,
    [InspectorName("DEX")] Dexterity,
    [InspectorName("INT")] Intelligence,
    [InspectorName("VIT")] Vitality,
    [InspectorName("WIS")] Wisdom,
    [InspectorName("CHA")] Charisma
}

/// <summary>카드의 피해·회복·보호막 수치를 강화할 캐릭터 능력치입니다.</summary>
public enum CardEffectStat
{
    [InspectorName("능력치 보정 없음")] None = 0,
    [InspectorName("STR")] Strength,
    [InspectorName("DEX")] Dexterity,
    [InspectorName("INT")] Intelligence,
    [InspectorName("VIT")] Vitality,
    [InspectorName("WIS")] Wisdom,
    [InspectorName("CHA")] Charisma
}

/// <summary>카드 한 장에 적용할 주사위 숫자, 결과 등급과 효과 배율입니다.</summary>
public readonly struct CardDiceResult
{
    public int DiceNumber { get; }
    public DiceResultGrade ResultGrade { get; }
    public float Multiplier => ResultGrade switch
    {
        DiceResultGrade.Fail => 0.5f,
        DiceResultGrade.Perfect => 2f,
        _ => 1f
    };

    public CardDiceResult(int diceNumber, DiceResultGrade resultGrade)
    {
        DiceNumber = Mathf.Clamp(diceNumber, 1, 6);
        ResultGrade = resultGrade;
    }

    public static CardDiceResult Normal => new CardDiceResult(3, DiceResultGrade.Normal);
}

/// <summary>Player Body의 기본 능력치와 장비 보너스를 더해 현재 능력치를 반환합니다.</summary>
public static class PlayerStatCalculator
{
    public static int GetMaxAP(GameObject player, int baseAP)
    {
        return Mathf.Max(0, baseAP) +
               Mathf.FloorToInt(Get(player, CardEffectStat.Dexterity) / 10f);
    }

    public static int GetMaxMP(GameObject player)
    {
        return 6 + Mathf.FloorToInt(Get(player, CardEffectStat.Wisdom) / 10f);
    }

    public static float GetMaxHP(GameObject player)
    {
        return 15f + Get(player, CardEffectStat.Vitality);
    }

    /// <summary>CHA 1당 1%, 최대 50%의 상점 할인율을 반환합니다.</summary>
    public static float GetShopDiscount(GameObject player)
    {
        return Mathf.Clamp(Get(player, CardEffectStat.Charisma) * 0.01f, 0f, 0.5f);
    }

    /// <summary>CHA 1당 이벤트 성공 확률 보정 1%를 반환합니다.</summary>
    public static float GetEventSuccessBonus(GameObject player)
    {
        return Mathf.Clamp01(Get(player, CardEffectStat.Charisma) * 0.01f);
    }

    public static float Get(GameObject player, CardEffectStat stat)
    {
        if (player == null || stat == CardEffectStat.None)
            return 0f;

        CharactorStatus character = player.GetComponent<CharactorStatus>();
        PlayerWeapon weapon = player.GetComponent<PlayerWeapon>();
        PlayerEquipmentStats equipment = weapon != null ? weapon.TotalEquipmentStats : default;

        return stat switch
        {
            CardEffectStat.Strength => (character != null ? character.STR : 0) + equipment.StrengthBonus,
            CardEffectStat.Dexterity => (character != null ? character.DEX : 0) + equipment.DexterityBonus,
            CardEffectStat.Intelligence => (character != null ? character.INT : 0) + equipment.IntelligenceBonus,
            CardEffectStat.Vitality => (character != null ? character.VIT : 0) + equipment.VitalityBonus,
            CardEffectStat.Wisdom => (character != null ? character.WIS : 0) + equipment.WisdomBonus,
            CardEffectStat.Charisma => (character != null ? character.CAR : 0) + equipment.CharismaBonus,
            _ => 0f
        };
    }

    public static float Get(GameObject player, CardRollStat stat)
    {
        if (player == null || stat == CardRollStat.Auto)
            return 0f;

        CharactorStatus character = player.GetComponent<CharactorStatus>();
        PlayerWeapon weapon = player.GetComponent<PlayerWeapon>();
        PlayerEquipmentStats equipment = weapon != null ? weapon.TotalEquipmentStats : default;

        return stat switch
        {
            CardRollStat.Strength => (character != null ? character.STR : 0) + equipment.StrengthBonus,
            CardRollStat.Dexterity => (character != null ? character.DEX : 0) + equipment.DexterityBonus,
            CardRollStat.Intelligence => (character != null ? character.INT : 0) + equipment.IntelligenceBonus,
            CardRollStat.Vitality => (character != null ? character.VIT : 0) + equipment.VitalityBonus,
            CardRollStat.Wisdom => (character != null ? character.WIS : 0) + equipment.WisdomBonus,
            CardRollStat.Charisma => (character != null ? character.CAR : 0) + equipment.CharismaBonus,
            _ => 0f
        };
    }
}

/// <summary>카드 사용이 확정되면 주사위 버튼을 열고 결과를 카드 실행 코드에 전달합니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleDiceSystem : MonoBehaviour
{
    [Header("효과 굴림 확률")]
    [Tooltip("이 수치 이하에서는 대성공을 제외한 확률의 절반이 실패입니다.")]
    [FormerlySerializedAs("minimumAccuracyStat")]
    [SerializeField] private float minStat = 6f;
    [Tooltip("이 수치 이상에서는 실패 확률이 최소값으로 고정됩니다.")]
    [FormerlySerializedAs("maximumAccuracyStat")]
    [SerializeField] private float maxStat = 50f;
    [Tooltip("능력치가 최대 기준에 도달했을 때의 실패 확률입니다.")]
    [FormerlySerializedAs("minimumFailChance")]
    [SerializeField, Range(0f, 1f)] private float minFailRate = 0.05f;
    [Tooltip("능력치와 관계없이 유지되는 대성공 확률입니다.")]
    [FormerlySerializedAs("perfectChance")]
    [SerializeField, Range(0f, 1f)] private float perfectRate = 0.2f;

    [Header("주사위 버튼")]
    [Tooltip("카드 사용 확인 뒤 표시할 주사위 버튼 오브젝트입니다.")]
    [FormerlySerializedAs("rollButtonRoot")]
    [SerializeField] private GameObject rollButtonObject;
    [Tooltip("주사위 버튼의 입력 컴포넌트입니다.")]
    [SerializeField] private BattleDiceRollButton rollButton;

    /// <summary>카드 효과를 기다리는 주사위 입력이 진행 중인지 나타냅니다.</summary>
    public bool IsRolling { get; private set; }
    /// <summary>가장 최근에 확정된 주사위 결과입니다.</summary>
    public CardDiceResult LastResult { get; private set; } = CardDiceResult.Normal;

    private Action<CardDiceResult> onRollFinished;
    private float accuracyStatValue;

    private void Awake()
    {
        ConnectRollButton();
        if (rollButtonObject != null)
            rollButtonObject.SetActive(false);
    }

    /// <summary>둘 중 하나만 연결된 경우 같은 UI 계층에서 나머지 참조를 찾습니다.</summary>
    private void ConnectRollButton()
    {
        if (rollButton == null && rollButtonObject != null)
            rollButton = rollButtonObject.GetComponentInChildren<BattleDiceRollButton>(true);

        if (rollButtonObject == null && rollButton != null)
            rollButtonObject = rollButton.transform.parent != null
                ? rollButton.transform.parent.gameObject
                : rollButton.gameObject;

        if (rollButtonObject == null || rollButton == null)
        {
            Debug.LogError(
                $"[BattleDice] RollButton 연결 실패. Scene='{gameObject.scene.name}', " +
                $"Object={(rollButtonObject != null ? rollButtonObject.name : "null")}, " +
                $"Button={(rollButton != null ? rollButton.name : "null")}",
                this);
        }
    }

    /// <summary>카드와 Player를 저장하고 주사위 버튼을 표시합니다.</summary>
    public bool TryStartCardRoll(GameObject player, BattleCardData card, Action<CardDiceResult> onFinished)
    {
        if (IsRolling || player == null || card == null || onFinished == null)
            return false;

        if (rollButtonObject == null || rollButton == null)
            ConnectRollButton();

        CardRollStat rollStat = GetCardRollStat(card);
        accuracyStatValue = PlayerStatCalculator.Get(player, rollStat);
        onRollFinished = onFinished;
        IsRolling = true;

        if (rollButtonObject != null && rollButton != null)
        {
            rollButtonObject.SetActive(true);
            rollButton.ShowForCardRoll(RollDiceAndFinish);
            Debug.Log("[BattleDice] RollButton을 눌러 카드 효과를 결정하세요.", this);
            return true;
        }

        Debug.LogWarning("[BattleDice] RollButton이 없어 자동으로 주사위를 굴립니다.", this);
        RollDiceAndFinish();
        return true;
    }

    /// <summary>주사위 결과를 만들고 대기 중인 카드에 전달합니다.</summary>
    private void RollDiceAndFinish()
    {
        if (!IsRolling)
            return;

        rollButton?.ClearCardRollRequest();
        if (rollButtonObject != null)
            rollButtonObject.SetActive(false);

        LastResult = CreateResult(accuracyStatValue);
        Debug.Log(
            $"[BattleDice] 눈 {LastResult.DiceNumber}, 판정 {LastResult.ResultGrade}, " +
            $"카드 효과 배율 x{LastResult.Multiplier:0.##}",
            this);
        FinishCardRoll();
    }

    /// <summary>캐릭터 능력치에 따라 실패 확률을 계산하고 1~6 결과를 만듭니다.</summary>
    private CardDiceResult CreateResult(float statValue)
    {
        float clampedPerfectRate = Mathf.Clamp01(perfectRate);
        float nonPerfectRate = 1f - clampedPerfectRate;
        float maxStatForCalculation = Mathf.Max(minStat, maxStat);
        float statRatio = Mathf.InverseLerp(minStat, maxStatForCalculation, statValue);
        float failRate = Mathf.Lerp(
            nonPerfectRate * 0.5f,
            Mathf.Clamp(minFailRate, 0f, nonPerfectRate),
            statRatio);

        float randomNumber = UnityEngine.Random.value;
        if (randomNumber < failRate)
            return new CardDiceResult(UnityEngine.Random.Range(1, 3), DiceResultGrade.Fail);

        if (randomNumber < nonPerfectRate)
            return new CardDiceResult(UnityEngine.Random.Range(3, 6), DiceResultGrade.Normal);

        return new CardDiceResult(6, DiceResultGrade.Perfect);
    }

    /// <summary>결과를 한 번 전달하고 다음 카드가 주사위를 사용할 수 있게 초기화합니다.</summary>
    private void FinishCardRoll()
    {
        if (!IsRolling)
            return;

        Action<CardDiceResult> callback = onRollFinished;
        onRollFinished = null;
        IsRolling = false;
        callback?.Invoke(LastResult);
    }

    /// <summary>미지정 카드만 카드 유형으로 임시 판정 능력치를 선택합니다.</summary>
    private static CardRollStat GetCardRollStat(BattleCardData card)
    {
        if (card.rollAccuracyStat != CardRollStat.Auto)
            return card.rollAccuracyStat;

        if (card.cardType == BattleCardType.PhysicalDamage)
            return CardRollStat.Strength;
        if (card.cardType == BattleCardType.MagicDamage)
            return CardRollStat.Intelligence;

        Debug.LogWarning(
            $"[BattleDice] 지원 카드 {card.legacyCardIndex}의 판정 능력치가 없어 WIS를 임시 사용합니다.");
        return CardRollStat.Wisdom;
    }

    private void OnDisable()
    {
        rollButton?.ClearCardRollRequest();
        if (rollButtonObject != null)
            rollButtonObject.SetActive(false);

        if (!IsRolling)
            return;

        LastResult = CardDiceResult.Normal;
        Debug.LogWarning(
            "[BattleDice] 주사위 결과가 나오기 전에 시스템이 꺼져 Normal 결과를 사용합니다.",
            this);
        FinishCardRoll();
    }
}
