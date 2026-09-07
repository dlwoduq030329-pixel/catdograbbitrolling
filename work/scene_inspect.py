from scene_compare import *
a=load('yeop.unity');b=load('moon_branch Jeon Yong.unity')
for x in (a,b):
 for k,blk in x[1].items():
  if x[2][k] in ('4','224') and 'stripped' in blk.splitlines()[0]:
   print('STRIPPED',k,blk[:420].replace('\n',' '))
print('NEW ROOT DETAILS')
for k in ['10843689','22660631','910769127','1244837529','1820484946','1944791447','2139437543']:
 go=a[3][k][0];print(a[4][k]);print(a[1][go]);
 for cid in re.findall(r'component: \{fileID: (\d+)\}',a[1][go]):print(a[1][cid][:2600])
