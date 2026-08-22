# -*- coding: utf-8 -*-
path = "Assets/renew/Battle/Player/BattlePlayerActionController.cs"

with open(path, "rb") as f:
    raw = f.read()

content = raw.decode("utf-8")
content = content.replace("\r\n", "\n")

fixes = []

# Fix 1: SetMoveRange's leftover ShowMoveRange() call
fixes.append((
'''    public void SetMoveRange(int moveRange)
    {
        currentMoveRange = Mathf.Clamp(moveRange, minMoveRange, maxMoveRange);
        turnActionState.MarkDiceRolled();
        Debug.Log($"이동 범위 설정: {currentMoveRange}칸", this);
        ShowMoveRange();
    }''',
'''    public void SetMoveRange(int moveRange)
    {
        currentMoveRange = Mathf.Clamp(moveRange, minMoveRange, maxMoveRange);
        turnActionState.MarkDiceRolled();
        Debug.Log($"이동 범위 설정: {currentMoveRange}칸", this);
        EnsureMoveFlow();
        moveFlow.ShowMoveRange();
    }'''
))

# Fix 2: remove orphaned field declarations now owned by BattleUnitMoveFlow
fixes.append((
'''    [InspectorName("일반 이동 기능 모듈")]
    [SerializeField] private BattlePlayerMoveTransaction battleMoveTransaction;
''',
''
))
fixes.append((
'''    [InspectorName("이동 목적지 프리뷰 모듈")]
    [SerializeField] private BattleMovePreview battleMovePreview;
''',
''
))
fixes.append((
'''    [InspectorName("이동 타일 Enemy 위협 연결선")]
    [SerializeField] private BattleMoveThreatPreview battleMoveThreatPreview;
''',
''
))

for i, (old, new) in enumerate(fixes, start=1):
    count = content.count(old)
    assert count == 1, (i, count, old[:80])
    content = content.replace(old, new, 1)

content = content.replace("\n", "\r\n")
with open(path, "wb") as f:
    f.write(content.encode("utf-8"))
print("OK fixes:", len(fixes))
