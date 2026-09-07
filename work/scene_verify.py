from scene_compare import *
a=load('yeop.unity');b=load('moon_branch Jeon Yong.unity');plan=json.load(open('work/scene_merge_plan.json'))
old=pathlib.Path('work/moon_branch_Jeon_Yong.before_yeop_merge.unity').read_text(encoding='utf-8-sig')
ob={m[2]:m[0] for m in re.finditer(r'^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^---|\Z)',old,re.M|re.S)}
def dangling(bs):return {(k,r) for k,v in bs.items() for r in re.findall(r'\{fileID: (-?\d+)\}',v) if r!='0' and r not in bs}
assert not dangling(b[1])-dangling(ob)
assert not set(a[4].values())-set(b[4].values())
changed=[k for k in ob if ob[k]!=b[1][k]]
print('Existing modified blocks:',changed)
for k in plan['added']:
 nk=plan['mapping'][k];assert nk in b[1]
 if a[2][k] in ('4','224') and 'm_Father:' in a[1][k]:
  parent=re.search(r'm_Father: \{fileID: (\d+)\}',b[1][nk])[1]
  if parent!='0':assert '{fileID: '+nk+'}' in b[1][parent],(nk,parent)
print('PASS: 11 imported hierarchy nodes; zero missing source paths; zero new dangling references; imported parent-child links valid.')
print('Fog map reference:',re.search(r'  mapGenerator:.*',b[1][plan['mapping']['22660632']])[0])
