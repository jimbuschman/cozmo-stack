"""Validate packet transcription/completeness, not source fidelity."""
from pathlib import Path
import hashlib, json, re, subprocess
import lief

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
coverage=json.loads((OUT/'coverage.json').read_text(encoding='utf-8'))
manifest=json.loads((ROOT/'re-analysis/fidelity_manifest.json').read_text(encoding='utf-8-sig'))
byid={r['id']:r for r in manifest['records']}
engine=ROOT/'resources/lib/armeabi-v7a/libcozmoEngine.so'
if not engine.exists(): engine=next(Path('C:/Users/JimBu/Downloads').glob('**/resources/lib/armeabi-v7a/libcozmoEngine.so'))
libs={}
for name in ['libcozmoEngine.so','libc++_shared.so']:
    path=engine.parent/name
    elf=lief.parse(str(path))
    libs[name]=(path.read_bytes(),[s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD])
errors=[]; instructions=0; ranges=0; patches=0; quotedlines=0; unmatched=[]
for r in coverage['included']:
    id=r['id']; text=(OUT/r['file']).read_text(encoding='utf-8')
    actual=json.loads(re.search(r'```json\n(.*?)\n```',text,re.S)[1])
    if actual!=byid[id]: errors.append(id+': manifest transcription differs')
    rowtext=text.split('## Build rows quoted from their source',1)[1].split('Additional historical checked repair input',1)[0].split('## Implementation commits',1)[0]
    lines=(ROOT/('re-analysis/inventory/'+byid[id]['subsystem']+'.md')).read_text(encoding='utf-8-sig').splitlines()
    for m in re.finditer(r'^> \[(\d+)\] (.*)$',rowtext,re.M):
        quotedlines+=1
        if lines[int(m[1])-1]!=m[2]: errors.append(id+': row '+m[1]+' differs')
    for p,patch in re.findall(r'### `([^`]+)`\n.*?```diff\n(.*?)\n```',text,re.S):
        exact=subprocess.check_output(['git','log','--follow','--reverse','--root','--format=commit %H%nParents: %P%nAuthor: %an <%ae>%nDate: %aI%nSubject: %s','-p','--no-ext-diff','--no-color','--',p],cwd=ROOT).decode('utf-8','replace')
        patches+=1
        if exact!=patch: errors.append(id+': patch differs for '+p)
    native=text.split('## Native instruction text for cited addresses',1)[1].split('## Tests, oracle evidence, and recorded results',1)[0]
    spans=[]; lib='libcozmoEngine.so'
    for line in native.splitlines():
        if line.startswith(('libcozmoEngine.so:','libc++_shared.so:')): lib=line.split(':',1)[0]
        m=re.match(r'^0x([0-9A-Fa-f]{8})  ((?:[0-9a-fA-F]{2}(?: |$))+)',line)
        if not m: continue
        a=int(m[1],16); data=bytes.fromhex(m[2]); raw,loads=libs[lib]
        seg=next((s for s in loads if s.virtual_address<=a<s.virtual_address+s.physical_size),None)
        if seg is None: errors.append(id+': native address outside ELF '+m[1]); continue
        off=seg.file_offset+a-seg.virtual_address
        if raw[off:off+len(data)]!=data: errors.append(id+': native bytes differ at '+lib+':'+m[1])
        instructions+=1
        if lib=='libcozmoEngine.so': spans.append((a,a+len(data)))
    merged=[]
    for a,b in sorted(spans):
        if merged and a<=merged[-1][1]: merged[-1]=(merged[-1][0],max(b,merged[-1][1]))
        else: merged.append((a,b))
    for aa,bb in re.findall(r'^0x([0-9A-Fa-f]{8})\.\.0x([0-9A-Fa-f]{8})$',native,re.M):
        ranges+=1; a=int(aa,16); b=int(bb,16)
        if not any(x<=a and b<y for x,y in merged): errors.append(id+': native citation coverage gap '+aa+'..'+bb)
    unmatched.extend((id,line) for line in text.splitlines() if line.startswith('- `') and 'NO MATCH in this run' in line)
allids={r['id'] for r in manifest['records'] if r['id'].startswith('M1-')}
included={r['id'] for r in coverage['included']}; excluded=set(coverage['excluded'])
if included&excluded or included|excluded!=allids: errors.append('included/excluded partition differs')
files={p.stem for p in OUT.glob('M[12]-*.md')}
if files!=included: errors.append('per-record file set differs')
result={'source_commit':coverage['source_commit'],'purpose':'Packet transcription and coverage checks only; no fidelity verdict.','record_files':len(files),'excluded_records':len(excluded),'manifest_transcriptions_checked':len(included),'source_lines_checked':quotedlines,'exact_git_patch_histories_checked':patches,'native_instruction_or_data_rows_checked_against_ELF':instructions,'engine_cited_intervals_checked_for_byte_coverage':ranges,'unmatched_declared_tests':unmatched,'test_counters':coverage['test_counters'],'errors':errors}
(OUT/'validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result))
raise SystemExit(bool(errors))
