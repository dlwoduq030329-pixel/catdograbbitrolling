using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 층 시작과 플레이어 턴 시작 정보를 화면 중앙에 잠시 표시한다.
/// 전투 규칙이나 턴 전환을 직접 판단하지 않고, 전달받은 Stage·Turn 값과 표시 시간만 표현한다.
/// 현재는 이전 Scene 호환을 위해 UI 참조가 없을 때 런타임 임시 UI를 생성한다.
/// 전용 TMP 프리팹이 준비되면 Inspector 참조를 연결하고 런타임 생성 함수 전체를 제거할 예정이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleTurnAnnouncementView : MonoBehaviour
{
    [Header("턴 안내 UI 참조")]
    [FormerlySerializedAs("root")]
    [SerializeField, Tooltip("턴·스테이지 문구 전체를 켜고 끄는 최상위 UI 오브젝트입니다. 전용 프리팹 제공 후 직접 연결합니다.")]
    private GameObject announcementRoot;

    [FormerlySerializedAs("titleText")]
    [SerializeField, Tooltip("STAGE, PLAYER TURN, ENEMY TURN처럼 현재 안내 종류를 크게 표시하는 TMP Text입니다.")]
    private TMP_Text announcementTitleText;

    [FormerlySerializedAs("detailText")]
    [SerializeField, Tooltip("TURN 1처럼 제목 아래에 세부 번호를 표시하는 TMP Text입니다. 스테이지 안내에서는 비웁니다.")]
    private TMP_Text announcementDetailText;

    // 플레이어 턴 안내만 외부에서 기다리지 않고 시작할 수 있어서 Coroutine 참조를 보관한다.
    // 새 안내가 들어오면 이전 안내를 중지하여 두 Coroutine이 같은 UI를 동시에 변경하지 않게 한다.
    private Coroutine activeTurnAnnouncementRoutine;

    /// <summary>연결된 프리팹 UI를 숨긴 상태로 시작하며, 참조가 없으면 이전 Scene용 임시 UI를 생성한다.</summary>
    private void Awake()
    {
        CreateRuntimeFallbackViewIfReferencesAreMissing();
        announcementRoot.SetActive(false);
    }

    /// <summary>
    /// 전달받은 층 번호를 지정한 시간 동안 표시한다.
    /// 호출자인 전투 UI 흐름이 층 안내 종료 후 다음 초기화를 이어갈 수 있도록 IEnumerator를 반환한다.
    /// </summary>
    public IEnumerator ShowStageAnnouncement(int stageNumber, float displaySeconds)
    {
        CreateRuntimeFallbackViewIfReferencesAreMissing();
        StopActiveTurnAnnouncement();

        announcementTitleText.text = $"STAGE {Mathf.Max(1, stageNumber)}";
        announcementDetailText.text = string.Empty;
        announcementRoot.SetActive(true);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, displaySeconds));
        announcementRoot.SetActive(false);
    }

    /// <summary>
    /// 플레이어 턴 안내를 시작하고 즉시 호출자에게 제어를 돌려준다.
    /// 입력 잠금처럼 안내 종료를 기다릴 필요가 없는 호출 경로에서 사용한다.
    /// </summary>
    public void StartPlayerTurnAnnouncement(int turnNumber, float displaySeconds)
    {
        CreateRuntimeFallbackViewIfReferencesAreMissing();
        StopActiveTurnAnnouncement();
        activeTurnAnnouncementRoutine = StartCoroutine(
            ShowPlayerTurnAnnouncementAndWait(turnNumber, displaySeconds));
    }

    /// <summary>
    /// 플레이어 턴과 현재 턴 번호를 표시하고 종료까지 기다린다.
    /// BattleGameManager가 이 Coroutine을 기다리는 동안 전투 입력과 카메라 입력을 잠그는 경로에서 사용한다.
    /// </summary>
    public IEnumerator ShowPlayerTurnAnnouncementAndWait(int turnNumber, float displaySeconds)
    {
        CreateRuntimeFallbackViewIfReferencesAreMissing();
        StopActiveTurnAnnouncement();

        announcementTitleText.text = "PLAYER TURN";
        announcementDetailText.text = $"TURN {Mathf.Max(1, turnNumber)}";
        announcementRoot.SetActive(true);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, displaySeconds));
        announcementRoot.SetActive(false);
        activeTurnAnnouncementRoutine = null;
    }

    /// <summary>
    /// 적 턴과 해당 라운드 번호를 표시하고 종료까지 기다린다.
    /// 안내가 끝난 뒤에만 EnemyTurnRunner가 행동을 시작하도록 BattleGameManager가 이 Coroutine을 기다린다.
    /// </summary>
    public IEnumerator ShowEnemyTurnAnnouncementAndWait(int turnNumber, float displaySeconds)
    {
        CreateRuntimeFallbackViewIfReferencesAreMissing();
        StopActiveTurnAnnouncement();

        announcementTitleText.text = "ENEMY TURN";
        announcementDetailText.text = $"TURN {Mathf.Max(1, turnNumber)}";
        announcementRoot.SetActive(true);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, displaySeconds));
        announcementRoot.SetActive(false);
        activeTurnAnnouncementRoutine = null;
    }

    /// <summary>새 안내가 이전 안내와 UI 활성 상태를 서로 덮어쓰지 않도록 진행 중인 턴 안내를 중지한다.</summary>
    private void StopActiveTurnAnnouncement()
    {
        if (activeTurnAnnouncementRoutine == null) return;

        StopCoroutine(activeTurnAnnouncementRoutine);
        activeTurnAnnouncementRoutine = null;
    }

    /// <summary>
    /// 전용 프리팹이 아직 연결되지 않은 Scene에서만 중앙 안내 Canvas와 TMP Text를 임시 생성한다.
    /// 프리팹 전환 후에는 Awake에서 세 참조를 검증하도록 바꾸고 이 함수와 아래 생성 함수를 삭제한다.
    /// </summary>
    private void CreateRuntimeFallbackViewIfReferencesAreMissing()
    {
        if (announcementRoot != null &&
            announcementTitleText != null &&
            announcementDetailText != null)
        {
            return;
        }

        announcementRoot = new GameObject(
            "Battle Turn Announcement",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        announcementRoot.transform.SetParent(transform, false);

        Canvas fallbackCanvas = announcementRoot.GetComponent<Canvas>();
        fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fallbackCanvas.sortingOrder = 500;

        CanvasScaler fallbackCanvasScaler = announcementRoot.GetComponent<CanvasScaler>();
        fallbackCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        fallbackCanvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        fallbackCanvasScaler.matchWidthOrHeight = 0.5f;

        RectTransform fallbackCanvasRect = announcementRoot.GetComponent<RectTransform>();
        fallbackCanvasRect.anchorMin = Vector2.zero;
        fallbackCanvasRect.anchorMax = Vector2.one;
        fallbackCanvasRect.offsetMin = Vector2.zero;
        fallbackCanvasRect.offsetMax = Vector2.zero;

        announcementTitleText = CreateRuntimeFallbackText(
            "Title",
            new Vector2(0f, 35f),
            new Vector2(1500f, 130f),
            78f);
        announcementDetailText = CreateRuntimeFallbackText(
            "Detail",
            new Vector2(0f, -70f),
            new Vector2(1000f, 80f),
            42f);
    }

    /// <summary>이전 Scene 호환용 임시 TMP Text를 만들고 중앙 기준 위치와 공통 서식을 적용한다.</summary>
    private TMP_Text CreateRuntimeFallbackText(
        string textObjectName,
        Vector2 anchoredPosition,
        Vector2 rectSize,
        float textSize)
    {
        GameObject fallbackTextObject = new GameObject(
            textObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        fallbackTextObject.transform.SetParent(announcementRoot.transform, false);

        RectTransform fallbackTextRect = fallbackTextObject.GetComponent<RectTransform>();
        fallbackTextRect.anchorMin = fallbackTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        fallbackTextRect.anchoredPosition = anchoredPosition;
        fallbackTextRect.sizeDelta = rectSize;

        TMP_Text fallbackText = fallbackTextObject.GetComponent<TMP_Text>();
        fallbackText.alignment = TextAlignmentOptions.Center;
        fallbackText.fontSize = textSize;
        fallbackText.fontStyle = FontStyles.Bold;
        fallbackText.color = Color.white;
        fallbackText.raycastTarget = false;
        fallbackText.enableWordWrapping = false;
        return fallbackText;
    }
}
