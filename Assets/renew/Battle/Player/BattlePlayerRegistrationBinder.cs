using UnityEngine;

/// <summary>
/// 새로 생성된 Player의 전투 데이터, MP 화면, 카드 덱, 행동 제어기를 한 번에 연결한다.
/// BattleGameManager는 Player 등록 시점만 결정하고 실제 컴포넌트 연결은 이 바인더에 위임한다.
/// (2026-08-22 개명: BattlePlayerRuntimeBinder -> BattlePlayerRegistrationBinder — 이름이 같은
/// BattlePlayerRegistrationService/BattlePlayerRuntimeDataFactory와 헷갈린다는 리뷰 지적으로,
/// "등록(Registration) 흐름의 Scene 진입점 컴포넌트"라는 역할이 이름에서 바로 드러나도록 바꿨다.
/// 실제 등록 로직은 BattlePlayerRegistrationService에 그대로 위임한다.)
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlayerRegistrationBinder : MonoBehaviour
{
    [Header("Player 등록 대상")]
    [SerializeField] private PlayerMPUI playerMpView;
    [InspectorName("AP UI (MP와 같은 형식)")]
    [SerializeField] private PlayerAPUI playerApView;

    public GameObject Player { get; private set; }
    public BattleUnitMP MP { get; private set; }
    public BattleUnitAP AP { get; private set; }
    public PlayerCombatData CombatData { get; private set; }
    public BattleHealth Health { get; private set; }
    public PlayerWeapon Weapon { get; private set; }
    public PlayerWallet Wallet { get; private set; }

    public bool Register(
        GameObject player,
        BattleCardDrawSystem cardDrawSystem,
        BattlePlayerActionController actionController,
        bool useDebugStats,
        int debugMaxMP,
        int debugMaxAP,
        Object logContext)
    {
        Clear();

        if (!TryBind(
                player,
                cardDrawSystem,
                actionController,
                logContext,
                out BattleUnitMP playerMP,
                out BattleUnitAP playerAP,
                out PlayerCombatData combatData,
                out BattleHealth playerHealth))
        {
            return false;
        }

        Player = player;
        MP = playerMP;
        AP = playerAP;
        CombatData = combatData;
        Health = playerHealth;
        Weapon = BattleComponentResolver.GetOrAdd(player, player.GetComponent<PlayerWeapon>());
        Wallet = BattleComponentResolver.GetOrAdd(player, player.GetComponent<PlayerWallet>());

        Wallet?.InitializeGold(DataConfig.playerMoney);
        if (Wallet != null)
            Wallet.GoldChanged += SaveGold;

        BattleEquipVisualBinder equipmentView = BattleComponentResolver.GetOrAdd(
            player,
            player.GetComponent<BattleEquipVisualBinder>());
        CombatData.Bind(Weapon);
        equipmentView?.Bind(Weapon);

        if (useDebugStats)
        {
            MP.ConfigureMaxMP(debugMaxMP);
            AP?.ConfigureMaxAP(debugMaxAP);
        }

        return true;
    }

    public void Clear()
    {
        if (Wallet != null)
            Wallet.GoldChanged -= SaveGold;

        Player = null;
        MP = null;
        AP = null;
        CombatData = null;
        Health = null;
        Weapon = null;
        Wallet = null;
    }

    private static void SaveGold(int gold)
    {
        DataConfig.playerMoney = Mathf.Max(0, gold);
    }

    public bool TryBind(
        GameObject player,
        BattleCardDrawSystem cardDrawSystem,
        BattlePlayerActionController actionController,
        Object logContext,
        out BattleUnitMP playerMP,
        out BattleUnitAP playerAP,
        out PlayerCombatData combatData,
        out BattleHealth playerHealth)
    {
        return BattlePlayerRegistrationService.TryRegisterRuntime(
            player,
            playerMpView,
            playerApView,
            cardDrawSystem,
            actionController,
            logContext,
            out playerMP,
            out playerAP,
            out combatData,
            out playerHealth);
    }
}
