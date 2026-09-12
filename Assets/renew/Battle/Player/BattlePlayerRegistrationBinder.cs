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
    [Header("Player UI 참조")]
    [InspectorName("MP UI")]
    [SerializeField] private PlayerMPUI playerMpView;
    [InspectorName("AP UI")]
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
        Object logContext)
    {
        Clear();

        if (!TrySetupPlayer(
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

        // 아래 3개는 전부 Player Body(player) 오브젝트에 미리 붙어 있어야 하는 데이터/뷰 컴포넌트다.
        // 누락돼도 조용히 새로 만들지 않고 LogError로 알리고 등록을 중단한다.
        Weapon = player.GetComponent<PlayerWeapon>();
        if (Weapon == null)
        {
            Debug.LogError($"[{nameof(BattlePlayerRegistrationBinder)}] {player.name}에 PlayerWeapon이 없습니다. Scene에 미리 추가해야 합니다.", logContext);
            return false;
        }

        Wallet = player.GetComponent<PlayerWallet>();
        if (Wallet == null)
        {
            Debug.LogError($"[{nameof(BattlePlayerRegistrationBinder)}] {player.name}에 PlayerWallet이 없습니다. Scene에 미리 추가해야 합니다.", logContext);
            return false;
        }

        BattleEquipVisualBinder equipmentView = player.GetComponent<BattleEquipVisualBinder>();
        if (equipmentView == null)
        {
            Debug.LogError($"[{nameof(BattlePlayerRegistrationBinder)}] {player.name}에 BattleEquipVisualBinder가 없습니다. Scene에 미리 추가해야 합니다.", logContext);
            return false;
        }

        Player = player;
        MP = playerMP;
        AP = playerAP;
        CombatData = combatData;
        Health = playerHealth;

        Wallet.InitializeGold(DataConfig.playerMoney);
        Wallet.GoldChanged += SaveGold;

        CombatData.Bind(Weapon);
        equipmentView.Bind(Weapon);

        return true;
    }

    /// <summary>QA 확인용으로 현재 Player의 최대 MP와 AP를 변경합니다.</summary>
    public void SetDebugResources(int maxMP, int maxAP)
    {
        MP?.ConfigureMaxMP(maxMP);
        AP?.ConfigureMaxAP(maxAP);
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

    private bool TrySetupPlayer(
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
