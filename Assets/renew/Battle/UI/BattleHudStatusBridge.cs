using TMPro;
using UnityEngine;

/// <summary>
/// HUDCanvas 아래의 TURN·STAGE·GOLD 텍스트를 현재 전투 및 진행 데이터와 연결한다.
/// 현재 HUD가 비활성 상태로 시작하거나 BattleGameManager보다 늦게 준비되는 구조를 지원하기 위해
/// Scene에서 세 텍스트를 이름으로 찾아 보관하고, 표시값이 달라졌을 때만 각 텍스트를 갱신한다.
/// HUD 최종 배치가 확정되면 이름 검색과 매 프레임 확인을 제거하고 Inspector 직접 참조와 변경 이벤트로 교체한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleHudStatusBridge : MonoBehaviour
{
    private const string TurnTextObjectName = "TurnText";
    private const string StageTextObjectName = "StageText";
    private const string GoldTextObjectName = "Coin Text";
    private const float MissingReferenceSearchIntervalSeconds = 0.5f;

    private TMP_Text hudTurnText;
    private TMP_Text hudStageText;
    private TMP_Text hudGoldText;
    private float nextMissingReferenceSearchTime;
    private int lastDisplayedTurn = int.MinValue;
    private int lastDisplayedStage = int.MinValue;
    private int lastDisplayedGold = int.MinValue;

    /// <summary>
    /// 컴포넌트가 활성화될 때 비활성 HUD 오브젝트까지 포함해 세 텍스트 참조를 찾고,
    /// 이전 캐시값과 관계없이 현재 TURN·STAGE·GOLD를 즉시 표시한다.
    /// </summary>
    private void OnEnable()
    {
        ResolveHudTextReferences();
        RefreshHudTextIfChanged(forceRefresh: true);
    }

    /// <summary>
    /// 세 텍스트 중 하나라도 아직 연결되지 않았으면 0.5초 간격으로 참조 검색을 다시 시도한다.
    /// 참조가 준비된 뒤에도 현재 값이 이전 표시값과 달라졌는지 확인해 필요한 텍스트만 갱신한다.
    /// </summary>
    private void Update()
    {
        if (hudTurnText == null || hudStageText == null || hudGoldText == null)
        {
            if (Time.unscaledTime >= nextMissingReferenceSearchTime)
            {
                ResolveHudTextReferences();
            }
        }

        RefreshHudTextIfChanged(forceRefresh: false);
    }

    /// <summary>
    /// Scene의 활성·비활성 TMP 텍스트를 한 번 검색하고, HUDCanvas 아래에 있으면서
    /// 지정된 오브젝트 이름과 일치하는 TURN·STAGE·GOLD 텍스트 참조를 보관한다.
    /// 검색이 끝나면 새로 찾은 텍스트에도 현재 값이 즉시 보이도록 강제 갱신한다.
    /// </summary>
    private void ResolveHudTextReferences()
    {
        nextMissingReferenceSearchTime =
            Time.unscaledTime + MissingReferenceSearchIntervalSeconds;
        TMP_Text[] allSceneTextLabels = FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int labelIndex = 0; labelIndex < allSceneTextLabels.Length; labelIndex++)
        {
            TMP_Text candidateText = allSceneTextLabels[labelIndex];
            if (candidateText == null || !IsUnderHudCanvas(candidateText.transform))
            {
                continue;
            }

            switch (candidateText.gameObject.name)
            {
                case TurnTextObjectName:
                    hudTurnText = candidateText;
                    break;
                case StageTextObjectName:
                    hudStageText = candidateText;
                    break;
                case GoldTextObjectName:
                    hudGoldText = candidateText;
                    break;
            }
        }

        RefreshHudTextIfChanged(forceRefresh: true);
    }

    /// <summary>
    /// 후보 텍스트부터 부모 계층을 따라 올라가며 이름이 HUDCanvas로 시작하는 오브젝트가 있는지 확인한다.
    /// 다른 Canvas에 같은 이름의 텍스트가 있어도 전투 HUD 참조로 잘못 선택하지 않기 위한 검사다.
    /// </summary>
    private static bool IsUnderHudCanvas(Transform candidateTransform)
    {
        Transform currentParent = candidateTransform;
        while (currentParent != null)
        {
            if (currentParent.gameObject.name.StartsWith("HUDCanvas"))
            {
                return true;
            }

            currentParent = currentParent.parent;
        }

        return false;
    }

    /// <summary>
    /// BattleGameManager의 현재 턴과 DataConfig의 스테이지·골드를 읽어 마지막 표시값과 비교한다.
    /// 값이 바뀌었거나 강제 갱신이 요청된 항목만 TMP 텍스트에 다시 쓰고 새 표시값을 캐시에 저장한다.
    /// </summary>
    private void RefreshHudTextIfChanged(bool forceRefresh)
    {
        BattleGameManager battleManager = BattleGameManager.Instance;
        int currentTurn = battleManager != null ? battleManager.CurrentTurn : 0;
        int currentStage = Mathf.Max(1, DataConfig.stage);
        int currentGold = Mathf.Max(0, DataConfig.playerMoney);

        if (hudTurnText != null && (forceRefresh || currentTurn != lastDisplayedTurn))
        {
            hudTurnText.text = $"TURN {currentTurn}";
        }

        if (hudStageText != null && (forceRefresh || currentStage != lastDisplayedStage))
        {
            hudStageText.text = $"STAGE {currentStage}";
        }

        if (hudGoldText != null && (forceRefresh || currentGold != lastDisplayedGold))
        {
            hudGoldText.text = $"GOLD {currentGold}G";
        }

        lastDisplayedTurn = currentTurn;
        lastDisplayedStage = currentStage;
        lastDisplayedGold = currentGold;
    }
}
