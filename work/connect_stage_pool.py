from pathlib import Path
import re, uuid

root=Path.cwd()
scene=root/'Assets/renew/moon_branch Jeon Yong.unity'
s=scene.read_text(encoding='utf-8-sig')
def block(text, ident):
    m=re.search(r'^--- !u!\d+ &'+str(ident)+r'[^\n]*\n.*?(?=^---|\Z)',text,re.M|re.S)
    assert m, ident
    return m.group(0)
def guid(name):
    p=root/f'Assets/renew/Battle/Stage/{name}.cs.meta'
    if not p.exists():
        p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
    return re.search(r'guid: (\w+)',p.read_text())[1]
pool=8000000000000000101
transition=8000000000000000102
go=320417157
go_old=block(s,go)
go_new=go_old
for ident in (pool,transition):
    if f'component: {{fileID: {ident}}}' not in go_new:
        go_new=go_new.replace('  m_Layer:',f'  - component: {{fileID: {ident}}}\n  m_Layer:',1)
s=s.replace(go_old,go_new)
old=block(s,320417159)
new=re.sub(r'^  enemyPool: \{fileID: 0\}$',f'  enemyPool: {{fileID: {pool}}}',old,flags=re.M) if '  enemyPool:' in old else old.rstrip()+f'\n  enemyPool: {{fileID: {pool}}}\n'
s=s.replace(old,new)
installer=block(s,1943288761)
camera=re.search(r'  battleCamera: (\{[^}]+\})',installer)[1]
def component(ident,name,fields):
    return f'''--- !u!114 &{ident}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid(name)}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
{fields}'''
added=''
if not re.search(r'^--- !u!114 &'+str(pool)+r'$',s,re.M):
    added+=component(pool,'EnemyPool','  normalPerType: 5\n  elitePerType: 2\n  bossPerType: 1\n')
if not re.search(r'^--- !u!114 &'+str(transition)+r'$',s,re.M):
    added+=component(transition,'StageTransitionController',f'''  stageSpawner: {{fileID: 8000000000000000100}}
  mapGenerator: {{fileID: 2147000020}}
  battleGameManager: {{fileID: 1943288765}}
  mapRegistry: {{fileID: 1943288762}}
  playerActions: {{fileID: 1263296889}}
  moveFlow: {{fileID: 0}}
  loadingUI: {{fileID: 1260140037}}
  battleCamera: {camera}
  fog: {{fileID: 8000000000000000004}}
  fadeSeconds: 0.35
  finalStageExited:
    m_PersistentCalls:
      m_Calls: []
''')
s+=added
scene.write_text(s,encoding='utf-8',newline='\n')
print('Connected fixed enemy pool and portal transition in integration scene only.')
