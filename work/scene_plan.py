from scene_compare import *
a=load('yeop.unity');b=load('moon_branch Jeon Yong.unity')
S,D=a[1],b[1]; mapping={'0':'0','1':'1','2':'2','3':'3','4':'4'}; added=set(); additions=[]
def sig(block):
 kind=re.match(r'--- !u!(\d+)',block)[1]
 script=re.search(r'm_Script:.*guid: (\w+)',block)
 return kind+(':'+script[1] if script else '')
def comps(block):return re.findall(r'component: \{fileID: (\d+)\}',block)
bpaths=collections.defaultdict(list)
for k,p in b[4].items():bpaths[p].append(k)
for k,p in a[4].items():
 matches=bpaths[p]
 if len(matches)>1:raise Exception('Ambiguous path '+p)
 if not matches:continue
 dk=matches[0]
 if a[2][k]!=b[2][dk]:
  if p=='/WaterPlane' and a[2][k]=='1001':
   print('PRESERVE existing unpacked WaterPlane');mapping[k]=dk;continue
  raise Exception('Different node types '+p)
 mapping[k]=dk
 if a[2][k]=='1001':
  assert re.search(r'm_SourcePrefab:.*guid: (\w+)',S[k])[1]==re.search(r'm_SourcePrefab:.*guid: (\w+)',D[dk])[1],p
  continue
 sg=a[3][k][0];dg=b[3][dk][0];mapping[sg]=dg
 available=collections.defaultdict(list)
 for c in comps(D[dg]):available[sig(D[c])].append(c)
 for c in comps(S[sg]):
  candidates=available[sig(S[c])]
  if candidates:mapping[c]=candidates.pop(0)
  else:added.add(c);additions.append((dg,c,p,sig(S[c])))
# Stripped objects identify the exact prefab source object, not their display names.
for k,block in S.items():
 if 'stripped' not in block.splitlines()[0]:continue
 inst=re.search(r'm_PrefabInstance: \{fileID: (\d+)\}',block)[1]
 if inst not in mapping:continue
 source=re.search(r'm_CorrespondingSourceObject: (\{.*?\})',block,re.S)[1]
 matches=[dk for dk,db in D.items() if 'stripped' in db.splitlines()[0] and re.search(r'm_PrefabInstance: \{fileID: (\d+)\}',db)[1]==mapping[inst] and re.search(r'm_CorrespondingSourceObject: (\{.*?\})',db,re.S)[1]==source]
 if len(matches)==1:mapping[k]=matches[0]
 elif len(matches)>1:raise Exception('ambiguous stripped')
for k in a[3]:
 if k in mapping:continue
 added.add(k)
 if a[2][k]!='1001':
  go=a[3][k][0];added.add(go);added.update(comps(S[go]))
# Add dependencies only when not already mapped to their existing counterpart.
def refs(block):return re.findall(r'\{fileID: (-?\d+)\}',block)
while True:
 missing={r for k in added for r in refs(S[k]) if r!='0' and r not in mapping and r not in added}
 if not missing:break
 assert all(r in S for r in missing),missing-set(S)
 added.update(missing)
print('EXTRA COMPONENTS',additions)
print('COPY BLOCKS',len(added))
print('COPY TYPES',collections.Counter(a[2][k] for k in added))
# Resolve newly copied blocks to collision-free IDs.
used=set(D);nextid=8000000000000000000
for k in sorted(added,key=int):
 while str(nextid) in used:nextid+=1
 mapping[k]=str(nextid);used.add(str(nextid));nextid+=1
# Show script paths to inspect compatibility before mutation.
for k in sorted(added,key=int):
 if a[2][k]=='114':print('SCRIPT',k,re.search(r'm_Script:.*guid: (\w+)',S[k])[1])
pathlib.Path('work/scene_merge_plan.json').write_text(json.dumps({'mapping':mapping,'added':sorted(added,key=int),'additions':additions},indent=2))

