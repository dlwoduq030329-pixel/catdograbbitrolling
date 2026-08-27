using UnityEngine;

/// <summary>
/// 전투 맵에서 자유 카메라 조작감을 검증하기 위한 MVP 컴포넌트다.
/// Camera Rig의 X·Y·Z 이동, 우클릭 Yaw/Pitch 회전, 휠 Zoom과 단순 위치 제한만 담당한다.
/// Player 자동 추적, Enemy 턴 연출, 지형 충돌과 UI 입력 차단은 첫 실험 범위에 포함하지 않는다.
///
/// 권장 Hierarchy:
/// BattleCameraRig(이 컴포넌트) → RotationPivot → Main Camera
/// 같은 Camera에 기존 BattleCameraRig를 함께 활성화하면 두 코드가 Transform을 서로 덮어쓰므로
/// 비교 QA 중에는 둘 중 하나만 활성화해야 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleFreeCameraController : MonoBehaviour
{
    // Inspector 감도를 사람이 조절하기 쉬운 100~1000 단위로 표시하면서 실제 Mouse 이동량에는
    // 0.01 배율로 적용한다. 따라서 500은 코드 계산에서 5.0 감도와 같은 의미다.
    private const float InspectorMouseSensitivityScale = 0.01f;

    [Header("코드 리뷰")]
    [SerializeField, Tooltip("이 컴포넌트의 역할과 입력 흐름을 확인했다면 체크합니다. 실행 결과에는 영향을 주지 않습니다.")]
    private bool CODE_EXPLAIN;

    [Header("필수 참조")]
    [SerializeField, Tooltip("상하 시점 회전(Pitch)을 담당하는 자식 Transform입니다.")]
    private Transform rotationPivot;
    [SerializeField, Tooltip("Pivot 뒤에서 Zoom 거리를 조절할 실제 전투 Camera Transform입니다.")]
    private Transform cameraTransform;
    [SerializeField, Tooltip("Space를 눌렀을 때 Camera Rig 기준점을 이동시킬 Player Transform입니다.")]
    private Transform playerFocusTarget;

    [Header("이동")]
    [SerializeField, Min(0.1f), Tooltip("WASD로 Camera Rig를 XZ 평면에서 이동시키는 초당 속도입니다.")]
    private float horizontalMoveSpeed = 10f;
    [SerializeField, Min(0.1f), Tooltip("Q/E로 Camera Rig의 Y 높이를 변경하는 초당 속도입니다.")]
    private float verticalMoveSpeed = 6f;
    [SerializeField, Min(0.1f), Tooltip("이동 키를 눌렀을 때 목표 속도까지 도달하는 가속도입니다.")]
    private float movementAcceleration = 35f;
    [SerializeField, Min(0.1f), Tooltip("이동 키를 놓았을 때 Camera가 멈추는 감속도입니다.")]
    private float movementDeceleration = 45f;
    [SerializeField, Min(1f), Tooltip("왼쪽 Shift를 누른 동안 적용할 이동 속도 배율입니다.")]
    private float fastMoveMultiplier = 2f;

    [Header("회전")]
    [SerializeField, Min(1f), Tooltip("우클릭 회전 감도입니다. MVP 권장 시작값은 500이며 실제 계산에는 0.01 배율로 적용됩니다.")]
    private float rotationSensitivity = 500f;
    [SerializeField, Range(0.01f, 0.5f), Tooltip("목표 회전각까지 부드럽게 따라가는 시간입니다. 낮을수록 즉각 반응합니다.")]
    private float rotationSmoothTime = 0.08f;
    [SerializeField, Range(0f, 89f), Tooltip("가장 낮게 내려다볼 수 있는 Pitch 각도입니다.")]
    private float minimumPitch = 30f;
    [SerializeField, Range(1f, 90f), Tooltip("가장 위에서 내려다볼 수 있는 Pitch 각도입니다.")]
    private float maximumPitch = 90f;

    [Header("Zoom")]
    [SerializeField, Min(0.1f), Tooltip("마우스 휠 한 단계가 Camera와 Pivot 사이 거리를 변경하는 양입니다.")]
    private float zoomSensitivity = 3f;
    [SerializeField, Range(0.01f, 0.5f), Tooltip("목표 Zoom 거리까지 부드럽게 이동하는 시간입니다.")]
    private float zoomSmoothTime = 0.1f;
    [SerializeField, Min(0.1f), Tooltip("Pivot에 가장 가까이 접근할 수 있는 Camera 거리입니다.")]
    private float minimumZoomDistance = 4f;
    [SerializeField, Min(0.1f), Tooltip("Pivot에서 가장 멀어질 수 있는 Camera 거리입니다.")]
    private float maximumZoomDistance = 25f;

    [Header("Camera Rig 이동 제한")]
    [SerializeField, Tooltip("체크하면 Camera Rig 위치를 아래 최소·최대 좌표 안으로 제한합니다.")]
    private bool limitRigPosition = true;
    [SerializeField, Tooltip("Camera Rig가 이동할 수 있는 월드 좌표의 최소값입니다.")]
    private Vector3 minimumRigPosition = new Vector3(-20f, 2f, -20f);
    [SerializeField, Tooltip("Camera Rig가 이동할 수 있는 월드 좌표의 최대값입니다.")]
    private Vector3 maximumRigPosition = new Vector3(20f, 20f, 20f);

    [Header("입력 상태")]
    [SerializeField, Tooltip("상점·상태창 등 다른 UI가 열렸을 때 외부 코드가 false로 바꿔 카메라 입력을 막습니다.")]
    private bool inputEnabled = true;

    // 화면에 현재 적용된 좌우(Y축)·상하(X축) 회전각과 Pivot-Camera 사이 거리다.
    private float currentHorizontalAngle;
    private float currentVerticalAngle;
    private float currentCameraDistance;

    // 마우스 입력으로 도달하고 싶은 목표값이다. 현재값은 SmoothRotationAndZoom()에서 이 값을 따라간다.
    private float desiredHorizontalAngle;
    private float desiredVerticalAngle;
    private float desiredCameraDistance;

    // 실제 Camera 이동 속도가 아니다. SmoothDamp가 이전 프레임의 변화량을 기억하는 내부 계산값이다.
    private float horizontalAngleChangeVelocity;
    private float verticalAngleChangeVelocity;
    private float cameraDistanceChangeVelocity;

    // WASD·Q/E 입력으로 Camera Rig가 현재 월드 공간에서 이동하는 실제 XYZ 속도다.
    private Vector3 currentRigMoveVelocity;

    /// <summary>Inspector에서 코드 설명 확인 여부를 조회할 때 사용하며 Camera 동작에는 관여하지 않는다.</summary>
    public bool IsCodeExplanationReviewed => CODE_EXPLAIN;

    private void Awake()
    {
        // 이 카메라는 Root·Pivot·Camera가 역할을 나눠 가진다.
        // 중간 Pivot이나 실제 Camera가 없으면 어느 Transform에 회전과 Zoom을 적용할지 결정할 수 없다.
        if (rotationPivot == null || cameraTransform == null)
        {
            Debug.LogError(
                "자유 카메라 초기화 실패: Rotation Pivot과 Camera Transform을 Inspector에 연결해야 합니다.",
                this);
            enabled = false;
            return;
        }

        // Scene에 배치된 현재 Transform 값을 시작값으로 읽어 Prefab 배치를 코드가 강제로 덮어쓰지 않는다.
        // Root의 Y축 회전은 지면 위에서 Camera가 바라보는 좌우 방향이다.
        currentHorizontalAngle = transform.eulerAngles.y;

        // Pivot의 local X축 회전은 Root를 기준으로 위·아래를 바라보는 각도다.
        // localEulerAngles는 0~360도로 나오므로 NormalizeAngle()로 -180~180 범위로 바꾼 뒤 제한한다.
        currentVerticalAngle = NormalizeAngle(rotationPivot.localEulerAngles.x);
        currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minimumPitch, maximumPitch);

        // Camera는 Pivot의 local Z축 뒤쪽(-Z)에 배치된다. 거리 계산에는 부호가 필요 없으므로 Abs를 사용한다.
        // 잘못된 Prefab 값이 들어와도 Inspector의 최소·최대 Zoom 범위를 벗어나지 않도록 Clamp한다.
        currentCameraDistance = Mathf.Clamp(
            Mathf.Abs(cameraTransform.localPosition.z),
            minimumZoomDistance,
            maximumZoomDistance);

        // 시작 프레임에는 목표값과 현재값을 같게 만들어 Camera가 갑자기 다른 위치로 보간되지 않게 한다.
        desiredHorizontalAngle = currentHorizontalAngle;
        desiredVerticalAngle = currentVerticalAngle;
        desiredCameraDistance = currentCameraDistance;

        // 위에서 검증한 시작값을 Root·Pivot·Camera에 한 번 적용해 세 Transform의 상태를 일치시킨다.
        ApplyRotationAndZoom();
    }

    /// <summary>
    /// 매 프레임 Player 입력을 읽고 Camera Rig의 위치·회전·Zoom을 갱신한다.
    /// 키와 마우스 상태는 프레임마다 달라지므로 일회성 초기화 함수가 아니라 Update에서 검사해야 한다.
    /// 먼저 입력으로 이동 및 목표 회전·Zoom 값을 만들고, 마지막에 SmoothRotationAndZoom()이
    /// 현재 화면값을 목표값 쪽으로 부드럽게 이동시켜 실제 Transform에 적용한다.
    /// </summary>
    private void Update()
    {
        // 상점·상태창처럼 전투 입력이 잠긴 동안에는 Camera도 어떤 키나 마우스 입력도 처리하지 않는다.
        if (!inputEnabled)
        {
            return;
        }

        // WASD·Q/E 입력은 Camera Rig Root의 실제 월드 위치를 변경한다.
        MoveCameraRig();

        // 우클릭 마우스 이동은 실제 Transform을 즉시 돌리지 않고 원하는 좌우·상하 각도만 변경한다.
        RotateViewWithRightMouseDrag();

        // 휠 입력도 Camera를 즉시 이동시키지 않고 원하는 Pivot-Camera 거리만 변경한다.
        ZoomWithMouseWheel();

        // Space가 눌린 한 프레임에는 자유 이동으로 벗어난 Camera 기준점을 Player XZ 위치로 되돌린다.
        FocusOnPlayerWhenSpaceIsPressed();

        // 위 입력 함수들이 만든 목표 회전·Zoom을 현재값이 부드럽게 따라가게 한 뒤 Transform에 적용한다.
        // 반드시 입력 수집 뒤에 호출해야 같은 프레임에 들어온 마우스·휠 결과가 즉시 보간에 반영된다.
        SmoothRotationAndZoom();
    }

    /// <summary>
    /// WASD 입력을 현재 Camera Rig의 좌우·앞뒤 방향으로 변환하고 Q/E 입력을 월드 Y 이동으로 적용한다.
    /// 이동 완료 후에는 ClampRigPosition()을 호출해 설정된 맵 범위를 벗어나지 않게 한다.
    /// </summary>
    private void MoveCameraRig()
    {
        // GetKey는 키를 누르고 있는 모든 프레임에 true다. 이동은 키를 누르는 동안 계속되어야 하므로 사용한다.
        // GetKeyDown은 처음 누른 한 프레임만 true라서 이동에 사용하면 Camera가 한 번만 짧게 움직이고 멈춘다.
        float sideInput = 0f;
        if (Input.GetKey(KeyCode.A)) sideInput -= 1f;
        if (Input.GetKey(KeyCode.D)) sideInput += 1f;

        float forwardInput = 0f;
        if (Input.GetKey(KeyCode.S)) forwardInput -= 1f;
        if (Input.GetKey(KeyCode.W)) forwardInput += 1f;

        float heightInput = 0f;
        if (Input.GetKey(KeyCode.Q)) heightInput -= 1f;
        if (Input.GetKey(KeyCode.E)) heightInput += 1f;

        // Pitch는 이동 방향에 섞지 않는다. Camera가 아래를 향해도 W는 지면과 평행한 앞쪽으로 이동한다.
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 horizontalDirection = flatForward * forwardInput + flatRight * sideInput;
        if (horizontalDirection.sqrMagnitude > 1f)
        {
            horizontalDirection.Normalize();
        }

        float speedMultiplier = Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f;
        Vector3 desiredMoveVelocity = horizontalDirection * (horizontalMoveSpeed * speedMultiplier);
        desiredMoveVelocity += Vector3.up * (heightInput * verticalMoveSpeed * speedMultiplier);

        // sqrMagnitude는 벡터 길이의 제곱이라 음수가 될 수 없다.
        // 이동 입력이 있으면 0보다 커서 가속도를, 입력이 전혀 없으면 정확히 0이라 감속도를 사용한다.
        float velocityChangeRate = desiredMoveVelocity.sqrMagnitude > 0f
            ? movementAcceleration
            : movementDeceleration;
        currentRigMoveVelocity = Vector3.MoveTowards(
            currentRigMoveVelocity,
            desiredMoveVelocity,
            velocityChangeRate * Time.unscaledDeltaTime);
        transform.position += currentRigMoveVelocity * Time.unscaledDeltaTime;
        ClampRigPosition();
    }

    /// <summary>
    /// 우클릭을 누른 동안 Mouse X는 Camera Rig의 Yaw, Mouse Y는 Pivot의 Pitch에 누적한다.
    /// Roll(Z 회전)은 사용하지 않으며 Pitch는 Inspector의 최소·최대 각도로 제한한다.
    /// </summary>
    private void RotateViewWithRightMouseDrag()
    {
        // GetMouseButton(1)은 우클릭을 누르고 있는 동안 계속 true다.
        // GetMouseButtonDown(1)은 누르기 시작한 한 프레임만 true라서 드래그 회전을 계속 읽을 수 없다.
        if (!Input.GetMouseButton(1))
        {
            return;
        }

        // Mouse X/Y는 이미 이번 프레임에 움직인 양이므로 DeltaTime을 다시 곱하지 않는다.
        // DeltaTime을 중복 적용하면 낮은 프레임과 높은 프레임에서 감도가 다르고 회전이 지나치게 둔해진다.
        // Inspector 값 500을 코드에서 다루기 쉬운 실제 감도 5.0으로 줄여 적용한다.
        float appliedMouseSensitivity = rotationSensitivity * InspectorMouseSensitivityScale;

        // Mouse X가 양수면 마우스를 오른쪽으로 움직였다는 뜻이다. 목표 좌우 각도에 더해 Root를 오른쪽으로 돌린다.
        desiredHorizontalAngle += Input.GetAxisRaw("Mouse X") * appliedMouseSensitivity;

        // Mouse Y가 양수면 마우스를 위로 움직였다는 뜻이다. 빼기(-)를 사용해 일반적인 Camera 드래그 방향으로 맞춘다.
        desiredVerticalAngle -= Input.GetAxisRaw("Mouse Y") * appliedMouseSensitivity;

        // 상하 회전만 제한한다. 좌우 회전은 360도 계속 돌 수 있으므로 별도 Clamp를 사용하지 않는다.
        desiredVerticalAngle = Mathf.Clamp(desiredVerticalAngle, minimumPitch, maximumPitch);
    }

    /// <summary>
    /// 마우스 휠 입력으로 Camera와 Pivot 사이 거리만 변경한다.
    /// Rig 높이를 직접 바꾸지 않으므로 Zoom과 Q/E 높이 이동을 서로 독립적으로 비교할 수 있다.
    /// </summary>
    private void ZoomWithMouseWheel()
    {
        float wheelInput = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(wheelInput, 0f))
        {
            return;
        }

        // 휠을 위로 굴리면 wheelInput이 양수다. 기존 거리에서 양수를 빼 Camera를 Pivot 가까이 당긴다(확대).
        // 휠을 아래로 굴리면 음수를 빼므로 거리가 늘어나 Camera가 Pivot에서 멀어진다(축소).
        float requestedCameraDistance = desiredCameraDistance - wheelInput * zoomSensitivity;

        // 너무 가까이 들어가 모델을 관통하거나 너무 멀리 나가 맵 전체가 작아지는 것을 막는다.
        desiredCameraDistance = Mathf.Clamp(
            requestedCameraDistance,
            minimumZoomDistance,
            maximumZoomDistance);
    }

    /// <summary>
    /// 입력 함수가 갱신한 목표 Yaw·Pitch·Zoom을 현재 값이 부드럽게 따라가게 한다.
    /// 입력 수집과 Transform 반영을 분리해 휠·마우스 입력이 한 프레임에 즉시 튀지 않도록 한다.
    /// </summary>
    private void SmoothRotationAndZoom()
    {
        // 첫 번째 보간은 Camera Rig Root의 좌우(Y축) 회전이다.
        // current=현재 화면 각도, desired=마우스로 원하는 각도, ref 값=SmoothDamp가 기억할 변화 속도다.
        currentHorizontalAngle = Mathf.SmoothDampAngle(
            currentHorizontalAngle,
            desiredHorizontalAngle,
            ref horizontalAngleChangeVelocity,
            rotationSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        // 두 번째 보간은 Rotation Pivot의 상하(X축) 회전이다.
        // SmoothDampAngle은 359도→1도처럼 각도가 순환하는 경우에도 짧은 방향으로 자연스럽게 계산한다.
        currentVerticalAngle = Mathf.SmoothDampAngle(
            currentVerticalAngle,
            desiredVerticalAngle,
            ref verticalAngleChangeVelocity,
            rotationSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        // 세 번째 보간은 XYZ축이 아니라 Pivot과 실제 Camera 사이의 거리 한 값이다.
        // 거리는 순환하는 각도가 아니므로 SmoothDampAngle이 아닌 일반 SmoothDamp를 사용한다.
        currentCameraDistance = Mathf.SmoothDamp(
            currentCameraDistance,
            desiredCameraDistance,
            ref cameraDistanceChangeVelocity,
            zoomSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        // Mathf.Infinity는 최대 변화 속도를 별도로 제한하지 않는다는 뜻이고,
        // Time.unscaledDeltaTime은 게임 시간 배율과 무관하게 실제 프레임 시간으로 부드럽게 움직이게 한다.
        // 세 현재값 계산이 끝난 뒤 각 값을 Root·Pivot·Camera Transform에 나눠 적용한다.
        ApplyRotationAndZoom();
    }

    /// <summary>
    /// Space를 누른 프레임에 Camera Rig의 XZ 기준점을 Player 위치로 즉시 옮긴다.
    /// 현재 Camera 높이·회전·Zoom은 유지하므로 전투 대상을 다시 찾되 시점 설정은 잃지 않는다.
    /// </summary>
    private void FocusOnPlayerWhenSpaceIsPressed()
    {
        if (!Input.GetKeyDown(KeyCode.Space))
        {
            return;
        }

        if (playerFocusTarget == null)
        {
            Debug.LogWarning(
                "Player 포커스 실패: Inspector의 Player Focus Target 또는 SetPlayerTarget() 연결이 필요합니다.",
                this);
            return;
        }

        // 이것은 Player를 계속 따라가는 Hold가 아니라 Space를 누른 순간 한 번 실행되는 Snap이다.
        // 먼저 현재 Rig 위치를 복사해 Y 높이는 보존하고 XZ만 Player 위치로 교체한다.
        Vector3 focusedPosition = transform.position;
        focusedPosition.x = playerFocusTarget.position.x;
        focusedPosition.z = playerFocusTarget.position.z;
        transform.position = focusedPosition;
        currentRigMoveVelocity = Vector3.zero;
        // Player가 임시 Bounds 밖에 있어도 Camera Rig가 허용 범위를 넘지 않도록 마지막에 위치 제한을 적용한다.
        ClampRigPosition();
    }

    /// <summary>
    /// 보간이 끝난 좌우 각도는 Root, 상하 각도는 Pivot, Camera 거리는 실제 Camera 자식에 적용한다.
    /// 한 Transform에 세 책임을 모두 넣지 않아 자유 이동·회전·Zoom 계산이 서로 덮어쓰지 않게 한다.
    /// </summary>
    private void ApplyRotationAndZoom()
    {
        // Root는 월드 위쪽(Y축)을 중심으로 좌우만 회전한다.
        transform.rotation = Quaternion.Euler(0f, currentHorizontalAngle, 0f);
        // Pivot은 Root를 기준으로 local X축 상하 회전만 담당한다.
        rotationPivot.localRotation = Quaternion.Euler(currentVerticalAngle, 0f, 0f);
        // Camera는 Pivot 뒤쪽(-Z)에 배치하며 회전은 부모 계층에서 상속받는다.
        cameraTransform.localPosition = new Vector3(0f, 0f, -currentCameraDistance);
    }

    /// <summary>Camera Rig의 기준점 위치를 Inspector에서 지정한 월드 좌표 범위 안으로 제한한다.</summary>
    private void ClampRigPosition()
    {
        if (!limitRigPosition)
        {
            return;
        }

        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, minimumRigPosition.x, maximumRigPosition.x);
        position.y = Mathf.Clamp(position.y, minimumRigPosition.y, maximumRigPosition.y);
        position.z = Mathf.Clamp(position.z, minimumRigPosition.z, maximumRigPosition.z);
        transform.position = position;
    }

    /// <summary>
    /// 상점·상태창처럼 전투 입력을 가리는 UI가 열릴 때 외부 코드가 카메라 입력도 함께 잠그는 공개 API다.
    /// 다시 활성화할 때 이전 마우스 위치를 저장할 필요가 없는 상대 이동값 기반 입력만 사용한다.
    /// 현재 MVP에서는 호출부를 연결하지 않았으며, 타일 작업 후 상점·Chest 입력 QA가 가능할 때 bool 이벤트에 연결한다.
    /// </summary>
    public void SetInputEnabled(bool shouldAcceptInput)
    {
        inputEnabled = shouldAcceptInput;
        if (!inputEnabled)
        {
            // UI가 열리는 순간 이전 이동 관성이 남아 UI 뒤에서 Camera가 계속 움직이지 않게 한다.
            currentRigMoveVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 런타임에 Spawn된 Player를 Space 포커스 대상으로 연결한다.
    /// Scene에 Player가 미리 존재하면 Inspector의 Player Focus Target을 직접 연결해도 된다.
    /// 현재 MVP에서는 호출부를 연결하지 않았으며, 타일 작업 후 Player 등록 이벤트에 연결한다.
    /// </summary>
    public void SetPlayerTarget(Transform playerTransform)
    {
        playerFocusTarget = playerTransform;
    }

    /// <summary>Unity의 0~360도 Euler 값을 비교하기 쉬운 -180~180도 범위로 변환한다.</summary>
    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    /// <summary>
    /// Unity Editor에서 Inspector 값이 변경되거나 Script가 다시 로드될 때 잘못된 최소·최대 설정을 즉시 보정한다.
    /// Play Mode의 매 프레임 함수가 아니며 Build된 게임의 일반 실행 흐름에도 사용되지 않는다.
    /// </summary>
    private void OnValidate()
    {
        // 최대 각도·거리가 최소값보다 작아질 수 없도록 하한을 맞춘다.
        maximumPitch = Mathf.Max(minimumPitch, maximumPitch);
        maximumZoomDistance = Mathf.Max(minimumZoomDistance, maximumZoomDistance);

        // Inspector에서 최소·최대 값을 반대로 입력해도 각 축의 작은 값과 큰 값으로 자동 정렬한다.
        Vector3 lowerBounds = Vector3.Min(minimumRigPosition, maximumRigPosition);
        Vector3 upperBounds = Vector3.Max(minimumRigPosition, maximumRigPosition);
        minimumRigPosition = lowerBounds;
        maximumRigPosition = upperBounds;
    }
}
