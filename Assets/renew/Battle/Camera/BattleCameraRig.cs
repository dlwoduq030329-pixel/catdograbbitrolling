using UnityEngine;

/// <summary>
/// 전투 중 플레이어 추적, 수동 이동, 확대·축소와 맵 경계 제한을 담당한다.
/// 원본 CameraChase를 수정하지 않고 전투 진입 후 카메라 제어를 인계받는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCameraRig : MonoBehaviour
{
    [Header("전투 카메라 고정 설정")]
    [InspectorName("전투 카메라 회전")]
    [SerializeField] private Vector3 battleCameraEulerAngles = new Vector3(90f, 0f, 0f);
    [InspectorName("원근 투영 사용")]
    [SerializeField] private bool usePerspectiveProjection = true;

    [Header("플레이어 추적")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 15f, 0f);
    [SerializeField, Min(0.01f)] private float followSpeed = 4f;
    [InspectorName("플레이어 이동 상태 모듈")]
    [SerializeField] private BattleMovementController movementController;

    [Header("확대·축소 제한")]
    [SerializeField, Min(1f)] private float minZoomHeight = 5f;
    [SerializeField, Min(1f)] private float maxZoomHeight = 40f;

    private Transform playerTarget;
    private Transform temporaryFocusTarget;
    private Vector3 manualPanOffset;
    private bool hasMapBounds;
    private float minMapX;
    private float maxMapX;
    private float minMapZ;
    private float maxMapZ;

    public float CurrentZoomHeight => followOffset.y;

    private void OnEnable()
    {
        ApplyBattleCameraConfiguration();
        TryBindGameManager();
    }

    private void Start()
    {
        ApplyBattleCameraConfiguration();
        TryBindGameManager();
        RefreshMapBounds();
    }

    private void OnDisable()
    {
        if (BattleGameManager.Instance != null)
        {
            BattleGameManager.Instance.PlayerRegistered -= SetPlayer;
        }
    }

    private void LateUpdate()
    {
        Transform activeTarget = temporaryFocusTarget != null ? temporaryFocusTarget : playerTarget;
        if (activeTarget == null)
        {
            return;
        }

        ResolveMovementController();
        bool holdPlayerDuringMovement =
            movementController != null && movementController.IsExecuting;
        if (holdPlayerDuringMovement)
        {
            manualPanOffset = Vector3.zero;
        }

        ClampPanToMap();
        Vector3 targetPosition = activeTarget.position + followOffset + manualPanOffset;
        transform.position = holdPlayerDuringMovement
            ? targetPosition
            : Vector3.Lerp(
                transform.position,
                targetPosition,
                followSpeed * Time.deltaTime);

        // 다른 카메라 코드가 회전을 변경해도 전투 Rig가 마지막에 고정값을 적용한다.
        transform.rotation = Quaternion.Euler(battleCameraEulerAngles);
    }

    /// <summary>카메라가 따라갈 현재 Player를 교체하고 추적 기준 위치를 즉시 갱신한다.</summary>
    public void SetPlayer(GameObject player)
    {
        playerTarget = player != null ? player.transform : null;
        manualPanOffset = Vector3.zero;
        ResolveMovementController();
        RefreshMapBounds();

        CameraChase legacyCamera = GetComponent<CameraChase>();
        if (legacyCamera != null)
        {
            legacyCamera.enabled = false;
        }

        ApplyBattleCameraConfiguration();
    }

    /// <summary>적 행동 연출 동안 카메라가 따라갈 임시 대상을 지정한다.</summary>
    public void SetTemporaryFocus(Transform target)
    {
        temporaryFocusTarget = target;
        manualPanOffset = Vector3.zero;
    }

    /// <summary>임시 추적을 끝내고 플레이어 추적으로 복귀한다.</summary>
    public void ClearTemporaryFocus()
    {
        temporaryFocusTarget = null;
        manualPanOffset = Vector3.zero;
    }

    /// <summary>보간을 기다리지 않고 즉시 플레이어 중심으로 카메라를 복귀시킨다.</summary>
    public void FocusPlayerImmediately()
    {
        temporaryFocusTarget = null;
        manualPanOffset = Vector3.zero;
        if (playerTarget == null) return;

        transform.position = playerTarget.position + followOffset;
        transform.rotation = Quaternion.Euler(battleCameraEulerAngles);
    }

    /// <summary>마우스 맵 이동으로 생긴 월드 오프셋을 추적 기준에 누적한다.</summary>
    public void AddPan(Vector3 worldDelta)
    {
        if (movementController != null && movementController.IsExecuting)
        {
            return;
        }

        manualPanOffset += new Vector3(worldDelta.x, 0f, worldDelta.z);
        ClampPanToMap();
    }

    /// <summary>휠 입력 높이 변화를 허용된 확대·축소 범위 안에서 적용한다.</summary>
    public void AddZoom(float heightDelta)
    {
        followOffset.y = Mathf.Clamp(
            followOffset.y + heightDelta,
            minZoomHeight,
            maxZoomHeight);
    }

    /// <summary>수동 이동·확대 오프셋을 초기화하여 카메라를 Player 중심 추적으로 복귀시킨다.</summary>
    public void ResetManualView()
    {
        manualPanOffset = Vector3.zero;
    }

    /// <summary>일반 이동 실행 상태를 제공하는 모듈을 연결하며 비어 있으면 Scene에서 한 번 확보한다.</summary>
    private void ResolveMovementController()
    {
        if (movementController == null)
        {
            movementController = FindFirstObjectByType<BattleMovementController>(
                FindObjectsInactive.Include);
        }
    }

    private void TryBindGameManager()
    {
        if (BattleGameManager.Instance == null)
        {
            return;
        }

        BattleGameManager.Instance.PlayerRegistered -= SetPlayer;
        BattleGameManager.Instance.PlayerRegistered += SetPlayer;

        if (BattleGameManager.Instance.CurrentPlayer != null)
        {
            SetPlayer(BattleGameManager.Instance.CurrentPlayer);
        }
    }

    private void RefreshMapBounds()
    {
        MapInfo[] tiles = FindObjectsByType<MapInfo>(FindObjectsSortMode.None);
        if (tiles.Length == 0)
        {
            hasMapBounds = false;
            return;
        }

        minMapX = maxMapX = tiles[0].transform.position.x;
        minMapZ = maxMapZ = tiles[0].transform.position.z;
        foreach (MapInfo tile in tiles)
        {
            if (tile == null)
            {
                continue;
            }

            Vector3 position = tile.transform.position;
            minMapX = Mathf.Min(minMapX, position.x);
            maxMapX = Mathf.Max(maxMapX, position.x);
            minMapZ = Mathf.Min(minMapZ, position.z);
            maxMapZ = Mathf.Max(maxMapZ, position.z);
        }

        hasMapBounds = true;
    }

    private void ClampPanToMap()
    {
        Transform activeTarget = temporaryFocusTarget != null ? temporaryFocusTarget : playerTarget;
        if (!hasMapBounds || activeTarget == null)
        {
            return;
        }

        float focusX = Mathf.Clamp(activeTarget.position.x + manualPanOffset.x, minMapX, maxMapX);
        float focusZ = Mathf.Clamp(activeTarget.position.z + manualPanOffset.z, minMapZ, maxMapZ);
        manualPanOffset.x = focusX - activeTarget.position.x;
        manualPanOffset.z = focusZ - activeTarget.position.z;
    }

    /// <summary>
    /// 기존 CameraChase가 변경한 투영 방식과 회전을 전투 카메라 규칙으로 다시 덮어쓴다.
    /// 확대/축소가 높이 이동 방식이므로 기본값은 원근 투영을 사용한다.
    /// </summary>
    private void ApplyBattleCameraConfiguration()
    {
        Camera controlledCamera = GetComponent<Camera>();
        if (controlledCamera != null)
        {
            controlledCamera.orthographic = !usePerspectiveProjection;
        }

        transform.rotation = Quaternion.Euler(battleCameraEulerAngles);
    }
}
