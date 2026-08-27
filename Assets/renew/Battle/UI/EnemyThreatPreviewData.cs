using UnityEngine;

public enum EnemyThreatIntent
{
    Attack,
    Chase
}

/// <summary>Enemy AI 계획을 화면 표시 컴포넌트들이 함께 읽을 수 있는 최소 데이터로 변환한 객체.</summary>
public readonly struct EnemyThreatPreviewData
{
    public EnemyTurnActor Enemy { get; }
    public EnemyThreatIntent Intent { get; }
    public MapInfo PlayerDestination { get; }
    public MapInfo EnemyPredictedDestination { get; }
    public EnemyTurnPlan Plan { get; }

    public EnemyThreatPreviewData(
        EnemyTurnActor enemy,
        EnemyThreatIntent intent,
        MapInfo playerDestination,
        EnemyTurnPlan plan)
    {
        Enemy = enemy;
        Intent = intent;
        PlayerDestination = playerDestination;
        EnemyPredictedDestination = plan != null ? plan.PredictedDestinationTile : null;
        Plan = plan;
    }
}
