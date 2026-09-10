using UnityEngine;

/// <summary>현재 스테이지 번호를 보관하고 기존 저장 값과 맞춥니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleStageState : MonoBehaviour
{
    [SerializeField, Min(1)] private int stage = 1;

    public int Stage => stage;

    public void LoadSavedStage()
    {
        SetStage(DataConfig.stage);
    }

    public void SetStage(int value)
    {
        stage = Mathf.Max(1, value);
        DataConfig.stage = stage;
    }
}
