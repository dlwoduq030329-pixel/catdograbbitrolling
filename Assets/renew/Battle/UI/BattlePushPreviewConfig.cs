using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 밀치기 계산 결과별로 화면에 표시할 이미지, 색상, 크기와 위치를 보관하는 설정 에셋이다.
/// 이 클래스는 밀치기 결과를 계산하거나 UI를 직접 생성하지 않는다.
/// <see cref="BattlePushPreviewView"/>가 계산된 <see cref="BattleCardMovementService.PushResult"/>를 전달하면
/// 그 결과에 대응하는 시각 설정만 반환한다.
/// </summary>
[CreateAssetMenu(fileName = "BattlePushPreviewConfig", menuName = "Renew/전투/밀치기 결과 예고 설정")]
public sealed class BattlePushPreviewConfig : ScriptableObject
{
    [Header("밀치기 결과별 표시 이미지")]
    [FormerlySerializedAs("movedSprite")]
    [Tooltip("대상이 정상적으로 밀려나 최종 위치가 변경될 때 표시할 이미지입니다.")]
    public Sprite successfulPushSprite;

    [FormerlySerializedAs("resistedSprite")]
    [Tooltip("대상이 밀치기에 저항하여 원래 위치에 남을 때 표시할 이미지입니다.")]
    public Sprite pushResistedSprite;

    [FormerlySerializedAs("enemyCollisionSprite")]
    [Tooltip("밀려난 대상이 다른 적과 충돌할 것으로 예상될 때 표시할 이미지입니다.")]
    public Sprite enemyCollisionSprite;

    [FormerlySerializedAs("wallCollisionSprite")]
    [Tooltip("밀려난 대상이 벽이나 이동 불가능한 지형과 충돌할 것으로 예상될 때 표시할 이미지입니다.")]
    public Sprite wallCollisionSprite;

    [FormerlySerializedAs("waterDefeatSprite")]
    [Tooltip("대상이 물로 밀려나 즉시 처치될 것으로 예상될 때 표시할 이미지입니다.")]
    public Sprite waterDefeatSprite;

    [Header("결과별 색상")]
    [FormerlySerializedAs("movedColor")]
    [Tooltip("정상적으로 밀려나는 결과 이미지에 적용할 색상입니다.")]
    public Color successfulPushColor = new Color(0.3f, 1f, 0.35f, 1f);

    [FormerlySerializedAs("resistedColor")]
    [Tooltip("밀치기에 저항한 결과 이미지에 적용할 색상입니다.")]
    public Color pushResistedColor = new Color(0.65f, 0.7f, 0.75f, 1f);

    [Tooltip("다른 적과 충돌하는 결과 이미지에 적용할 색상입니다.")]
    public Color enemyCollisionColor = new Color(1f, 0.8f, 0.15f, 1f);

    [Tooltip("벽이나 이동 불가능한 지형과 충돌하는 결과 이미지에 적용할 색상입니다.")]
    public Color wallCollisionColor = new Color(1f, 0.4f, 0.1f, 1f);

    [Tooltip("물로 밀려나 처치되는 결과 이미지에 적용할 색상입니다.")]
    public Color waterDefeatColor = new Color(0.2f, 0.75f, 1f, 1f);

    [Header("화면 배치")]
    [FormerlySerializedAs("iconSize")]
    [Min(8f)]
    [Tooltip("화면에 생성되는 밀치기 결과 아이콘의 가로·세로 크기입니다.")]
    public float previewIconSize = 72f;

    [FormerlySerializedAs("targetWorldOffset")]
    [Tooltip("대상 Transform의 월드 위치에서 아이콘을 얼마나 위·옆으로 띄울지 정하는 오프셋입니다.")]
    public Vector3 previewWorldOffset = new Vector3(0f, 2.2f, 0f);

    /// <summary>
    /// 밀치기 계산 결과에 대응하는 표시 이미지를 반환한다.
    /// 결과가 없거나 아직 지원하지 않는 값이면 미리보기를 만들지 않도록 null을 반환한다.
    /// </summary>
    public Sprite GetSprite(BattleCardMovementService.PushResult result)
    {
        switch (result)
        {
            case BattleCardMovementService.PushResult.Moved: return successfulPushSprite;
            case BattleCardMovementService.PushResult.Resisted: return pushResistedSprite;
            case BattleCardMovementService.PushResult.EnemyCollision: return enemyCollisionSprite;
            case BattleCardMovementService.PushResult.WallCollision: return wallCollisionSprite;
            case BattleCardMovementService.PushResult.WaterDefeat: return waterDefeatSprite;
            default: return null;
        }
    }

    /// <summary>
    /// 밀치기 계산 결과에 대응하는 이미지 색상을 반환한다.
    /// 정의되지 않은 결과는 원본 이미지 색상을 유지할 수 있도록 흰색을 반환한다.
    /// </summary>
    public Color GetColor(BattleCardMovementService.PushResult result)
    {
        switch (result)
        {
            case BattleCardMovementService.PushResult.Moved: return successfulPushColor;
            case BattleCardMovementService.PushResult.Resisted: return pushResistedColor;
            case BattleCardMovementService.PushResult.EnemyCollision: return enemyCollisionColor;
            case BattleCardMovementService.PushResult.WallCollision: return wallCollisionColor;
            case BattleCardMovementService.PushResult.WaterDefeat: return waterDefeatColor;
            default: return Color.white;
        }
    }
}
