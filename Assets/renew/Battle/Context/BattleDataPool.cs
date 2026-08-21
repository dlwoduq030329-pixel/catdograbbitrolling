using UnityEngine;

/// <summary>
/// BattleSceneInstaller가 전달한 Scene 고정 참조와 현재 Player 참조를 다시 보관하는 전환기용 주소록이다.
/// 실제 Object Pool이나 전투 데이터 원본이 아니며 BattleGameManager·UnitRegistry와 Player 정보가 중복된다.
/// 직접 Registry 참조 전환이 끝나면 삭제하고, 이 클래스에 새로운 전투 규칙이나 참조를 추가하지 않는다.
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
    [SerializeField] private BattleUnitMP currentPlayerMP;
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
    public BattleUnitMP CurrentPlayerMP => currentPlayerMP;
    public PlayerCombatData CurrentPlayerCombatData => currentPlayerCombatData;

    /// <summary>
    /// Installer가 Inspector로 받은 Scene 고정 참조를 이 임시 주소록에 복사한다.
    /// Awake와 Start 중복 호출에도 같은 값을 덮어쓸 뿐이지만 최종 구조에서는 이 우회 전달 자체를 제거한다.
    /// </summary>
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

    /// <summary>
    /// 생성된 Player와 자주 조회하는 MP·전투 수치 컴포넌트를 한 번 찾아 캐싱한다.
    /// BattleGameManager도 동일 Player 정보를 보유하므로 최종적으로 Player 소유자 한 곳으로 통합한다.
    /// </summary>
    public void RegisterPlayer(GameObject player)
    {
        currentPlayer = player;
        currentPlayerModel = ResolvePlayerModel(player);
        currentPlayerMP = player != null ? player.GetComponent<BattleUnitMP>() : null;
        currentPlayerCombatData = player != null ? player.GetComponent<PlayerCombatData>() : null;
    }

    /// <summary>전투 종료 또는 Player 교체 전에 이 주소록에 캐싱된 Player 관련 참조만 해제한다.</summary>
    public void ClearPlayer()
    {
        currentPlayer = null;
        currentPlayerModel = null;
        currentPlayerMP = null;
        currentPlayerCombatData = null;
    }

    /// <summary>
    /// Player 자식 이름이 정확히 Model이면 시각 모델을 반환하고, 없으면 Player 루트를 대신 반환한다.
    /// 문자열 기반 Prefab 탐색은 이름 변경을 조용히 숨기므로 추후 Player의 직렬화 참조로 교체한다.
    /// </summary>
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
