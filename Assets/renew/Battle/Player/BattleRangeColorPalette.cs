using UnityEngine;

/// <summary>
/// 이동/공격/카드 범위 표시에 쓰이는 색상 9종을 모아둔 공유 데이터 에셋이다.
/// 색상 로직(BattleRangeVisualizer)이나 계산(BattlePlayerRangeController)은 담당하지 않고,
/// 오직 "어떤 색을 쓸지"만 갖고 있다. BattlePlayerActionController가 이 에셋 하나를 참조해서
/// 필요한 색상을 꺼내 쓰며, 같은 팔레트를 다른 Scene/컨트롤러에서도 그대로 재사용할 수 있다.
/// </summary>
[CreateAssetMenu(fileName = "BattleRangeColorPalette", menuName = "Battle/Range Color Palette")]
public sealed class BattleRangeColorPalette : ScriptableObject
{
    [Header("이동 범위 색상")]
    [InspectorName("이동 가능 타일 색상")]
    [SerializeField] private Color movableTileColor = new Color(0.25f, 0.9f, 0.25f, 1f);
    [InspectorName("이동 불가 타일 색상")]
    [SerializeField] private Color blockedTileColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [InspectorName("적 감지 범위 타일 색상")]
    [SerializeField] private Color enemyDetectColor = new Color(1f, 0.6f, 0.15f, 1f);
    [InspectorName("선택 타일 색상")]
    [SerializeField] private Color selectedTileColor = new Color(1f, 0.95f, 0.35f, 1f);
    [InspectorName("도착 타일 색상")]
    [SerializeField] private Color landedTileColor = new Color(0.55f, 1f, 0.85f, 1f);
    [InspectorName("이동 후 공격 가능 타일 색상")]
    [SerializeField] private Color attackableTileColor = new Color(0.9f, 0.2f, 0.25f, 1f);
    [InspectorName("카드 사용 가능 타일 색상")]
    [SerializeField] private Color cardRangeTileColor = new Color(0.35f, 0.45f, 1f, 1f);
    [InspectorName("카드 실제 효과 범위 색상")]
    [SerializeField] private Color cardEffectAreaTileColor = new Color(0.15f, 0.9f, 0.95f, 1f);
    [InspectorName("R 토글 - 적 위협 범위 색상")]
    [SerializeField] private Color enemyThreatTileColor = new Color(0.75f, 0.15f, 0.85f, 1f);

    public Color MovableTileColor => movableTileColor;
    public Color BlockedTileColor => blockedTileColor;
    public Color EnemyDetectColor => enemyDetectColor;
    public Color SelectedTileColor => selectedTileColor;
    public Color LandedTileColor => landedTileColor;
    public Color AttackableTileColor => attackableTileColor;
    public Color CardRangeTileColor => cardRangeTileColor;
    public Color CardEffectAreaTileColor => cardEffectAreaTileColor;
    public Color EnemyThreatTileColor => enemyThreatTileColor;
}
