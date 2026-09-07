from scene_compare import *
x=load('moon_branch Jeon Yong.unity');manifest=json.load(open('work/stage_manifest.json'));old=pathlib.Path('work/stage_scene_before.unity').read_text(encoding='utf-8-sig');ob={m[2]:m[0] for m in re.finditer(r'^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^---|\Z)',old,re.M|re.S)}
node=manifest['sceneComponent'];p=BASE/'moon_branch Jeon Yong.unity';s=p.read_text(encoding='utf-8-sig');block=x[1][node];clean=block.replace('  m_Name: \n','  m_Name:\n').replace('  m_EditorClassIdentifier: \n','  m_EditorClassIdentifier:\n');s=s.replace(block,clean);p.write_bytes(s.encode('utf-8'))
x=load(p.name)
assert x[1][manifest['map']]==ob[manifest['map']], 'Map settings changed'
def dangling(bs):return {(k,r) for k,v in bs.items() for r in re.findall(r'\{fileID: (-?\d+)\}',v) if r!='0' and r not in bs}
assert not dangling(x[1])-dangling(ob)
assert 'useStagePlacement: 1' in x[1][manifest['flow']]
assert 'stageSpawner: {fileID: '+node+'}' in x[1][manifest['flow']]
for g in manifest['assets']:assert g in x[1][node]
print('PASS: scene references, all 4 data links, MapGenerator block unchanged.')
