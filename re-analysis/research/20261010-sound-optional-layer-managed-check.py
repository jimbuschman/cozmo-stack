"""Q14-021 finite managed import/name witness; not dynamic/reflection/input absence proof."""
from pathlib import Path
import sys,json,hashlib,gc
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'re-analysis/research/_m5013_dependencies'))
import dnfile
names=json.loads(Path(__file__).with_name('20261010-sound-optional-layer-import-witness.json').read_text(encoding='utf-8'))['symbols']
exact={x['name'] for x in names}; labels={'AddLayer','AddPersistentLayer','AddToPersistentLayer','RemovePersistentLayer','GenerateGlitchAudio'}
rows=[]
for path in sorted((ROOT/'resources/assets/bin/Data/Managed').glob('*.dll')):
 raw=path.read_bytes();pe=dnfile.dnPE(str(path))
 assert pe.net and pe.net.mdtables, str(path)
 imports=[]
 table=pe.net.mdtables.ImplMap
 if table:
  for row in table.rows:
   imports.append(dict(importName=str(row.ImportName),module=str(row.ImportScope.row.Name),managedMethod=str(row.MemberForwarded.row.Name)))
 named=[]
 for tableName in ('MethodDef','MemberRef'):
  table=getattr(pe.net.mdtables,tableName)
  if table:
   for ix,row in enumerate(table.rows,1):
    name=str(row.Name)
    if name in labels or name in exact:named.append(dict(table=tableName,row=ix,name=name))
 literal=[]
 for name in sorted(exact|labels):
  for encoding in ('ascii','utf-16-le'):
   needle=name.encode(encoding);start=0
   while True:
    pos=raw.find(needle,start)
    if pos<0:break
    literal.append(dict(name=name,encoding=encoding,fileOffset=hex(pos)));start=pos+len(needle)
 rows.append(dict(file=str(path.relative_to(ROOT)),sha256=hashlib.sha256(raw).hexdigest(),implMapCount=len(imports),matchingExactImports=[x for x in imports if x['importName'] in exact],engineImports=[x for x in imports if x['module'] in ('cozmoEngine','libcozmoEngine.so')],matchingManagedNames=named,literalNameCandidates=literal))
 pe.close();del pe;gc.collect()
result=dict(scope='All shipped Managed/*.dll: ImplMap exact five optional-audio-layer mangled imports, MethodDef/MemberRef exact simple or mangled names, and ASCII/UTF16 raw name navigation. No dynamic lookup/reflection/CLAD/native-pointer producer absence proof.',assemblies=rows)
Path(__file__).with_suffix('.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('Assemblies',len(rows),'ImplMap records',sum(x['implMapCount'] for x in rows),'exact imports',sum(len(x['matchingExactImports']) for x in rows),'named methods/refs',sum(len(x['matchingManagedNames']) for x in rows),'literal candidates',sum(len(x['literalNameCandidates']) for x in rows))
for x in rows:
 if x['matchingExactImports'] or x['matchingManagedNames'] or x['literalNameCandidates'] or x['engineImports']:print(json.dumps(x))
