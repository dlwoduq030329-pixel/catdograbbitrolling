import pathlib,re,json,uuid,hashlib
from scene_compare import load,BASE
folder=BASE/'Battle/Stage'
def meta(path,kind):
 p=pathlib.Path(str(path)+'.meta')
 if p.exists():return re.search(r'^guid: (\w+)',p.read_text(),re.M)[1]
 g=uuid.uuid4().hex
 body='fileFormatVersion: 2\nguid: '+g+'\n'
 if kind=='folder':body+='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
 elif kind=='script':body+='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
 else:body+='NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
 p.write_text(body,encoding='utf-8');return g
meta(folder,'folder')
sg={p.stem:meta(p,'script') for p in folder.glob('*.cs')}
stages=[(1,5,['green_slime','red_slime','blue_slime','goblin'],0,[],0,[]),
 (2,6,['green_slime','red_slime','blue_slime','goblin','goblin_mage'],1,['orc'],0,[]),
 (3,8,['red_slime','blue_slime','goblin','goblin_mage'],2,['orc','werewolf'],0,[]),
 (4,0,[],0,[],1,['minotaur'])]
ags=[]
for n,nc,ni,ec,ei,bc,bi in stages:
 p=folder/f'Stage{n:02d}.asset';assert not p.exists(),p
 text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
 text+=f'  m_Script: {{fileID: 11500000, guid: {sg["StageObjData"]}, type: 3}}\n  m_Name: Stage{n:02d}\n  m_EditorClassIdentifier: \n  stageNumber: {n}\n  enemyDatabase: {{fileID: 11400000, guid: ce0f2132c8fa5264d8c778e5f2bd3565, type: 2}}\n'
 for name,c,ids in [('normalEnemies',nc,ni),('eliteEnemies',ec,ei),('bossEnemies',bc,bi)]:
  text+=f'  {name}:\n    count: {c}\n    enemyIds:'+ ('\n'+''.join('    - '+id+'\n' for id in ids) if ids else ' []\n')
 text+='  safeRadiusTiles: 1\n';p.write_text(text,encoding='utf-8');ags.append(meta(p,'asset'))
scene=BASE/'moon_branch Jeon Yong.unity';raw=scene.read_bytes();x=load(scene.name);bs=x[1]
def component(guid):
 matches=[k for k,b in bs.items() if x[2][k]=='114' and 'guid: '+guid in b];assert len(matches)==1;return matches[0]
enemy=component('ab167392e7188154dbe4dfd87627c2c9');flow=component('7da83bca325d7a345993294439ff97df');mp=component('d92c59f01e2fae6489ee3aa5d1d367db')
go=re.search(r'm_GameObject: \{fileID: (\d+)\}',bs[enemy])[1]
newid='8000000000000000100';assert newid not in bs
oldblocks=dict(bs)
bs[go]=bs[go].replace('  m_Component:\n','  m_Component:\n  - component: {fileID: '+newid+'}\n',1).replace('  m_Name: EnemySpawner','  m_Name: Stage Spawner')
bs[flow]=bs[flow].replace('  enemySpawner: {fileID: '+enemy+'}', '  enemySpawner: {fileID: '+enemy+'}\n  useStagePlacement: 1\n  stageSpawner: {fileID: '+newid+'}')
b=f'--- !u!114 &{newid}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {sg["StageSpawner"]}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n  stages:\n'
b+=''.join('  - {fileID: 11400000, guid: '+g+', type: 2}\n' for g in ags)
b+=f'  initialStageNumber: 1\n  mapGenerator: {{fileID: {mp}}}\n  enemySpawner: {{fileID: {enemy}}}\n'
rid=next(k for k in bs if x[2][k]=='1660057539')
result=x[0].split('---',1)[0]+''.join(v for k,v in bs.items() if k!=rid)+b+bs[rid]
assert scene.read_bytes()==raw
pathlib.Path('work/stage_scene_before.unity').write_bytes(raw)
scene.write_bytes(result.encode('utf-8'))
assert bs[mp]==oldblocks[mp], 'MapGenerator block changed'
pathlib.Path('work/stage_manifest.json').write_text(json.dumps({'scripts':sg,'assets':ags,'sceneComponent':newid,'map':mp,'enemy':enemy,'flow':flow,'go':go},indent=2))
print('4 Stage assets created; StageSpawner attached to existing EnemySpawner object; UIFlow connected; MapGenerator block unchanged.')
