using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>포털 도착 후 맵과 적만 교체한다. Player 등록·턴 시작을 다시 호출하지 않는다.</summary>
public sealed class StageTransitionController : MonoBehaviour
{
    [SerializeField] private StageSpawner stageSpawner;
    [SerializeField] private NewMapGenerator mapGenerator;
    [SerializeField] private BattleGameManager battleGameManager;
    [SerializeField] private BattleMapRegistry mapRegistry;
    [SerializeField] private BattlePlayerActionController playerActions;
    private BattleUnitMoveFlow moveFlow;
    [SerializeField] private LoadingUI loadingUI;
    [SerializeField] private Camera battleCamera;
    [SerializeField] private FogOfWarManager fog;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.35f;
    [SerializeField, Tooltip("최종 Stage 포털 도착 시 한 번 발생합니다. 결과 화면은 별도로 연결합니다.")]
    private UnityEvent finalStageExited = new UnityEvent();
    private bool transitioning;
    private bool finished;
    private bool inputLocked;
    private Coroutine transitionRoutine;

    private void OnEnable()
    {
        if (battleGameManager != null) battleGameManager.PlayerRegistered += HandlePlayerRegistered;
        SubscribeMovement();
    }

    private void Start() { SubscribeMovement(); }

    private void HandlePlayerRegistered(GameObject player) { SubscribeMovement(); }

    private void SubscribeMovement()
    {
        if (moveFlow == null && playerActions != null) moveFlow = playerActions.moveFlow;
        if (moveFlow == null) return;
        moveFlow.MoveCompleted -= HandleArrival;
        moveFlow.MoveCompleted += HandleArrival;
    }

    private void OnDisable()
    {
        if (battleGameManager != null) battleGameManager.PlayerRegistered -= HandlePlayerRegistered;
        if (moveFlow != null) moveFlow.MoveCompleted -= HandleArrival;
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        RestoreInput();
    }

    private void HandleArrival(MapInfo tile)
    {
        if (tile == null || tile.Type != TileType.Exit || transitioning || finished) return;
        if (stageSpawner == null || stageSpawner.CurrentStage == null || mapGenerator == null ||
            battleGameManager == null || battleGameManager.CurrentPlayer == null ||
            mapRegistry == null || playerActions == null || loadingUI == null)
        { Debug.LogWarning("Stage 전환 참조가 부족해 현재 맵을 유지합니다.", this); return; }
        int next = stageSpawner.CurrentStage.stageNumber + 1;
        if (!stageSpawner.HasStage(next))
        {
            // 중간 Stage의 누락을 게임 완료로 오인하지 않는다.
            if (next <= 4) Debug.LogWarning($"Stage {next} 데이터가 없어 현재 맵을 유지합니다.", this);
            else { finished = true; finalStageExited.Invoke(); }
            return;
        }
        if (!stageSpawner.CanSelectStage(next)) return;
        if (battleGameManager.IsBattleStopped) return;
        transitioning = true;
        transitionRoutine = StartCoroutine(Transition(next, tile));
    }

    private IEnumerator Transition(int next, MapInfo exitTile)
    {
        GameObject player = battleGameManager.CurrentPlayer;
        float playerHeightOffset = player.transform.position.y - exitTile.transform.position.y;
        battleGameManager.LockBattleInputForOverlay();
        inputLocked = true;
        try
        {
            yield return loadingUI.FadeToBlackRoutine(fadeSeconds);
            playerActions.CancelCurrentPlayerAction();
            playerActions.battleRangeVisualizer?.ReleaseMap();
            stageSpawner.ReleaseStageEnemies();
            if (!stageSpawner.TrySelectStage(next)) yield break;
            // 허수아비는 현재 맵의 타일에 귀속된 임시 소환물이다.
            var summons = new List<BattleScarecrowSummon>(BattleScarecrowSummon.Active);
            foreach (BattleScarecrowSummon summon in summons)
                if (summon != null) { summon.gameObject.SetActive(false); Destroy(summon.gameObject); }
            mapRegistry.Clear();
            mapGenerator.StartGenerator();
            // 이전 타일의 지연 Destroy가 끝난 다음 새 타일만 등록한다.
            yield return null;
            var tiles = new List<MapInfo>();
            stageSpawner.CollectMapTiles(tiles);
            mapRegistry.RegisterTiles(tiles);
            MapInfo start = tiles.Find(tile => tile.Type == TileType.Start);
            if (start == null)
            { Debug.LogWarning("새 맵의 시작 타일이 없습니다.", this); yield break; }
            // 같은 Player 인스턴스를 옮겨 지갑·패·장비·HP·MP·턴 행동 상태를 보존한다.
            player.transform.position = start.transform.position + Vector3.up * playerHeightOffset;
            mapRegistry.SetOccupiedTile(player, start);
            playerActions.RefreshMapTiles();
            fog?.ResetFog();
            BattleCameraRig cameraRig = battleCamera != null ? battleCamera.GetComponent<BattleCameraRig>() : null;
            if (cameraRig != null)
            {
                cameraRig.SetMapBounds(tiles);
                cameraRig.FocusPlayerImmediately();
            }
            stageSpawner.TrySpawnStageEnemies(player.transform);
            battleGameManager.SetCurrentStage(next);
            yield return loadingUI.fadeImg(0f, fadeSeconds);
            yield return battleGameManager.PlayStageIntro();
        }
        finally
        {
            RestoreInput();
        }
    }

    private void RestoreInput()
    {
        if (inputLocked)
        {
            loadingUI?.FadeIn(0f);
            battleGameManager?.UnlockBattleInputAfterOverlay();
            inputLocked = false;
        }
        transitioning = false;
        transitionRoutine = null;
    }
}
