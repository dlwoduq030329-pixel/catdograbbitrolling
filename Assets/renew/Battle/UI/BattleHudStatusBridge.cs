using TMPro;
using UnityEngine;

/// <summary>
/// Bridges the legacy HUD labels to the current battle/run state without requiring
/// serialized scene references. This also survives the HUD being created after the
/// battle manager or being replaced during a scene transition.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleHudStatusBridge : MonoBehaviour
{
    private const string TurnObjectName = "TurnText";
    private const string StageObjectName = "StageText";
    private const string GoldObjectName = "Coin Text";
    private const float ResolveIntervalSeconds = 0.5f;

    private TMP_Text turnText;
    private TMP_Text stageText;
    private TMP_Text goldText;
    private float nextResolveTime;
    private int displayedTurn = int.MinValue;
    private int displayedStage = int.MinValue;
    private int displayedGold = int.MinValue;

    private void OnEnable()
    {
        ResolveLabels();
        Refresh(force: true);
    }

    private void Update()
    {
        if (turnText == null || stageText == null || goldText == null)
        {
            if (Time.unscaledTime >= nextResolveTime)
                ResolveLabels();
        }

        Refresh(force: false);
    }

    private void ResolveLabels()
    {
        nextResolveTime = Time.unscaledTime + ResolveIntervalSeconds;
        TMP_Text[] labels = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || !BelongsToHudCanvas(label.transform)) continue;

            switch (label.gameObject.name)
            {
                case TurnObjectName:
                    turnText = label;
                    break;
                case StageObjectName:
                    stageText = label;
                    break;
                case GoldObjectName:
                    goldText = label;
                    break;
            }
        }

        Refresh(force: true);
    }

    private static bool BelongsToHudCanvas(Transform current)
    {
        while (current != null)
        {
            if (current.gameObject.name.StartsWith("HUDCanvas")) return true;
            current = current.parent;
        }

        return false;
    }

    private void Refresh(bool force)
    {
        BattleGameManager manager = BattleGameManager.Instance;
        int turn = manager != null ? manager.CurrentTurn : 0;
        int stage = Mathf.Max(1, DataConfig.stage);
        int gold = Mathf.Max(0, DataConfig.playerMoney);

        if (turnText != null && (force || turn != displayedTurn))
            turnText.text = $"TURN {turn}";
        if (stageText != null && (force || stage != displayedStage))
            stageText.text = $"STAGE {stage}";
        if (goldText != null && (force || gold != displayedGold))
            goldText.text = $"GOLD {gold}G";

        displayedTurn = turn;
        displayedStage = stage;
        displayedGold = gold;
    }
}
