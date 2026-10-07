"""Read shipped Unity serialized fields locally; never export or upload assets."""
from pathlib import Path
import sys,json,hashlib,gc,struct
h=Path(__file__).resolve().parent;r=h.parents[1]
sys.path.insert(0,str(h/'_q17_unity_dependencies'))
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
generator=TypeTreeGenerator('2018.1.2f1')
generator.load_local_dll_folder(str(r/'resources/assets/bin/Data/Managed'))
roots=[r/'resources/assets/bin/Data',r/'re-analysis/obb/assets/bin/Data',r/'re-analysis/obb/assets/AssetBundles']
inputs=[]
for root in roots:
 for p in root.rglob('*'):
  if not p.is_file() or 'Managed' in p.parts:continue
  if '.split' in p.name:
   if p.name.endswith('.split0'):inputs.append(p)
   continue
  with p.open('rb') as f:head=f.read(48)
  serialized=len(head)>=16 and 5<=struct.unpack('>I',head[8:12])[0]<=25 and struct.unpack('>I',head[4:8])[0]==p.stat().st_size
  if head.startswith((b'UnityFS',b'UnityWeb',b'UnityRaw')) or serialized or p.name in ('globalgamemanagers','globalgamemanagers.assets','level0','unity default resources'):
   inputs.append(p)
report={'tool':'UnityPy '+UnityPy.__version__,'inputs':[],'matches':[],'unreadable':[],'monobehaviors':0}
def walk(v,path=''):
 if isinstance(v,dict):
  for k,x in v.items():
   key=path+'/'+k
   if any(t in k.lower() for t in ('idle','animtrigger','default_anim')):
    yield key,x
   yield from walk(x,key)
 elif isinstance(v,list):
  for i,x in enumerate(v):yield from walk(x,path+'/'+str(i))
for number,p in enumerate(inputs):
 entry={'path':str(p.relative_to(r)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
 try:
  env=UnityPy.load(str(p));env.typetree_generator=generator
  if p.name=='level0':env.load_file(str(p.parent/'globalgamemanagers.assets'))
  entry['objects']=len(env.objects)
  for obj in env.objects:
   if obj.type.name!='MonoBehaviour':continue
   report['monobehaviors']+=1
   try:
    try:tree=obj.read_typetree()
    except Exception:
     env.load_file(str(r/'resources/assets/bin/Data/globalgamemanagers.assets'))
     env.load_file(str(r/'resources/assets/bin/Data/unity default resources'))
     tree=obj.read_typetree(nodes=obj.generate_monobehaviour_node())
    for field,value in walk(tree):
     report['matches'].append({'input':entry['path'],'serialized_file':obj.assets_file.name,'path_id':obj.path_id,'field':field,'value':value,'script':tree.get('m_Script')})
   except Exception as e:
    failure={'input':entry['path'],'serialized_file':obj.assets_file.name,'path_id':obj.path_id,'error':str(e)}
    try:
     script=obj.parse_monobehaviour_head().m_Script.deref_parse_as_object()
     failure['script']={'namespace':script.m_Namespace,'class':script.m_ClassName,'assembly':script.m_AssemblyName}
    except Exception as se:failure['script_error']=str(se)
    report['unreadable'].append(failure)
  del env
 except Exception as e:entry['error']=str(e)
 report['inputs'].append(entry)
 (h/'20261007-q17-unity-idle-scan.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
 if number%25==0:print(number+1,'/',len(inputs),'inputs;',report['monobehaviors'],'MonoBehaviours;',len(report['matches']),'matches',flush=True)
 gc.collect()
print('DONE',len(inputs),'inputs;',report['monobehaviors'],'MonoBehaviours;',len(report['unreadable']),'unreadable;',len(report['matches']),'fields')
