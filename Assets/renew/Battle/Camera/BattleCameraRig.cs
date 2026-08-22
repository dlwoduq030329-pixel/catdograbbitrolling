using UnityEngine;

/// <summary>
/// 전투 중 플레이어 추적, 수동 이동, 확대·축소와 맵 경계 제한을 담당한다.
/// 원본 CameraChase를 수정하지 않고 전투 진입 후 카메라 제어를 인계받는다.
/// 카메라 각도는 기본적으로 사이드뷰(30도)에서 탑뷰(90도)까지 런타임에 조절한다.
/// 최소·최대 각도는 Inspector에서 변경할 수 있어 이후 연출 범위를 다시 조정할 수 있다.
/// 후방 거리(Z) 제한 기능은 코드에 남겨두되 기본은 비활성화(일단 해제) 상태다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleCameraRig : MonoBehaviour
{
    [Header("전투 카메라 틸트 범위")]
    [InspectorName("기본 시작 각도")]
    [SerializeField, Range(0f, 90f)] private float defaultTiltAngle = 45f;
    [InspectorName("최소 틸트 각도(사이드뷰)")]
    [SerializeField, Range(0f, 90f)] private float minTiltAngle = 30f;
    [InspectorName("최대 틸트 각도(탑뷰)")]
    [SerializeField, Range(0f, 90f)] private float maxTiltAngle = 90f;
    [InspectorName("원근 투영 사용")]
    [SerializeField] private bool usePerspectiveProjection = true;
    [InspectorName("View Transition Speed (Degrees/Second)")]
    [SerializeField, Min(1f)] private float tiltTransitionSpeed = 90f;

    [Header("플레이어 추적")]
    [InspectorName("카메라-대상 거리(줌으로 조절)")]
    [SerializeField, Min(1f)] private float camDistance = 15f;
    [SerializeField, Min(0.01f)] private float followSpeed = 4f;
    [InspectorName("플레이어 이동 상태 모듈")]
    [SerializeField] private BattlePlayerMoveTransaction moveTransaction;

    [Header("확대·축소 제한")]
    [SerializeField, Min(1f)] private float minZoomHeight = 5f;
    [SerializeField, Min(1f)] private float maxZoomHeight = 40f;

    [Header("카메라 높이/거리 기준")]
    [InspectorName("사이드뷰 기본 높이(0도 기준 추가 높이)")]
    [SerializeField, Min(0f)] private float baseCameraHeight = 2f;
    [InspectorName("카메라 후방 거리(Z) 제한 사용")]
    [SerializeField] private bool limitBackDistance = false;
    [InspectorName("카메라 최대 후방 거리(Z, 맵 이탈 방지)")]
    [SerializeField, Min(0f)] private float maxBackDistance = 6f;

    private Transform playerTarget;
    private Transform temporaryFocusTarget;
    private Vector3 manualPanOffset;
    private bool hasMapBounds;
    private float minMapX;
    private float maxMapX;
    private float minMapZ;
    private float maxMapZ;

    /// <summary>minTiltAngle(사이드뷰)에서 maxTiltAngle(탑뷰) 사이의 현재 카메라 피치 각도.</summary>
    private float currentTiltAngle;
    private float targetTiltAngle;

    public float CurrentZoomHeight => camDistance;

    /// <summary>현재 카메라 틸트 각도. Inspector에 설정된 최소~최대 범위 사이를 오간다.</summary>
    public float CurrentTiltAngle => currentTiltAngle;

    private void Awake()
    {
        ValidateTiltRange();
        currentTiltAngle = Mathf.Clamp(defaultTiltAngle, minTiltAngle, maxTiltAngle);
        targetTiltAngle = currentTiltAngle;
    }

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
            moveTransaction != null && moveTransaction.IsExecuting;
        if (holdPlayerDuringMovement)
        {
            manualPanOffset = Vector3.zero;
        }

        ClampPanToMap();
        currentTiltAngle = Mathf.MoveTowards(
            currentTiltAngle,
            targetTiltAngle,
            tiltTransitionSpeed * Time.unscaledDeltaTime);
        Vector3 targetPosition = activeTarget.position + ComputeFollowOffset() + manualPanOffset;
        transform.position = holdPlayerDuringMovement
            ? targetPosition
            : Vector3.Lerp(
                transform.position,
                targetPosition,
                followSpeed * Time.deltaTime);

        // 다른 카메라 코드가 회전을 변경해도 전투 Rig가 마지막에 현재 틸트 각도를 적용한다.
        transform.rotation = Quaternion.Euler(currentTiltAngle, 0f, 0f);
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

        transform.position = playerTarget.position + ComputeFollowOffset();
        transform.rotation = Quaternion.Euler(currentTiltAngle, 0f, 0f);
    }

    /// <summary>마우스 맵 이동으로 생긴 월드 오프셋을 추적 기준에 누적한다.</summary>
    public void AddPan(Vector3 worldDelta)
    {
        if (moveTransaction != null && moveTransaction.IsExecuting)
        {
            return;
        }

        manualPanOffset += new Vector3(worldDelta.x, 0f, worldDelta.z);
        ClampPanToMap();
    }

    /// <summary>휠 입력 높이 변화를 허용된 확대·축소 범위 안에서 적용한다.</summary>
    public void AddZoom(float heightDelta)
    {
        camDistance = Mathf.Clamp(
            camDistance + heightDelta,
            minZoomHeight,
            maxZoomHeight);
    }

    /// <summary>Inspector에 설정된 최소~최대 각도 안에서 카메라 틸트를 변경한다.
    /// degreesDelta가 양수이면 탑뷰 방향으로, 음수이면 사이드뷰 방향으로 기운다.</summary>
    public void AddTilt(float degreesDelta)
    {
        targetTiltAngle = Mathf.Clamp(targetTiltAngle + degreesDelta, minTiltAngle, maxTiltAngle);
    }

    /// <summary>현재 목표 각도를 기준으로 Inspector의 사이드뷰 최소 각도와 탑뷰 최대 각도 사이를 전환한다.</summary>
    public void ToggleSideTopView()
    {
        float middle = (minTiltAngle + maxTiltAngle) * 0.5f;
        targetTiltAngle = targetTiltAngle > middle ? minTiltAngle : maxTiltAngle;
    }

    /// <summary>카메라 틸트 목표를 기본 시작 각도로 되돌리고 설정된 속도로 전환한다.</summary>
    public void ResetTilt()
    {
        targetTiltAngle = Mathf.Clamp(defaultTiltAngle, minTiltAngle, maxTiltAngle);
    }

    /// <summary>수동 이동·확대 오프셋을 초기화하여 카메라를 Player 중심 추적으로 복귀시킨다.</summary>
    public void ResetManualView()
    {
        manualPanOffset = Vector3.zero;
    }

    /// <summary>Inspector에서 최소값이 최대값을 넘겨도 항상 유효한 틸트 범위로 보정한다.</summary>
    private void OnValidate()
    {
        ValidateTiltRange();
        defaultTiltAngle = Mathf.Clamp(defaultTiltAngle, minTiltAngle, maxTiltAngle);
    }

    /// <summary>
    /// 최소 각도를 0~90도로 제한하고 최대 각도가 최소 각도보다 작아지지 않도록 Inspector 값을 정규화한다.
    /// 기본 30~90도 범위는 Inspector에서 추후 변경할 수 있다.
    /// </summary>
    private void ValidateTiltRange()
    {
        minTiltAngle = Mathf.Clamp(minTiltAngle, 0f, 90f);
        maxTiltAngle = Mathf.Clamp(maxTiltAngle, minTiltAngle, 90f);
    }

    /// <summary>현재 틸트 각도와 줌 거리로부터 대상 기준 카메라 오프셋을 계산한다.
    /// 각도가 90도(탑뷰)에 가까울수록 대상 바로 위, 줄어들수록(사이드뷰) 대상 뒤쪽(-Z)으로 물러나며 낮아진다.
    /// 0도에서도 baseCameraHeight만큼은 항상 높이를 유지한다(대상 스폰 Y 0.5 기준 카메라 기본 높이 2.5).
    /// 후방 거리(Z) 제한은 limitBackDistance가 켜져 있을 때만 maxBackDistance로 clamp한다(현재는 기본 해제 상태).</summary>
    private Vector3 ComputeFollowOffset()
    {
        float pitchRad = currentTiltAngle * Mathf.Deg2Rad;
        float height = baseCameraHeight + camDistance * Mathf.Sin(pitchRad);
        float back = camDistance * Mathf.Cos(pitchRad);
        if (limitBackDistance)
        {
            back = Mathf.Min(back, maxBackDistance);
        }
        return new Vector3(0f, height, -back);
    }

    /// <summary>일반 이동 실행 상태를 제공하는 모듈을 연결하며 비어 있으면 Scene에서 한 번 확보한다.</summary>
    private void ResolveMovementController()
    {
        if (moveTransaction == null)
        {
            moveTransaction = FindFirstObjectByType<BattlePlayerMoveTransaction>(
                FindObjectsInactive.Include);
        }
    }

    /// <summary>
    /// BattleGameManager의 PlayerRegistered 이벤트를 중복 없이 구독하고 이미 생성된 Player가 있으면 즉시 추적한다.
    /// 현재 Singleton에 의존하므로 SceneInstaller 직접 참조 연결로 전환할 예정이다.
    /// </summary>
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

    /// <summary>
    /// 생성된 모든 MapInfo의 XZ 최소·최대 좌표를 계산해 수동 카메라 이동 한계로 저장한다.
    /// 현재 Player 등록 때마다 Scene 검색하므로 MapRegistry의 등록 타일을 직접 받도록 변경할 예정이다.
    /// </summary>
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

    /// <summary>
    /// Player 또는 임시 포커스 대상에 수동 이동값을 더한 초점이 Map 경계를 벗어나지 않도록 보정한다.
    /// 카메라 Transform 자체가 아니라 추적 초점을 제한한다.
    /// </summary>
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
    /// 기존 CameraChase가 변경한 투영 방식을 전투 카메라 규칙으로 다시 덮어쓰고
    /// 현재 틸트 각도를 회전에 적용한다.
    /// 확대/축소가 높이 이동 방식이므로 기본값은 원근 투영을 사용한다.
    /// </summary>
    private void ApplyBattleCameraConfiguration()
    {
        Camera controlledCamera = GetComponent<Camera>();
        if (controlledCamera != null)
        {
            controlledCamera.orthographic = !usePerspectiveProjection;
        }

        transform.rotation = Quaternion.Euler(currentTiltAngle, 0f, 0f);
    }
}
