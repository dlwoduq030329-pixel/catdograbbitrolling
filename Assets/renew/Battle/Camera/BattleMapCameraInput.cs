using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 전투 맵의 확대/축소, 드래그, 키보드, 화면 가장자리 이동 입력을 BattleCameraRig에 전달한다.
/// Space를 누르는 동안 수동 오프셋을 초기화해 Player를 계속 화면 중심에 고정한다.
/// </summary>
public class BattleMapCameraInput : MonoBehaviour
{
    [Header("카메라 이동 입력")]
    [InspectorName("가운데 버튼 드래그 속도")]
    [SerializeField] private float dragPanSpeed = 0.02f;
    [InspectorName("키보드 이동 속도")]
    [SerializeField] private float keyboardPanSpeed = 12f;

    // 레거시 코드: 화면 가장자리 자동 이동(엣지팬) 기능. 사용자 요청으로 비활성화, 삭제하지 않고 주석 처리로 보존.
    // [InspectorName("화면 가장자리 이동 사용")]
    // [SerializeField] private bool useEdgePan = true;
    // [InspectorName("화면 가장자리 감지 두께")]
    // [SerializeField] private float edgeSize = 18f;
    // [InspectorName("화면 가장자리 이동 속도")]
    // [SerializeField] private float edgePanSpeed = 12f;

    [Header("카메라 확대 및 축소")]
    [InspectorName("마우스 휠 확대/축소 속도")]
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Camera View Transition")]
    [InspectorName("Side / Top View Toggle Key")]
    [SerializeField] private KeyCode viewToggleKey = KeyCode.C;
    [InspectorName("Manual Tilt Down Key")]
    [SerializeField] private KeyCode tiltDownKey = KeyCode.PageDown;
    [InspectorName("Manual Tilt Up Key")]
    [SerializeField] private KeyCode tiltUpKey = KeyCode.PageUp;
    [InspectorName("틸트 입력 속도(도/초)")]
    [SerializeField] private float tiltInputSpeed = 40f;

    private BattleCameraRig cameraRig;
    private Vector3 previousMousePosition;
    private bool inputEnabled;

    /// <summary>
    /// 같은 Camera GameObject의 BattleCameraRig를 확보한다.
    /// 현재 누락 시 런타임 생성하지만 최종 Scene에서는 Inspector에 미리 연결해 숨은 컴포넌트 생성을 제거한다.
    /// </summary>
    private void Awake()
    {
        cameraRig = GetComponent<BattleCameraRig>();
        if (cameraRig == null)
        {
            cameraRig = gameObject.AddComponent<BattleCameraRig>();
        }
    }

    /// <summary>전투 입력이 허용된 동안 확대, 드래그, 키보드, 화면 가장자리 이동을 처리한다.</summary>
    private void Update()
    {
        if (!inputEnabled || cameraRig == null)
        {
            return;
        }

        HandleZoom();
        HandleTilt();

        if (Input.GetKey(KeyCode.Space))
        {
            cameraRig.ResetManualView();
            return;
        }

        HandleMiddleMouseDrag();
        HandleKeyboardPan();
        // HandleEdgePan(); // 레거시 코드: 엣지팬 비활성화 (아래 메서드 주석 처리와 함께 유지)

        if (Input.GetKeyDown(KeyCode.Home))
        {
            // Home은 수동 이동과 카메라 각도를 모두 초기값으로 되돌리는 숨은 편의 기능이다.
            // 키 설정을 Inspector로 노출하거나 실제 사용 요구가 없으면 제거할 예정이다.
            cameraRig.ResetManualView();
            cameraRig.ResetTilt();
        }
    }

