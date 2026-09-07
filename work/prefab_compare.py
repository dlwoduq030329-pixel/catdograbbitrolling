from scene_compare import *
a=load('yeop.unity');b=load('moon_branch Jeon Yong.unity');plan=json.load(open('work/scene_merge_plan.json'));mp=plan['mapping']
for k,blk in a[1].items():
 if a[2][k]!='1001' or k not in mp or mp[k] not in b[1] or b[2][mp[k]]!='1001':continue
 pat=r'    - target: (\{.*?\})\n      propertyPath: ([^\n]+)\n      value: ([^\n]*)\n      objectReference: (\{.*?\})'
 aa=re.findall(pat,blk,re.S);bb=re.findall(pat,b[1][mp[k]],re.S);keys={(x[0],x[1]) for x in bb};extra=[x for x in aa if (x[0],x[1]) not in keys]
 if extra:print(a[4].get(k),json.dumps(extra,ensure_ascii=True))
