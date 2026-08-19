using UnityEngine;

/// <summary>밀치기 사전 예고의 임시 Sprite·색상·크기를 Inspector에서 교체하는 설정 에셋.</summary>
[CreateAssetMenu(fileName = "BattlePushPreviewConfig", menuName = "Renew/전투/밀치기 결과 예고 설정")]
public sealed class BattlePushPreviewConfig : ScriptableObject
{
    [Header("결과별 임시 이미지")]
    public Sprite movedSprite;
    public Sprite resistedSprite;
    public Sprite enemyCollisionSprite;
    public Sprite wallCollisionSprite;
    public Sprite waterDefeatSprite;

    [Header("결과별 색상")]
    public Color movedColor = new Color(0.3f, 1f, 0.35f, 1f);
    public Color resistedColor = new Color(0.65f, 0.7f, 0.75f, 1f);
    public Color enemyCollisionColor = new Color(1f, 0.8f, 0.15f, 1f);
    public Color wallCollisionColor = new Color(1f, 0.4f, 0.1f, 1f);
    public Color waterDefeatColor = new Color(0.2f, 0.75f, 1f, 1f);

    [Header("화면 배치")]
    [Min(8f)] public float iconSize = 72f;
    public Vector3 targetWorldOffset = new Vector3(0f, 2.2f, 0f);

    public Sprite GetSprite(BattleCardMovementService.PushResult result)
    {
        switch (result)
        {
            case BattleCardMovementService.PushResult.Moved: return movedSprite;
            case BattleCardMovementService.PushResult.Resisted: return resistedSprite;
            case BattleCardMovementService.PushResult.EnemyCollision: return enemyCollisionSprite;
            case BattleCardMovementService.PushResult.WallCollision: return wallCollisionSprite;
            case BattleCardMovementService.PushResult.WaterDefeat: return waterDefeatSprite;
            default: return null;
        }
    }

    public Color GetColor(BattleCardMovementService.PushResult result)
    {
        switch (result)
        {
            case BattleCardMovementService.PushResult.Moved: return movedColor;
            case BattleCardMovementService.PushResult.Resisted: return resistedColor;
            case BattleCardMovementService.PushResult.EnemyCollision: return enemyCollisionColor;
            case BattleCardMovementService.PushResult.WallCollision: return wallCollisionColor;
            case BattleCardMovementService.PushResult.WaterDefeat: return waterDefeatColor;
            default: return Color.white;
        }
    }
}