    /// <summary>
    /// 상점·캐릭터 정보·배너 같은 모달 UI가 열릴 때 카메라 수동 입력을 켜거나 끈다.
    /// 재활성화 순간 이전 드래그 좌표가 남아 카메라가 튀지 않도록 현재 마우스 위치도 다시 저장한다.
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        previousMousePosition = Input.mousePosition;
    }

    /// <summary>Camera.main에서 이 컴포넌트를 찾아(없으면 새로 붙여서) 입력을 켜거나 끈다.
    /// 턴/스테이지 배너, 캐릭터 정보 패널처럼 모달이 떠 있는 동안 카메라 드래그·줌·키보드
    /// 이동을 잠글 때 여러 곳에서 공통으로 쓴다.</summary>
    public static void SetEnabledOnMainCamera(bool enabled)
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        BattleMapCameraInput cameraInput = camera.GetComponent<BattleMapCameraInput>();
        if (cameraInput == null)
        {
            if (!enabled) return; // 끄려는데 컴포넌트가 없으면 만들 필요 없음
            cameraInput = camera.gameObject.AddComponent<BattleMapCameraInput>();
        }

        cameraInput.SetInputEnabled(enabled);
    }

    /// <summary>Pointer가 조작 가능한 UI 위에 없을 때 마우스 휠을 카메라-대상 거리 변화로 전달한다.</summary>
    private void HandleZoom()
    {
        if (BattlePlayerInputReader.IsPointerOverInteractiveUI(Input.mousePosition))
        {
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(scroll, 0f))
        {
            cameraRig.AddZoom(-scroll * zoomSpeed);
        }
    }

    /// <summary>C로 Side/Top View를 토글하고 PageDown/PageUp으로 목표 각도를 미세 조정한다.
    /// E는 턴 종료 전용 입력이므로 카메라에서 사용하지 않는다.</summary>
    private void HandleTilt()
    {
        if (Input.GetKeyDown(viewToggleKey))
        {
            cameraRig.ToggleSideTopView();
            return;
        }

        float tiltInput = 0f;
        if (Input.GetKey(tiltDownKey)) tiltInput -= 1f;
        if (Input.GetKey(tiltUpKey)) tiltInput += 1f;

        if (!Mathf.Approximately(tiltInput, 0f))
        {
            cameraRig.AddTilt(tiltInput * tiltInputSpeed * Time.unscaledDeltaTime);
        }
    }

    /// <summary>마우스 가운데 버튼 드래그량을 카메라 평면 이동량으로 변환한다.</summary>
    private void HandleMiddleMouseDrag()
    {
        if (Input.GetMouseButtonDown(2))
        {
            previousMousePosition = Input.mousePosition;
        }

        if (!Input.GetMouseButton(2))
        {
            return;
        }

        Vector3 mousePosition = Input.mousePosition;
        Vector3 mouseDelta = mousePosition - previousMousePosition;
        float zoomScale = Mathf.Max(1f, cameraRig.CurrentZoomHeight / 10f);
        cameraRig.AddPan(new Vector3(
            -mouseDelta.x * dragPanSpeed * zoomScale,
            0f,
            -mouseDelta.y * dragPanSpeed * zoomScale));
        previousMousePosition = mousePosition;
    }

    /// <summary>설정된 방향키 입력으로 카메라의 수동 이동 보정값을 변경한다.</summary>
    private void HandleKeyboardPan()
    {
        Vector3 direction = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical"));

        if (direction.sqrMagnitude > 0f)
        {
            cameraRig.AddPan(direction.normalized * keyboardPanSpeed * Time.unscaledDeltaTime);
        }
    }

    // 레거시 코드: 화면 가장자리 자동 이동(엣지팬). 사용자 요청으로 비활성화, 삭제하지 않고 보존.
    // /// <summary>마우스가 화면 가장자리에 있을 때 해당 방향으로 카메라를 이동한다.</summary>
    // private void HandleEdgePan()
    // {
    //     if (!useEdgePan || Input.GetMouseButton(2))
    //     {
    //         return;
    //     }
    //
    //     Vector3 mousePosition = Input.mousePosition;
    //     Vector3 direction = Vector3.zero;
    //
    //     if (mousePosition.x <= edgeSize)
    //         direction.x = -1f;
    //     else if (mousePosition.x >= Screen.width - edgeSize)
    //         direction.x = 1f;
    //
    //     if (mousePosition.y <= edgeSize)
    //         direction.z = -1f;
    //     else if (mousePosition.y >= Screen.height - edgeSize)
    //         direction.z = 1f;
    //
    //     if (direction.sqrMagnitude > 0f)
    //     {
    //         cameraRig.AddPan(direction.normalized * edgePanSpeed * Time.unscaledDeltaTime);
    //     }
    // }
}
