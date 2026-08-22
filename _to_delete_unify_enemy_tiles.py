# -*- coding: utf-8 -*-

def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8").replace("\r\n", "\n")

def save(path, content):
    with open(path, "wb") as f:
        f.write(content.replace("\n", "\r\n").encode("utf-8"))

def apply(path, replacements):
    content = load(path)
    for i, (old, new) in enumerate(replacements, start=1):
        count = content.count(old)
        assert count == 1, (path, i, count, old[:80])
        content = content.replace(old, new, 1)
    save(path, content)
    print("OK:", path, "->", len(replacements), "replacements")

# ---------------------------------------------------------------------------
# 1) BattlePlayerRangeController.cs
# ---------------------------------------------------------------------------
p1 = "Assets/renew/Battle/Player/BattlePlayerRangeController.cs"

old_class_doc = '''/// <summary>
/// Player 이동·공격 범위 집합을 생성하고 우선순위에 따라 타일 색상을 표시한다.
/// 입력 처리와 행동 확정은 담당하지 않는다.
///
/// 두 가지 독립된 표시 모드를 갖는다: (1) `BuildAndShow` — Player 자신의 이동/공격 범위를
/// 우선순위 색상(점유 차단 > 이동가능 > 공격가능 > Enemy 탐지권)으로 표시, (2) `BuildAndShowEnemyThreatRange`
/// — R 단축키 전용으로 활성 Enemy 전체가 이번 턴에 위협 가능한 타일을 한 가지 색으로 따로 표시.
/// 두 결과는 서로 다른 필드(reachableTiles/attackableTiles vs enemyThreatTiles)에 저장되고 섞이지 않는다.
/// </summary>'''
new_class_doc = '''/// <summary>
/// Player 이동·공격 범위 집합을 생성하고 우선순위에 따라 타일 색상을 표시한다.
/// 입력 처리와 행동 확정은 담당하지 않는다.
///
/// 두 가지 독립된 표시 모드를 갖는다: (1) `BuildAndShow` — Player 자신의 이동/공격 범위를
/// 우선순위 색상(점유 차단 > 이동가능 > 공격가능 > Enemy 탐지권)으로 표시, (2) `BuildAndShowEnemyThreatRange`
/// — R 단축키 전용으로 활성 Enemy 전체가 이번 턴에 위협 가능한 타일을 한 가지 색으로 따로 표시.
/// 두 결과는 서로 다른 필드(reachableTiles/attackableTiles vs enemyThreatTiles)에 저장되고 섞이지 않는다.
/// "Enemy가 어느 타일에 서 있는가"는 두 모드 모두 occupiedEnemyTiles(RefreshOccupiedEnemyTiles가 채움) 하나만
/// 참조하도록 통일했다 — 예전에는 BuildAndShowEnemyThreatRange가 findClosestTile로 매번 따로 계산해서
/// 두 표시가 서로 다른 소스를 참조했었다.
/// </summary>'''

old_build2_block = '''        // 다른 Enemy가 서 있는 타일은 이동 계산에서 막되, 계산 대상 본인의 타일은 막지 않는다.
        HashSet<MapInfo> allEnemyTiles = new HashSet<MapInfo>();
        foreach (GameObject enemyObject in activeEnemies)
        {
            MapInfo tile = findClosestTile(enemyObject.transform.position);
            // R 위협 범위는 최하단 정보다. 이동·공격 가능 타일의 색을 덮지 않는다.
            if (tile != null && !reachableTiles.Contains(tile) && !attackableTiles.Contains(tile))
            {
                allEnemyTiles.Add(tile);
            }
        }'''
