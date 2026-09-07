from pathlib import Path
import re
p=Path('Assets/renew/moon_branch Jeon Yong.unity')
s=p.read_text(encoding='utf-8-sig')
blocks={}
for m in re.finditer(r'^--- !u!\d+ &(\d+)[^\n]*\n.*?(?=^---|\Z)',s,re.M|re.S):
    assert m[1] not in blocks, 'duplicate Unity fileID '+m[1]
    blocks[m[1]]=m[0]
for ident in ['8000000000000000100','8000000000000000101','8000000000000000102']:
    b=blocks[ident]
    go=re.search(r'm_GameObject: \{fileID: (\d+)\}',b)[1]
    assert 'component: {fileID: '+ident+'}' in blocks[go]
    for field,ref in re.findall(r'^  (\w+): \{fileID: (\d+)\}$',b,re.M):
        if ref!='0': assert ref in blocks,(field,ref)
for name in ['EnemyPool','StageTransitionController','EnemyRuntimeFactory','EnemySpawnGeometry','StageEnemyBridge']:
    meta=Path('Assets/renew/Battle/Stage/'+name+'.cs.meta')
    assert meta.exists()
assert 'enemyPool: {fileID: 8000000000000000101}' in blocks['320417159']
assert 'normalPerType: 5\n  elitePerType: 2\n  bossPerType: 1' in blocks['8000000000000000101']
transition=Path('Assets/renew/Battle/Stage/StageTransitionController.cs').read_text(encoding='utf-8-sig')
assert 'RegisterPlayer(' not in transition and 'StartPlayerTurn(' not in transition
assert 'playerHeightOffset' in transition and 'finally' in transition
print('Scene component ownership, direct references, pool capacities and player-preservation entry points verified.')
