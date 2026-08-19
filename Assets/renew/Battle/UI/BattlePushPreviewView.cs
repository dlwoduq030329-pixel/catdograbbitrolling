using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Confirm 전 밀치기 예상과 실행 직후 충돌 결과를 대상 머리 위에 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class BattlePushPreviewView : MonoBehaviour
{
    private const string ConfigResourcePath = "UI/PushPreview/BattlePushPreviewConfig";
    [SerializeField] private BattlePushPreviewConfig config;
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0.1f)] private float feedbackDuration = 0.8f;

    private readonly List<Marker> previewMarkers = new List<Marker>();
    private readonly List<Marker> feedbackMarkers = new List<Marker>();
    private Canvas canvas;

    private sealed class Marker
    {
        public GameObject Root;
        public RectTransform Rect;
        public Image Image;
        public Transform Target;
    }

    public void Configure(Camera camera)
    {
        targetCamera = camera;
        if (config == null) config = Resources.Load<BattlePushPreviewConfig>(ConfigResourcePath);
    }

    private void OnEnable()
    {
        BattleCardMovementService.PushApplied += HandlePushApplied;
    }

    private void OnDisable()
    {
        BattleCardMovementService.PushApplied -= HandlePushApplied;
    }

    public void Show(BattleCardMovementService.PushPlan plan)
    {
        ShowMany(plan != null ? new[] { plan } : null);
    }

    public void ShowMany(IEnumerable<BattleCardMovementService.PushPlan> plans)
    {
        Hide();
        if (plans == null) return;
        foreach (BattleCardMovementService.PushPlan plan in plans)
        {
            Marker marker = CreateMarker(plan, "Push Preview");
            if (marker != null) previewMarkers.Add(marker);
        }
    }

    public void Hide()
    {
        ClearMarkers(previewMarkers);
    }

    private void HandlePushApplied(BattleCardMovementService.PushPlan plan)
    {
        if (plan == null || (plan.Result != BattleCardMovementService.PushResult.EnemyCollision &&
                            plan.Result != BattleCardMovementService.PushResult.WallCollision &&
                            plan.Result != BattleCardMovementService.PushResult.WaterDefeat)) return;
        Marker marker = CreateMarker(plan, "Push Feedback");
        if (marker != null)
        {
            feedbackMarkers.Add(marker);
            StartCoroutine(RemoveAfter(marker, feedbackDuration));
        }
    }

    private Marker CreateMarker(BattleCardMovementService.PushPlan plan, string name)
    {
        if (plan == null || plan.Target == null || plan.Result == BattleCardMovementService.PushResult.None) return null;
        Configure(targetCamera != null ? targetCamera : Camera.main);
        EnsureCanvas();
        Sprite sprite = config != null ? config.GetSprite(plan.Result) : null;
        if (sprite == null) return null;

        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        Marker marker = new Marker
        {
            Root = root,
            Rect = root.GetComponent<RectTransform>(),
            Image = root.GetComponent<Image>(),
            Target = plan.Target.transform
        };
        marker.Rect.anchorMin = marker.Rect.anchorMax = Vector2.zero;
        marker.Rect.pivot = new Vector2(0.5f, 0.5f);
        marker.Rect.sizeDelta = Vector2.one * config.iconSize;
        marker.Image.sprite = sprite;
        marker.Image.color = config.GetColor(plan.Result);
        marker.Image.preserveAspect = true;
        marker.Image.raycastTarget = false;
        RefreshMarker(marker);
        return marker;
    }

    private void EnsureCanvas()
    {
        if (canvas != null) return;
        GameObject root = new GameObject("Battle Push Information", typeof(RectTransform), typeof(Canvas));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
    }

    private void LateUpdate()
    {
        foreach (Marker marker in previewMarkers) RefreshMarker(marker);
        foreach (Marker marker in feedbackMarkers) RefreshMarker(marker);
    }

    private void RefreshMarker(Marker marker)
    {
        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        if (marker == null || marker.Target == null || camera == null || config == null) return;
        Vector3 screen = camera.WorldToScreenPoint(marker.Target.position + config.targetWorldOffset);
        marker.Rect.anchoredPosition = screen;
        marker.Image.enabled = screen.z > 0f;
    }

    private IEnumerator RemoveAfter(Marker marker, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        feedbackMarkers.Remove(marker);
        if (marker.Root != null) Destroy(marker.Root);
    }

    private static void ClearMarkers(List<Marker> markers)
    {
        foreach (Marker marker in markers)
        {
            if (marker != null && marker.Root != null) Destroy(marker.Root);
        }
        markers.Clear();
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
