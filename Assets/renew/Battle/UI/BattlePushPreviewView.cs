using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 카드 사용 확정 전에는 밀치기 예상 결과를 표시하고, 실제 밀치기 후에는 충돌 결과를 잠시 표시한다.
/// 예상 마커는 카드 대상이 바뀔 때 즉시 교체되어야 하지만 결과 마커는 실행 후 일정 시간 남아야 하므로
/// 서로 다른 목록에서 생명주기를 관리한다. 밀치기 계산은 수행하지 않고 전달받은 결과만 화면에 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePushPreviewView : MonoBehaviour
{
    private const string DefaultPreviewConfigResourcePath = "UI/PushPreview/BattlePushPreviewConfig";

    [FormerlySerializedAs("config")]
    [SerializeField, Tooltip("밀치기 결과별 이미지, 색상, 크기와 위치를 보관하는 설정 에셋입니다.")]
    private BattlePushPreviewConfig previewVisualConfig;

    [FormerlySerializedAs("targetCamera")]
    [SerializeField, Tooltip("적의 월드 위치를 화면 좌표로 변환할 전투 카메라입니다.")]
    private Camera battleCamera;

    [FormerlySerializedAs("feedbackDuration")]
    [SerializeField, Min(0.1f), Tooltip("실제 밀치기 후 충돌 결과 아이콘을 화면에 유지하는 시간입니다.")]
    private float resultMarkerDisplaySeconds = 0.8f;

    // 선택 중인 카드의 예상 결과다. 대상이나 카드가 바뀌면 전부 지우고 새 계산 결과로 교체한다.
    private readonly List<PushResultMarker> activePredictionMarkers = new List<PushResultMarker>();

    // 이미 실행된 밀치기의 충돌 결과다. 예상 결과가 갱신되어도 사라지지 않고 표시 시간이 끝날 때 개별 삭제된다.
    private readonly List<PushResultMarker> temporaryResultMarkers = new List<PushResultMarker>();

    private Canvas pushResultOverlayCanvas;

    /// <summary>한 대상의 월드 위치를 따라다니는 밀치기 결과 이미지에 필요한 참조 묶음.</summary>
    private sealed class PushResultMarker
    {
        public GameObject MarkerObject;
        public RectTransform ScreenRect;
        public Image ResultImage;
        public Transform FollowTarget;
    }

    /// <summary>
    /// 월드 위치를 화면 좌표로 바꿀 카메라를 전달받는다.
    /// 설정 에셋이 Inspector에 연결되지 않은 이전 Scene도 동작하도록 Resources 설정을 한 번 보완한다.
    /// </summary>
    public void ConfigurePreviewDependencies(Camera camera)
    {
        battleCamera = camera;
        if (previewVisualConfig == null)
        {
            previewVisualConfig = Resources.Load<BattlePushPreviewConfig>(DefaultPreviewConfigResourcePath);
        }
    }

    /// <summary>실제로 밀치기가 적용된 순간의 결과를 받기 시작한다.</summary>
    private void OnEnable()
    {
        BattleCardMovementService.PushApplied += ShowTemporaryCollisionResult;
    }

    /// <summary>비활성화된 View가 이후 밀치기 결과를 중복 수신하지 않도록 이벤트 연결을 해제한다.</summary>
    private void OnDisable()
    {
        BattleCardMovementService.PushApplied -= ShowTemporaryCollisionResult;
    }

    /// <summary>단일 대상 카드가 계산한 밀치기 예상 결과 하나를 표시한다.</summary>
    public void ShowSinglePushPrediction(BattleCardMovementService.PushPlan pushPlan)
    {
        ShowPushPredictions(pushPlan != null ? new[] { pushPlan } : null);
    }

    /// <summary>
    /// 범위 카드가 계산한 여러 대상의 밀치기 예상 결과를 한 번에 표시한다.
    /// 이전 카드 또는 이전 대상의 예상 결과가 남지 않도록 기존 예상 마커부터 모두 초기화한다.
    /// </summary>
    public void ShowPushPredictions(IEnumerable<BattleCardMovementService.PushPlan> pushPlans)
    {
        HidePushPredictions();
        if (pushPlans == null) return;

        foreach (BattleCardMovementService.PushPlan pushPlan in pushPlans)
        {
            PushResultMarker predictionMarker = CreateMarkerForPushResult(pushPlan, "Push Preview");
            if (predictionMarker != null)
            {
                activePredictionMarkers.Add(predictionMarker);
            }
        }
    }

    /// <summary>현재 카드 선택에 속한 예상 마커만 제거하며, 이미 실행된 충돌 결과 마커는 유지한다.</summary>
    public void HidePushPredictions()
    {
        DestroyAndClearMarkers(activePredictionMarkers);
    }

    /// <summary>
    /// 실제 밀치기가 끝난 뒤 플레이어가 알아야 할 충돌·낙사 결과만 잠시 표시한다.
    /// 정상 이동과 저항은 확정 전 예상 정보로 충분하므로 실행 후 결과 마커를 추가하지 않는다.
    /// </summary>
    private void ShowTemporaryCollisionResult(BattleCardMovementService.PushPlan appliedPushPlan)
    {
        if (appliedPushPlan == null ||
            (appliedPushPlan.Result != BattleCardMovementService.PushResult.EnemyCollision &&
             appliedPushPlan.Result != BattleCardMovementService.PushResult.WallCollision &&
             appliedPushPlan.Result != BattleCardMovementService.PushResult.WaterDefeat))
        {
            return;
        }

        PushResultMarker resultMarker = CreateMarkerForPushResult(appliedPushPlan, "Push Feedback");
        if (resultMarker != null)
        {
            temporaryResultMarkers.Add(resultMarker);
            StartCoroutine(RemoveResultMarkerAfterDelay(resultMarker, resultMarkerDisplaySeconds));
        }
    }

    /// <summary>
    /// 계산된 결과에 맞는 이미지를 설정 에셋에서 꺼내 대상 머리 위를 따라갈 UI 마커를 생성한다.
    /// 표시할 대상·결과·이미지가 하나라도 없으면 불완전한 UI를 만들지 않고 null을 반환한다.
    /// </summary>
    private PushResultMarker CreateMarkerForPushResult(
        BattleCardMovementService.PushPlan pushPlan,
        string markerObjectName)
    {
        if (pushPlan == null ||
            pushPlan.Target == null ||
            pushPlan.Result == BattleCardMovementService.PushResult.None)
        {
            return null;
        }

        ConfigurePreviewDependencies(battleCamera != null ? battleCamera : Camera.main);
        CreateOverlayCanvasIfMissing();

        Sprite resultSprite = previewVisualConfig != null
            ? previewVisualConfig.GetSprite(pushPlan.Result)
            : null;
        if (resultSprite == null) return null;

        GameObject markerObject = new GameObject(
            markerObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        markerObject.transform.SetParent(pushResultOverlayCanvas.transform, false);

        PushResultMarker marker = new PushResultMarker
        {
            MarkerObject = markerObject,
            ScreenRect = markerObject.GetComponent<RectTransform>(),
            ResultImage = markerObject.GetComponent<Image>(),
            FollowTarget = pushPlan.Target.transform
        };

        marker.ScreenRect.anchorMin = marker.ScreenRect.anchorMax = Vector2.zero;
        marker.ScreenRect.pivot = new Vector2(0.5f, 0.5f);
        marker.ScreenRect.sizeDelta = Vector2.one * previewVisualConfig.previewIconSize;
        marker.ResultImage.sprite = resultSprite;
        marker.ResultImage.color = previewVisualConfig.GetColor(pushPlan.Result);
        marker.ResultImage.preserveAspect = true;
        marker.ResultImage.raycastTarget = false;
        UpdateMarkerScreenPosition(marker);
        return marker;
    }

    /// <summary>밀치기 결과 이미지를 다른 HUD 위에 표시할 전용 Overlay Canvas를 최초 한 번만 생성한다.</summary>
    private void CreateOverlayCanvasIfMissing()
    {
        if (pushResultOverlayCanvas != null) return;

        GameObject canvasObject = new GameObject(
            "Battle Push Information",
            typeof(RectTransform),
            typeof(Canvas));
        pushResultOverlayCanvas = canvasObject.GetComponent<Canvas>();
        pushResultOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pushResultOverlayCanvas.sortingOrder = 250;
    }

    /// <summary>
    /// 카메라 이동과 회전이 모두 끝난 LateUpdate에서 각 대상의 최신 월드 위치를 화면 좌표로 변환한다.
    /// 일반 Update에서 계산하면 같은 프레임의 카메라 이동보다 먼저 실행되어 아이콘이 한 프레임 늦게 따라갈 수 있다.
    /// </summary>
    private void LateUpdate()
    {
        foreach (PushResultMarker predictionMarker in activePredictionMarkers)
        {
            UpdateMarkerScreenPosition(predictionMarker);
        }

        foreach (PushResultMarker resultMarker in temporaryResultMarkers)
        {
            UpdateMarkerScreenPosition(resultMarker);
        }
    }

    /// <summary>마커가 따라가는 대상의 현재 월드 위치를 화면 좌표로 바꾸고, 카메라 뒤에 있으면 숨긴다.</summary>
    private void UpdateMarkerScreenPosition(PushResultMarker marker)
    {
        Camera cameraForProjection = battleCamera != null ? battleCamera : Camera.main;
        if (marker == null ||
            marker.FollowTarget == null ||
            cameraForProjection == null ||
            previewVisualConfig == null)
        {
            return;
        }

        Vector3 targetScreenPosition = cameraForProjection.WorldToScreenPoint(
            marker.FollowTarget.position + previewVisualConfig.previewWorldOffset);
        marker.ScreenRect.anchoredPosition = targetScreenPosition;
        marker.ResultImage.enabled = targetScreenPosition.z > 0f;
    }

    /// <summary>실행 결과를 지정된 시간 동안 보여준 뒤 목록과 화면에서 함께 제거한다.</summary>
    private IEnumerator RemoveResultMarkerAfterDelay(PushResultMarker resultMarker, float displaySeconds)
    {
        yield return new WaitForSecondsRealtime(displaySeconds);
        temporaryResultMarkers.Remove(resultMarker);
        if (resultMarker.MarkerObject != null)
        {
            Destroy(resultMarker.MarkerObject);
        }
    }

    /// <summary>목록이 소유한 모든 마커 GameObject를 파괴하고 목록 상태도 비운다.</summary>
    private static void DestroyAndClearMarkers(List<PushResultMarker> markersToClear)
    {
        foreach (PushResultMarker marker in markersToClear)
        {
            if (marker != null && marker.MarkerObject != null)
            {
                Destroy(marker.MarkerObject);
            }
        }

        markersToClear.Clear();
    }

    /// <summary>View가 파괴될 때 런타임에 만든 전용 Canvas와 남아 있는 마커를 함께 정리한다.</summary>
    private void OnDestroy()
    {
        if (pushResultOverlayCanvas != null)
        {
            Destroy(pushResultOverlayCanvas.gameObject);
        }
    }
}
