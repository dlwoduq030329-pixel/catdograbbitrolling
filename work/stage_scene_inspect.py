from scene_compare import *
x=load('moon_branch Jeon Yong.unity')
for k,b in x[1].items():
 if any(g in b for g in ['ab167392e7188154dbe4dfd87627c2c9','7da83bca325d7a345993294439ff97df','d92c59f01e2fae6489ee3aa5d1d367db']) and x[2][k]=='114':
  print(k,b[:1900]);go=re.search(r'm_GameObject: \{fileID: (\d+)\}',b)[1];print(x[1][go])
