from scene_compare import *
a=load('yeop.unity');b=load('moon_branch Jeon Yong.unity');m=json.load(open('work/scene_merge_plan.json'))['mapping']
for k,dk in m.items():
 if k not in a[1] or dk not in b[1] or a[2][k]!='114':continue
 aa=dict(re.findall(r'^  (\w+): (.*)$',a[1][k],re.M));bb=dict(re.findall(r'^  (\w+): (.*)$',b[1][dk],re.M))
 extra={x:v for x,v in aa.items() if x not in bb and not x.startswith('m_')}
 if extra:print('EXTRA FIELDS',k,extra)
