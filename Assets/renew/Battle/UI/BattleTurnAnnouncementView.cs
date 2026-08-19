using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 층 시작과 플레이어 턴 시작 정보를 화면 중앙에 잠시 표시한다.
/// 전투 규칙에는 관여하지 않고 전달받은 Stage와 Turn 값만 표현한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleTurnAnnouncementView : MonoBehaviour
{
    private GameObject root;
    private TMP_Text titleText;
    private TMP_Text detailText;
    private Coroutine turnRoutine;

    private void Awake()
    {
        EnsureView();
        root.SetActive(false);
    }

    /// <summary>층 번호를 지정한 시간 동안 표시하고 표시 종료까지 기다린다.</summary>
    public IEnumerator ShowStage(int stage, float durationSeconds)
    {
        EnsureView();
        StopTurnRoutine();
        titleText.text = $"STAGE {Mathf.Max(1, stage)}";
        detailText.text = string.Empty;
        root.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, durationSeconds));
        root.SetActive(false);
    }

    /// <summary>플레이어 턴과 현재 턴 번호를 지정한 시간 동안 표시한다.</summary>
    public void ShowPlayerTurn(int turn, float durationSeconds)
    {
        EnsureView();
        StopTurnRoutine();
        turnRoutine = StartCoroutine(ShowPlayerTurnRoutine(turn, durationSeconds));
    }

    /// <summary>ShowPlayerTurn과 동일하지만 표시가 끝날 때까지 호출자가 기다릴 수 있도록
    /// IEnumerator로 노출한다(BattleGameManager가 이 동안 입력을 잠글 때 사용).</summary>
    public IEnumerator ShowPlayerTurnRoutine(int turn, float durationSeconds)
    {
        EnsureView();
        StopTurnRoutine();
        titleText.text = "PLAYER TURN";
        detailText.text = $"TURN {Mathf.Max(1, turn)}";
        root.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, durationSeconds));
        root.SetActive(false);
        turnRoutine = null;
    }

    /// <summary>적 턴과 해당 라운드 번호를 지정한 시간 동안 표시한다.</summary>
    public IEnumerator ShowEnemyTurnRoutine(int turn, float durationSeconds)
    {
        EnsureView();
        StopTurnRoutine();
        titleText.text = "ENEMY TURN";
        detailText.text = $"TURN {Mathf.Max(1, turn)}";
        root.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, durationSeconds));
        root.SetActive(false);
        turnRoutine = null;
    }

    private void StopTurnRoutine()
    {
        if (turnRoutine == null) return;
        StopCoroutine(turnRoutine);
        turnRoutine = null;
    }

    private void EnsureView()
    {
        if (root != null) return;

        root = new GameObject("Battle Turn Announcement", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        titleText = CreateText("Title", new Vector2(0f, 35f), new Vector2(1500f, 130f), 78f);
        detailText = CreateText("Detail", new Vector2(0f, -70f), new Vector2(1000f, 80f), 42f);
    }

    private TMP_Text CreateText(string objectName, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }
}
