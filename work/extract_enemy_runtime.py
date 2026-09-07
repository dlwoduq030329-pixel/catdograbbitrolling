from pathlib import Path
import re
p=Path('Assets/renew/Battle/Enemy/EnemySpawner.cs')
s=p.read_text(encoding='utf-8-sig')
a=s.index('        EnemyDetector detector =',s.index('private GameObject CreateEnemy'))
b=s.index('        return enemy;',a)
body=s[a:b]
body=body.replace('deathHandler.Configure(enemyHealth, unitRegistry, () => ReleaseEnemy(enemy));',
                  'deathHandler.Configure(enemyHealth, units, () => release(enemy));')
body=body.replace('enemy.transform.position - enemyTile.position','spawnOffset')
body=body.replace('        Sprite typeIcon = selectedData != null ? ResolveTypeIcon(selectedData) : null;\n','')
body=body.replace('turnActor.ResetForSpawn(dataPool)','turnActor.ResetForSpawn(shared)')
body=re.sub(r'^\s*//[^\n]*\n','',body,flags=re.M)
Path('Assets/renew/Battle/Stage/EnemyRuntimeFactory.cs').write_text('''using System;
using UnityEngine;

/// <summary>풀에 최초 생성하는 Enemy의 전투 컴포넌트와 UI를 조립한다.</summary>
public static class EnemyRuntimeFactory
{
    public static void Configure(GameObject enemy, BattleEnemyData selectedData, Sprite typeIcon,
        BattleUnitRegistry units, BattleDataPool shared, Action<GameObject> release, Vector3 spawnOffset)
    {
'''+body+'    }\n}\n',encoding='utf-8')
s=s[:a]+'''        EnemyRuntimeFactory.Configure(enemy, selectedData, ResolveTypeIcon(selectedData),
            unitRegistry, dataPool, ReleaseEnemy, enemy.transform.position - enemyTile.position);
'''+s[b:]
# Keep inspector tooltips and code; remove superseded historical explanations.
s=re.sub(r'^\s*///[^\n]*\n','',s,flags=re.M)
s=re.sub(r'^\s*//[^\n]*\n','',s,flags=re.M)
s=re.sub(r'\n{3,}','\n\n',s)
p.write_text(s,encoding='utf-8')
print('Separated runtime assembly; EnemySpawner lines:',len(s.splitlines()))
