from scene_compare import *
import hashlib
src=BASE/'yeop.unity';dst=BASE/'moon_branch Jeon Yong.unity'
a=load(src.name);b=load(dst.name);raw=dst.read_bytes();source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
p=json.load(open('work/scene_merge_plan.json'));mapping=p['mapping'];added=p['added'];S,D=a[1],dict(b[1])
def remap(block):
 block=re.sub(r'(^--- !u!\d+ &)(\d+)',lambda m:m[1]+mapping[m[2]],block,flags=re.M)
 return re.sub(r'\{fileID: (-?\d+)\}',lambda m:'{fileID: '+mapping[m[1]]+'}',block)
for k in added:D[mapping[k]]=remap(S[k])
newnodes=[k for k in a[3] if k in added]
roots=[]
for k in newnodes:
 go,parent=a[3][k]
 if parent=='0':roots.append(mapping[k]);continue
 if parent in added:continue # copied parent already includes children
 dp=mapping[parent]
 assert b[2][dp] in ('4','224'),('unexpected prefab parent',k,parent)
 child=mapping[k]
 block=D[dp]
 if '  m_Children: []' in block:block=block.replace('  m_Children: []','  m_Children:\n  - {fileID: '+child+'}')
 else:block=block.replace('  m_Children:\n','  m_Children:\n  - {fileID: '+child+'}\n',1)
 D[dp]=block
for dg,c,_,_ in p['additions']:
 D[dg]=D[dg].replace('  m_Component:\n','  m_Component:\n  - component: {fileID: '+mapping[c]+'}\n',1)
rid=next(k for k,v in b[2].items() if v=='1660057539')
D[rid]=D[rid].rstrip()+'\n'+''.join('  - {fileID: '+k+'}\n' for k in roots)
# Preserve every existing block except explicit hierarchy additions; insert new records before SceneRoots.
header=b[0].split('---',1)[0]
result=header+''.join(D[k] for k in b[1] if k!=rid)+''.join(D[mapping[k]] for k in added)+D[rid]
blocks={m[2]:m[0] for m in re.finditer(r'^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^---|\Z)',result,re.M|re.S)}
assert len(blocks)==len(b[1])+len(added)
def dangling(bs):return {(k,r) for k,v in bs.items() for r in re.findall(r'\{fileID: (-?\d+)\}',v) if r!='0' and r not in bs}
assert not (dangling(blocks)-dangling(b[1])),dangling(blocks)-dangling(b[1])
# All imported external asset GUIDs must exist in project or package metadata.
guids={g for k in added for g in re.findall(r'guid: ([a-f0-9]{32})',S[k]) if not g.startswith('0000000000000000')}
found={}
for root in [pathlib.Path('Assets'),pathlib.Path('Library/PackageCache')]:
 for meta in root.rglob('*.meta'):
  try:
   mt=meta.read_text(encoding='utf-8-sig');match=re.search(r'^guid: (\w+)',mt,re.M)
   if match and match[1] in guids:found[match[1]]=str(meta)
  except (OSError,UnicodeError):pass
assert not guids-found.keys(),guids-found.keys()
assert dst.read_bytes()==raw,'Target changed during merge'
pathlib.Path('work/moon_branch_Jeon_Yong.before_yeop_merge.unity').write_bytes(raw)
dst.write_bytes(result.replace('\n','\r\n').encode('utf-8'))
assert hashlib.sha256(src.read_bytes()).hexdigest()==source_hash
report={'addedNodes':[a[4][k] for k in newnodes],'addedRoots':len(roots),'addedBlocks':len(added),'newDanglingReferences':0,'sourceUnchanged':True,'assets':found}
pathlib.Path('work/yeop_merge_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=True,indent=2))
