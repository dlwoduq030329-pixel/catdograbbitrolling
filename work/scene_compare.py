import re,json,pathlib,collections
BASE=pathlib.Path('Assets/renew')
def load(name):
 s=(BASE/name).read_text(encoding='utf-8-sig'); blocks={m[2]:m[0] for m in re.finditer(r'^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^---|\Z)',s,re.M|re.S)}
 kinds={k:re.match(r'--- !u!(\d+)',v)[1] for k,v in blocks.items()}
 names={k:re.search(r'^  m_Name: (.*)',v,re.M)[1].strip("'\"") for k,v in blocks.items() if kinds[k]=='1' and re.search(r'^  m_Name: (.*)',v,re.M)}
 trans={}; paths={}
 for k,b in blocks.items():
  if kinds[k] in ('4','224') and 'm_Father:' in b:
   go=re.search(r'm_GameObject: \{fileID: (\d+)\}',b)[1]; parent=re.search(r'm_Father: \{fileID: (\d+)\}',b)[1];trans[k]=(go,parent)
 for k,b in blocks.items():
  if kinds[k]=='1001':
   guid=re.search(r'm_SourcePrefab:.*guid: ([a-f0-9]+)',b)[1]
   parent=re.search(r'm_TransformParent: \{fileID: (\d+)\}',b)[1]
   n=re.search(r'propertyPath: m_Name\n\s+value: (.*)',b)
   names[k]=n[1] if n else 'PREFAB:'+guid
   trans[k]=(k,parent)
 def path(k,trail=()):
  if k=='0':return ''
  if k in paths:return paths[k]
  if k not in trans:
   blk=blocks.get(k,'')
   inst=re.search(r'm_PrefabInstance: \{fileID: (\d+)\}',blk)
   if inst and inst[1]!='0': return path(inst[1])
   return '?'+k
  go,par=trans[k]; n=names.get(go,'?'+go)
  if '\\u' in n:n=json.loads('"'+n+'"')
  paths[k]=path(par,trail+(k,))+'/'+n;return paths[k]
 for k in trans:path(k)
 return s,blocks,kinds,trans,paths
if __name__=='__main__':
 a=load('yeop.unity');b=load('moon_branch Jeon Yong.unity')
 for title,x in [('SOURCE',a),('TARGET',b)]:
  print(title+' ROOTS')
  for k,p in x[4].items():
   if x[3][k][1]=='0':print(k,p)
 print('MISSING PATHS')
 bp=set(b[4].values())
 for k,p in a[4].items():
  if p not in bp:print(k,p)
