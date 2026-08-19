using UnityEngine;

/// <summary>
/// 전투 Scene에서 공유하는 핵심 참조를 보관한다.
/// 전투 규칙을 실행하지 않으며 BattleSceneInstaller를 통해서만 참조를 등록한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDataPool : MonoBehaviour
{
    [Header("정적 Scene 참조")]
    [SerializeField] private GameObject playerBody;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private Camera battleCamera;
    [SerializeField] private GameObject playerSelectCanvas;
    [SerializeField] private GameObject battleCanvas;

    [Header("공용 Registry")]
    [SerializeField] private BattleUnitRegistry unitRegistry;
    [SerializeField] private BattleMapRegistry mapRegistry;

    [Header("런타임 Player 참조")]
    [SerializeField] private GameObject currentPlayer;
    [SerializeField] private GameObject currentPlayerModel;
    [SerializeField] private CharacterMP currentPlayerMP;
    [SerializeField] private PlayerCombatData currentPlayerCombatData;

    public GameObject PlayerBody => playerBody;
    public MapGenerator MapGenerator => mapGenerator;
    public Camera BattleCamera => battleCamera;
    public GameObject PlayerSelectCanvas => playerSelectCanvas;
    public GameObject BattleCanvas => battleCanvas;
    public BattleUnitRegistry Units => unitRegistry;
    public BattleMapRegistry Map => mapRegistry;
    public GameObject CurrentPlayer => currentPlayer;
    public GameObject CurrentPlayerModel => currentPlayerModel;
    public CharacterMP CurrentPlayerMP => currentPlayerMP;
    public PlayerCombatData CurrentPlayerCombatData => currentPlayerCombatData;

    /// <summary>Scene에 고정된 참조를 한 번에 등록한다.</summary>
    public void ConfigureSceneReferences(
        GameObject body,
        MapGenerator generator,
        Camera camera,
        GameObject selectCanvas,
        GameObject combatCanvas,
        BattleUnitRegistry units,
        BattleMapRegistry map)
    {
        playerBody = body;
        mapGenerator = generator;
        battleCamera = camera;
        playerSelectCanvas = selectCanvas;
        battleCanvas = combatCanvas;
        unitRegistry = units;
        mapRegistry = map;
    }

    /// <summary>생성된 Player와 Player가 가진 런타임 전투 참조를 등록한다.</summary>
    public void RegisterPlayer(GameObject player)
    {
        currentPlayer = player;
        currentPlayerModel = ResolvePlayerModel(player);
        currentPlayerMP = player != null ? player.GetComponent<CharacterMP>() : null;
        currentPlayerCombatData = player != null ? player.GetComponent<PlayerCombatData>() : null;
    }

    /// <summary>등록된 런타임 Player 참조를 해제한다.</summary>
    public void ClearPlayer()
    {
        currentPlayer = null;
        currentPlayerModel = null;
        currentPlayerMP = null;
        currentPlayerCombatData = null;
    }

    private static GameObject ResolvePlayerModel(GameObject player)
    {
        if (player == null)
        {
            return null;
        }

        Transform model = player.transform.Find("Model");
        return model != null ? model.gameObject : player;
    }
}