new_build2_block = '''        // 다른 Enemy가 서 있는 타일은 이동 계산에서 막되, 계산 대상 본인의 타일은 막지 않는다.
        // occupiedEnemyTiles(RefreshOccupiedEnemyTiles가 채운, BuildAndShow와 동일한 소스)를 그대로 써서
        // "Enemy가 어느 타일에 서 있는가"를 두 표시 모드가 서로 다르게 계산하지 않도록 했다
        // (호출부인 BattlePlayerActionController.ShowEnemyThreatRange가 이 메서드를 부르기 전에
        // RefreshOccupiedEnemyTiles를 먼저 호출해서 채워둔다).
        HashSet<MapInfo> allEnemyTiles = new HashSet<MapInfo>();
        foreach (MapInfo tile in occupiedEnemyTiles)
        {
            // R 위협 범위는 최하단 정보다. 이동·공격 가능 타일의 색을 덮지 않는다.
            if (tile != null && !reachableTiles.Contains(tile) && !attackableTiles.Contains(tile))
            {
                allEnemyTiles.Add(tile);
            }
        }'''

old_method_doc = '''    /// 주의(별도 확인 필요, 아직 통일 안 됨): 여기서 "Enemy가 서 있는 타일"은 이 메서드 안에서 매번
    /// findClosestTile로 새로 계산하는 allEnemyTiles이고, BuildAndShow가 쓰는 occupiedEnemyTiles(Registry의
    /// FillOccupiedTiles 결과)와는 별개의 계산이다. ActionController.ShowEnemyThreatRange()는 이 메서드를 부르기
    /// 전에 RefreshOccupiedEnemyTiles()를 호출하지 않으므로, "이동 범위 볼 때 보이는 Enemy 점유 표시"와
    /// "R로 위협 범위 볼 때 내부적으로 쓰는 Enemy 위치 판정"이 서로 다른 경로로 계산된다.
    /// </summary>'''
new_method_doc = '''    /// (2026-08-22 통일 완료) "Enemy가 서 있는 타일"은 occupiedEnemyTiles(RefreshOccupiedEnemyTiles가 채운,
    /// BuildAndShow와 동일한 Registry 우선 소스) 하나만 참조한다. 호출부(ActionController.ShowEnemyThreatRange)가
    /// 이 메서드를 부르기 전에 RefreshOccupiedEnemyTiles()를 먼저 호출해서 채워둬야 한다(BuildAndShow와 동일한 계약).
    /// </summary>'''

apply(p1, [
    (old_class_doc, new_class_doc),
    (old_build2_block, new_build2_block),
    (old_method_doc, new_method_doc),
])

# ---------------------------------------------------------------------------
# 2) BattlePlayerActionController.cs - caller must refresh occupiedEnemyTiles first
# ---------------------------------------------------------------------------
p2 = "Assets/renew/Battle/Player/BattlePlayerActionController.cs"

old_show_threat = '''    internal void ShowEnemyThreatRange(bool preserveMoveRange = false)
    {
        RefreshMapTiles();
        EnsureBattlePlayerRangeController();
        if (!preserveMoveRange)
        {
            RestoreAllTileColors();
            battlePlayerRangeController.ClearState();
        }
        ResolveBattleDataPool();

        IEnumerable<GameObject> enemies = battleDataPool != null && battleDataPool.Units != null
            ? battleDataPool.Units.Enemies
            : null;
        bool shown = battlePlayerRangeController.BuildAndShowEnemyThreatRange('''
new_show_threat = '''    internal void ShowEnemyThreatRange(bool preserveMoveRange = false)
    {
        RefreshMapTiles();
        EnsureBattlePlayerRangeController();
        if (!preserveMoveRange)
        {
            RestoreAllTileColors();
            battlePlayerRangeController.ClearState();
        }
        ResolveBattleDataPool();
        // BuildAndShow와 동일하게, R 위협 범위 표시도 occupiedEnemyTiles를 먼저 갱신해둬야 두 표시 모드가
        // 같은 소스로 "Enemy가 어느 타일에 서 있는가"를 판단한다(BattlePlayerRangeController 쪽 통일 작업과 짝).
        battlePlayerRangeController.RefreshOccupiedEnemyTiles(battleDataPool, FindClosestMapTile);

        IEnumerable<GameObject> enemies = battleDataPool != null && battleDataPool.Units != null
            ? battleDataPool.Units.Enemies
            : null;
        bool shown = battlePlayerRangeController.BuildAndShowEnemyThreatRange('''

apply(p2, [
    (old_show_threat, new_show_threat),
])

print("ALL DONE")
