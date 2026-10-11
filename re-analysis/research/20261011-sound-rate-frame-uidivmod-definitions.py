"""Exactly1 existing post-call UIDIVMOD import: shipped symbol-definition navigation only."""
from pathlib import Path
import json,hashlib,lief
names={'__aeabi_uidivmod'};rows=[];definitions=[]
for path in sorted(Path('resources/lib/armeabi-v7a').glob('*.so')):
 elf=lief.parse(str(path));raw=path.read_bytes();found=[]
 for sym in elf.dynamic_symbols:
  if sym.name not in names:continue
  item={'name':sym.name,'value':f'{sym.value:08X}','size':sym.size,'section_index':sym.shndx,'binding':str(sym.binding),'visibility':str(sym.visibility),'defined':bool(sym.shndx and sym.value)}
  found.append(item)
  if item['defined']:
   definitions.append({'library':path.name,**item})
 rows.append({'path':path.as_posix(),'sha256':hashlib.sha256(raw).hexdigest(),'needed_libraries':list(elf.libraries),'symbols':found})
r={'scope':'Exactly1 existing post-call UIDIVMOD import; exported source availability only,not runtime lookup winner,helper algorithm or engine alias absence.','librariesExamined':len(rows),'definitions':definitions,'libraries':rows}
Path(__file__).with_suffix('.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8')
print('Shipped libraries examined',len(rows))
for x in definitions:print(x)
print('Engine needed libraries',next(x['needed_libraries'] for x in rows if x['path'].endswith('libcozmoEngine.so')))
