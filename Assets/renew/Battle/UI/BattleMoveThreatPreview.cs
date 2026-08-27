using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player의 이동 후보 타일을 정하고 Enemy 공용 턴 계획을 요청한 뒤 전용 View에 결과를 전달한다.
/// 선·아이콘·타일 표식을 직접 생성하지 않는 위협 미리보기 조정자다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BattleThreatLineView))]
[RequireComponent(typeof(BattleThreatIntentIconView))]
[RequireComponent(typeof(BattleThreatTileMarkerView))]
public sealed class BattleMoveThreatPreview : MonoBehaviour
{
    [SerializeField] private Camera battleCamera;
    [SerializeField] private BattleRaycaster mapPointerRaycaster;
    [SerializeField] private BattlePlayerRangeController playerMoveRange;
    [Tooltip("현재 전투에 등록된 Enemy 목록을 제공한다. Scene 검색 대신 이 목록만 순회한다.")]
    [SerializeField] private BattleUnitRegistry battleUnitRegistry;
    [SerializeField] private BattleThreatLineView threatLineView;
    [SerializeField] private BattleThreatIntentIconView threatIntentIconView;
    [SerializeField] private BattleThreatTileMarkerView threatTileMarkerView;

    // 마지막으로 AI 계획을 계산한 목적지다. 마우스가 같은 타일 위에 머무는 동안 재계산하지 않게 한다.
    private MapInfo lastCalculatedDestination;
    // Player가 이동 타일을 선택한 뒤 확정할 때까지 호버 대신 계속 표시할 목적지다.
    private MapInfo selectedMoveDestination;

    private void Awake()
    {
        // 세 View는 같은 GameObject의 필수 구성 요소다. Scene 전체 검색은 하지 않는다.
        if (threatLineView == null) threatLineView = GetComponent<BattleThreatLineView>();
        if (threatIntentIconView == null) threatIntentIconView = GetComponent<BattleThreatIntentIconView>();
        if (threatTileMarkerView == null) threatTileMarkerView = GetComponent<BattleThreatTileMarkerView>();
        threatIntentIconView.SetCamera(battleCamera);
    }

    /// <summary>
    /// BattleUnitMoveFlow가 이미 보유한 Scene 참조를 전달한다.
    /// 이 컴포넌트가 Camera·RangeController·Registry를 다시 검색하지 않게 하는 초기 연결 지점이다.
    /// </summary>
    public void ConfigureDependencies(
        Camera camera,
        BattleRaycaster pointerRaycaster,
        BattlePlayerRangeController moveRangeController,
        BattleUnitRegistry unitRegistry)
    {
        battleCamera = camera;
        mapPointerRaycaster = pointerRaycaster;
        playerMoveRange = moveRangeController;
        battleUnitRegistry = unitRegistry;
        threatIntentIconView?.SetCamera(camera);
    }

    private void Update()
    {
        // 목적지를 선택한 뒤에는 마우스가 다른 타일로 움직여도 선택 타일의 위험 정보를 유지한다.
        if (selectedMoveDestination != null)
        {
            if (ShouldHideThreatPreview()) HideAllThreatPreviewVisuals();
            else RefreshThreatPreviewWhenDestinationChanges(selectedMoveDestination);
            // 카메라가 회전할 수 있으므로 AI를 다시 계산하지 않아도 아이콘 방향과 위치는 갱신한다.
            threatIntentIconView?.RefreshTransforms();
            return;
        }

        // UI 위, 이동 범위 밖 또는 Map 타일이 아닌 곳에서는 이전 표시가 남지 않게 즉시 숨긴다.
        if (!TryGetReachableTileUnderMouse(out MapInfo tileUnderMouse))
        {
            lastCalculatedDestination = null;
            HideAllThreatPreviewVisuals();
            return;
        }

        RefreshThreatPreviewWhenDestinationChanges(tileUnderMouse);
        threatIntentIconView?.RefreshTransforms();
    }

    /// <summary>
    /// Player가 선택한 이동 목적지를 고정한다. 이후 Update는 마우스 호버 타일 대신 이 타일의 위험만 표시한다.
    /// </summary>
    public void ShowSelectedDestination(MapInfo destination)
    {
        selectedMoveDestination = destination;
        lastCalculatedDestination = null;
        if (destination != null) DisplayThreatPreviewForDestination(destination);
    }

    /// <summary>이동 완료·취소 시 고정 목적지와 화면에 남아 있는 모든 위협 표시를 제거한다.</summary>
    public void ClearSelectedDestination()
    {
        selectedMoveDestination = null;
        lastCalculatedDestination = null;
        HideAllThreatPreviewVisuals();
    }

