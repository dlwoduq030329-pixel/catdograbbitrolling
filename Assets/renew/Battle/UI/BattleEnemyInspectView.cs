using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleEnemyInspectView : MonoBehaviour
{
    [SerializeField] private KeyCode inspectKey = KeyCode.Q;
    private Canvas canvas;
    private TMP_Text text;

    private void Awake()
    {
        EnsureView();
        Hide();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Hide();
        if (!Input.GetKey(inspectKey) || !Input.GetMouseButtonDown(0)) return;
        if (BattleGameManager.Instance != null && BattleGameManager.Instance.IsModalInteractionOpen) return;
        if (BattlePlayerInputReader.IsPointerOverInteractiveUI(Input.mousePosition)) return;

        Camera camera = Camera.main;
        if (camera == null) return;
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            Hide();
            return;
        }

        BattleEnemyRuntimeData runtime = hit.collider.GetComponentInParent<BattleEnemyRuntimeData>();
        if (runtime == null || runtime.Data == null)
        {
            Hide();
            return;
        }
        Show(runtime);
    }

    private void Show(BattleEnemyRuntimeData runtime)
    {
        EnsureView();
        BattleEnemyData data = runtime.Data;
        CharacterMP mp = runtime.GetComponent<CharacterMP>();
        int moveTiles = data.moveMPCostPerTile > 0 ? data.maxTurnMP / data.moveMPCostPerTile : 0;
        text.text =
            $"<b>{data.displayName}</b>\n" +
            $"MP : {(mp != null ? mp.CurrentMP.ToString() : "-")} ({data.minTurnMP}-{data.maxTurnMP})\n" +
            $"Max Move : {moveTiles} tiles\n" +
            $"Attack Range : {data.attackRangeTiles} tiles\n" +
            $"Attack : {data.attackDamage:0.##}";
        canvas.gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    private void EnsureView()
    {
        if (canvas != null) return;
        GameObject root = new GameObject("Enemy Inspect View", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        RectTransform panel = new GameObject("Panel", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(root.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-24f, -24f);
        panel.sizeDelta = new Vector2(320f, 210f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

        RectTransform label = new GameObject("Info", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
        label.SetParent(panel, false);
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.offsetMin = new Vector2(18f, 14f);
        label.offsetMax = new Vector2(-18f, -14f);
        text = label.GetComponent<TextMeshProUGUI>();
        text.fontSize = 25f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
    }
}
