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

p = "Assets/renew/Battle/Rewards/BattleChestRewardSystem.cs"
apply(p, [
    (
'''[DisallowMultipleComponent]
public sealed class BattleChestRewardSystem : MonoBehaviour''',
'''/// <summary>
/// 맵의 보상 상자(Box 타일)를 열고, 카드/장비/골드 중 하나를 무작위로 지급하는 UI·상태를 담당한다.
/// 타일별로 한 번 결정된 보상은 <c>pendingRewards</c>에 저장돼 다시 열어도(문 닫고 다시 클릭 등) 같은
/// 보상을 유지한다. 상자가 열려 있는 동안은 <c>BattleGameManager.LockBattleInputForOverlay</c>로
/// 뒤쪽 전투 조작을 잠그고, 닫히면 <c>UnlockBattleInputAfterOverlay</c>로 되돌린다(Player 사망 시에는
/// <see cref="ForceClose"/>로 강제 정리).
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleChestRewardSystem : MonoBehaviour'''
    ),
    (
'''    public bool TryOpen(MapInfo tile)
    {
        if (tile == null || tile.Type != TileType.Box || openedTiles.Contains(tile) || currentTile != null)''',
'''    /// <summary>
    /// Box 타일을 클릭했을 때 호출한다. 이미 다른 상자가 열려 있거나(currentTile != null), 대상이
    /// Box 타일이 아니거나, 이미 연 상자면 즉시 false를 반환하고 아무 것도 하지 않는다.
    /// 성공하면 보상을 결정(또는 이전에 결정된 보상을 재사용)하고 UI를 표시한 뒤 입력을 잠근다.
    /// </summary>
    public bool TryOpen(MapInfo tile)
    {
        if (tile == null || tile.Type != TileType.Box || openedTiles.Contains(tile) || currentTile != null)'''
    ),
    (
'''    public void ForceClose()
    {
        Close();
    }''',
'''    /// <summary>
    /// Player 사망 등으로 전투가 즉시 정지될 때 <c>BattleGameManager</c>가 호출한다.
    /// 열려 있는 보상 상자 UI와 입력 잠금을 정상 닫기(Close)와 동일하게 정리한다.
    /// </summary>
    public void ForceClose()
    {
        Close();
    }'''
    ),
])