    /// <summary>
    /// 이동 범위가 닫혔거나 상점·상태창 같은 Overlay UI가 전투 입력을 잠갔으면 true를 반환한다.
    /// true인 동안 Enemy 계획을 계산하지 않고 기존 위협 표시도 숨긴다.
    /// </summary>
    private bool ShouldHideThreatPreview()
    {
        return !BattleRangeVisibilityTracker.IsAnyRangeVisible ||
               (BattleGameManager.Instance != null && BattleGameManager.Instance.IsModalInteractionOpen);
    }

    /// <summary>
    /// 현재 마우스 아래에서 Player가 실제로 이동할 수 있는 Map 타일을 찾는다.
    /// UI 위 포인터, Scene 참조 누락, Map 밖 또는 이동 불가능 타일은 모두 false를 반환한다.
    /// </summary>
    private bool TryGetReachableTileUnderMouse(out MapInfo reachableTileUnderMouse)
    {
        reachableTileUnderMouse = null;
        return !ShouldHideThreatPreview() && mapPointerRaycaster != null && playerMoveRange != null &&
               !BattlePlayerInputReader.IsPointerOverInteractiveUI(Input.mousePosition) &&
               mapPointerRaycaster.TryGetMapTile(Input.mousePosition, out reachableTileUnderMouse) &&
               playerMoveRange.IsReachable(reachableTileUnderMouse);
    }

    /// <summary>직전 계산 타일과 다를 때만 Enemy AI 계획과 화면 표시를 다시 만든다.</summary>
    private void RefreshThreatPreviewWhenDestinationChanges(MapInfo destination)
    {
        if (lastCalculatedDestination == destination) return;
        lastCalculatedDestination = destination;
        DisplayThreatPreviewForDestination(destination);
    }

    /// <summary>
    /// Registry에 생성 순서대로 등록된 Enemy에게 실제 턴과 같은 계획 계산을 요청한다.
    /// 이 함수가 EnemyThreatPreviewData의 유일한 생성 지점이며 View는 전달된 결과만 읽는다.
    /// </summary>
    private List<EnemyThreatPreviewData> BuildThreatPreviewDataFromRegisteredEnemies(MapInfo playerDestination)
    {
        List<EnemyThreatPreviewData> calculatedThreats = new List<EnemyThreatPreviewData>();
        if (battleUnitRegistry == null) return calculatedThreats;

        foreach (GameObject enemyObject in battleUnitRegistry.Enemies)
        {
            // 파괴 대기 중이거나 비활성화된 Enemy는 다음 턴에 행동하지 않으므로 Preview에서도 제외한다.
            if (enemyObject == null || !enemyObject.activeInHierarchy) continue;
            EnemyTurnActor enemy = enemyObject.GetComponent<EnemyTurnActor>();
            if (enemy == null) enemy = enemyObject.GetComponentInChildren<EnemyTurnActor>();
            if (enemy == null ||
                !enemy.TryPredictResponseToPlayerTile(playerDestination, out EnemyTurnPlan plan)) continue;

            // 검은 다음 턴 확정 공격, 눈은 Player 방향으로 추격 이동한다는 의미다.
            if (plan.WillAttack)
                calculatedThreats.Add(new EnemyThreatPreviewData(enemy, EnemyThreatIntent.Attack, playerDestination, plan));
            else if (plan.WillChase)
                calculatedThreats.Add(new EnemyThreatPreviewData(enemy, EnemyThreatIntent.Chase, playerDestination, plan));
        }
        return calculatedThreats;
    }

    /// <summary>
    /// Registry의 Enemy 계획을 한 번만 계산하고 동일한 결과를 선·아이콘·타일 View에 전달한다.
    /// 각 View는 AI를 다시 판단하지 않고 자신이 담당하는 화면 표현만 갱신한다.
    /// </summary>
    private void DisplayThreatPreviewForDestination(MapInfo playerDestination)
    {
        List<EnemyThreatPreviewData> calculatedThreats =
            BuildThreatPreviewDataFromRegisteredEnemies(playerDestination);
        threatLineView?.Show(calculatedThreats);
        threatIntentIconView?.Show(calculatedThreats);
        threatTileMarkerView?.Show(playerDestination, calculatedThreats);
    }

    /// <summary>이전 목적지에 표시된 선·아이콘·타일 마커가 화면에 남지 않도록 모두 숨긴다.</summary>
    private void HideAllThreatPreviewVisuals()
    {
        threatLineView?.HideAll();
        threatIntentIconView?.HideAll();
        threatTileMarkerView?.HideAll();
    }

    private void OnDisable()
    {
        selectedMoveDestination = null;
        lastCalculatedDestination = null;
        HideAllThreatPreviewVisuals();
    }
}
