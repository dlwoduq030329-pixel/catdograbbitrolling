from pathlib import Path
import re
p=Path('Assets/renew/Battle/Enemy/EnemySpawner.cs')
s=p.read_text(encoding='utf-8-sig')
def take_method(name):
    global s
    match=re.search(r'^    private (?:static )?[^\n]+ '+name+r'\([^\n]*\)\n    \{',s,re.M)
    assert match,name
    start=match.start(); brace=s.index('{',match.start()); depth=1; end=brace+1
    while depth:
        if s[end]=='{': depth+=1
        elif s[end]=='}': depth-=1
        end+=1
    method=s[start:end]
    # Remove the XML summary immediately attached to this method too.
    prefix=s[:start]
    if prefix.rstrip().endswith('/// </summary>'):
        start=prefix.rfind('    /// <summary>')
    s=s[:start]+s[end:]
    return method
methods=[take_method(x) for x in ['EnsureEnemyCollider','SafeDivide','NormalizeEnemyFootprint','TryGetVisualBounds','TryGetTileBounds']]
methods[0]=methods[0].replace('private static void EnsureEnemyCollider','public static void EnsureCollider')
methods[2]=methods[2].replace('private void NormalizeEnemyFootprint(GameObject enemy, Transform tile)',
    'public static void Fit(GameObject enemy, Transform tile, float enemyTileFillRatio, bool allowEnemyUpscaling, float minimumScaleMultiplier)')
methods[2]=methods[2].replace('!normalizeEnemyToTile || ','')
# Preserve the calculations, replace the historical commentary with a compact explanation.
methods=[re.sub(r'^\s*//[^\n]*\n','',m,flags=re.M) for m in methods]
s=s.replace('        NormalizeEnemyFootprint(enemy, enemyTile);',
    '        if (normalizeEnemyToTile)\n            EnemySpawnGeometry.Fit(enemy, enemyTile, enemyTileFillRatio, allowEnemyUpscaling, minimumScaleMultiplier);')
s=s.replace('        EnsureEnemyCollider(enemy);','        EnemySpawnGeometry.EnsureCollider(enemy);')
p.write_text(s,encoding='utf-8',newline='\n')
Path('Assets/renew/Battle/Stage/EnemySpawnGeometry.cs').write_text(
    'using UnityEngine;\n\n/// <summary>최초 풀 생성 시 모델 크기·발 높이·클릭 Collider를 맞춘다. 대여 때는 재계산하지 않는다.</summary>\npublic static class EnemySpawnGeometry\n{\n'+
    '\n\n'.join(methods)+'\n}\n',encoding='utf-8')
print('Extracted geometry; spawn source lines:',len(s.splitlines()))
