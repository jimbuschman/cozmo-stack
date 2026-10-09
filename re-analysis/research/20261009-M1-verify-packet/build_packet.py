"""Reproducible evidence packaging only; no fidelity decisions or production writes."""
from pathlib import Path
import bisect, collections, hashlib, json, re, subprocess, xml.etree.ElementTree as ET
import capstone, lief

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT).decode('utf-8', 'replace')
def read(p): return (ROOT/p).read_text(encoding='utf-8-sig')
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
BASE = git('rev-parse', 'HEAD').strip()
manifest_path = 're-analysis/fidelity_manifest.json'
manifest = json.loads(read(manifest_path))
records = [r for r in manifest['records'] if r['id'].startswith('M1-')]
excluded = {}
for r in records:
    if 'MISSING' in (r.get('unresolved') or ''):
        excluded[r['id']] = 'Open MISSING in current unresolved: ' + r['unresolved']
    elif r['status'] in ('RECOVERABLE_GAP', 'HARDWARE_ONLY'):
        excluded[r['id']] = r['status'] + '; no completed built record to package. ' + (r.get('unresolved') or '')
included = [r for r in records if r['id'] not in excluded]
inventory = {k: read('re-analysis/inventory/'+k+'.md').splitlines() for k in ('M1-transport', 'M2-protocol')}

# M2's per-message rows often say NEW rather than an allocated ID. Preserve
# the complete relevant original table/section, including headers and caveats.
m2_sections = {
 'M2-001': [(131,149),(349,382),(95,116)],
 'M2-002': [(461,537),(537,558)],
 'M2-003': [(461,519),(537,553)],
 'M2-004': [(149,253)], 'M2-005': [(149,253),(267,274)],
 'M2-006': [(382,461),(537,553)], 'M2-007': [(149,253)],
 'M2-008': [(131,149)], 'M2-009': [(149,200),(288,302)],
 'M2-010': [(349,382)], 'M2-011': [(349,382),(95,116)],
 'M2-012': [(382,461),(560,578)], 'M2-013': [(382,461),(553,560)],
 'M2-014': [(519,537),(553,560)], 'M2-015': [(200,253),(274,288)],
 'M2-016': [(604,659),(753,775)], 'M2-017': [(95,116),(349,382)]}
def source_rows(r):
    lines = inventory[r['subsystem']]
    selected = {i for i,l in enumerate(lines,1) if r['id'] in l and l.startswith('|')}
    if r['id'].startswith('M2-'):
        for lo,hi in m2_sections[r['id']]: selected.update(range(lo,hi))
    else:
        # Row references in both the current manifest and original ownership table.
        reftext = json.dumps(r, ensure_ascii=False) + '\n'.join(lines[i-1] for i in selected)
        refs = set(re.findall(r'\b(?:R|B|CA|CB|CC|CD|E|G[1-5]\.)\d+(?:\.\d+)?', reftext))
        for prefix,a,b in re.findall(r'\b(R|B|CA|CB|CC|CD|E)(\d+)\s*(?:\.\.|–|-)\s*(?:\1)?(\d+)',reftext):
            refs.update(prefix+str(n) for n in range(int(a),int(b)+1))
        for i,l in enumerate(lines,1):
            label = re.match(r'^\|\s*((?:R|B|CA|CB|CC|CD|E|G[1-5]\.)\d+(?:\.\d+)?)\b',l)
            if label and label[1] in refs: selected.add(i)
        if r['id']=='M1-005': selected.update(range(186,214))
        # All subsequent correction sections mentioning the record are retained,
        # avoiding presentation of superseded initial rows without the correction.
        heads = [i for i,l in enumerate(lines,1) if l.startswith('## ')] + [len(lines)+1]
        for lo,hi in zip(heads,heads[1:]):
            if 'Correction' in lines[lo-1] and any(r['id'] in l for l in lines[lo-1:hi-1]):
                selected.update(range(lo,hi))
    return [(i,lines[i-1]) for i in sorted(selected) if i<=len(lines)]

engine = ROOT/'resources/lib/armeabi-v7a/libcozmoEngine.so'
if not engine.exists():
    engine = next(Path('C:/Users/JimBu/Downloads').glob('**/resources/lib/armeabi-v7a/libcozmoEngine.so'))
def load_library(path):
    global elf,raw,loads,sections,functions,starts,library_name
    library_name=path.name
    elf = lief.parse(str(path)); raw = path.read_bytes()
    loads = [s for s in elf.segments if s.type==lief.ELF.Segment.TYPE.LOAD]
    sections = list(elf.sections)
    functions = {}
    for s in elf.symbols:
        if s.value and s.size and s.type==lief.ELF.Symbol.TYPE.FUNC:
            a=s.value&~1
            if a not in functions or s.size>functions[a][0]: functions[a]=(s.size,s.name,bool(s.value&1))
    starts = sorted(functions)
load_library(engine)
def offset(a):
    for s in loads:
        if s.virtual_address<=a<s.virtual_address+s.physical_size:
            return s.file_offset+a-s.virtual_address
def section(a):
    return next((s for s in sections if s.virtual_address<=a<s.virtual_address+s.size),None)
def owner(a):
    i=bisect.bisect_right(starts,a)-1
    if i>=0:
        start=starts[i]; size,name,thumb=functions[start]
        if a<start+size: return start,start+size,name,thumb
def addresses(text, minimum=0x4B0000):
    result=[]
    # Full ranges and abbreviated right endpoints, with/without repeated 0x.
    pat=r'0x([0-9A-Fa-f]{5,8})(?:\s*(?:\.\.|–|-)\s*(?:0x)?([0-9A-Fa-f]{4,8}))?'
    for m in re.finditer(pat,text):
        a=int(m[1],16); b=a
        if m[2]: b=int(m[1][:-len(m[2])]+m[2],16) if len(m[2])<len(m[1]) else int(m[2],16)
        if b<a: a,b=b,a
        if a>=minimum and offset(a) is not None and b-a<0x100000: result.append((a,b))
    # Slash-compressed citations such as 0x007FCEBE/C2 and 0x69DB18/26/3A/6E.
    for m in re.finditer(r'0x([0-9A-Fa-f]{5,8})((?:/[0-9A-Fa-f]{2,8})+)',text):
        for part in m[2].split('/')[1:]:
            a=int(m[1][:-len(part)]+part,16)
            if a>=minimum and offset(a) is not None: result.append((a,a))
    return sorted(set(result))
native_cache={}
def native(ranges):
    blocks=set(); unmapped=[]
    for a,b in ranges:
        o=owner(a)
        if o: blocks.add(o)
        for start in starts[bisect.bisect_right(starts,a):bisect.bisect_right(starts,b)]:
            size,name,thumb=functions[start]; blocks.add((start,start+size,name,thumb))
        # Preserve explicit data/unlabelled ranges separately.
        if not o or not owner(b) or o[1]<=b:
            sec=section(a)
            if not o and sec and sec.name=='.plt':
                blocks.add((a&~3,max(b+4,a+12),'explicit ARM PLT stub citation',False))
            else:
                blocks.add((a&~1,b+4,'explicit citation span (includes symbol gaps and data)',o[3] if o else True))
    out=[]
    for a,b,name,thumb in sorted(blocks):
        key=(library_name,a,b,name,thumb)
        if key not in native_cache:
            off=offset(a); sec=section(a)
            data=raw[off:off+b-a] if off is not None else b''
            header=f'{library_name}: 0x{a:08X}..0x{b:08X} exclusive; {name}; section {sec.name if sec else "none"}'
            if sec and (int(sec.flags)&4):
                md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB if thumb else capstone.CS_MODE_ARM)
                md.skipdata=True
                body='\n'.join(f'0x{i.address:08X}  {i.bytes.hex(" "):23s}  {i.mnemonic:10s} {i.op_str}'.rstrip() for i in md.disasm(data,a))
                header+='; '+('Thumb' if thumb else 'ARM')+' linear instruction/byte transcript'
            else:
                header+='; DATA BYTES (not instructions)'
                body='\n'.join(f'0x{a+n:08X}  {data[n:n+16].hex(" ")}' for n in range(0,len(data),16))
            native_cache[key]=header+'\n'+body
        out.append(native_cache[key])
    return '\n\n'.join(out)

tracked = git('ls-files','cozmo-stack/src').splitlines()
tags=collections.defaultdict(set)
for p in tracked:
    if not p.endswith(('.cs','.py','.json')): continue
    for line in read(p).splitlines():
        if 'fidelity:' in line:
            for id in re.findall(r'M[12]-\d{3}',line.split('fidelity:',1)[1]): tags[id].add(p)
history_cache={}; blame_cache={}
def history(p):
    if p not in history_cache:
        history_cache[p]=git('log','--follow','--reverse','--root','--format=commit %H%nParents: %P%nAuthor: %an <%ae>%nDate: %aI%nSubject: %s','-p','--no-ext-diff','--no-color','--',p)
    return history_cache[p]
def line_origins(p):
    if p not in blame_cache:
        entries=[]
        for line in git('blame','--line-porcelain',BASE,'--',p).splitlines():
            m=re.match(r'^([0-9a-f]{40}) (\d+) (\d+)(?: (\d+))?$',line)
            if m: entries.append((int(m[3]),m[1]))
        spans=[]
        for line,commit in entries:
            if spans and spans[-1][2]==commit and spans[-1][1]+1==line:
                spans[-1]=(spans[-1][0],line,commit)
            else: spans.append((line,line,commit))
        blame_cache[p]='\n'.join(f'| {lo}–{hi} | `{commit}` |' for lo,hi,commit in spans)
    anchors=[f'{i}: {l.strip()}' for i,l in enumerate(read(p).splitlines(),1) if 'fidelity:' in l]
    return '| Current lines | Origin commit |\n|---|---|\n'+blame_cache[p]

trx_path=OUT/'test-results/packet-tests.trx'
trx=ET.parse(trx_path); ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
results=trx.findall('.//t:UnitTestResult',ns)
counters=trx.find('.//t:Counters',ns).attrib
testfiles=git('ls-files','cozmo-stack/tests').splitlines()
test_sources={p:read(p) for p in testfiles if p.endswith('.cs')}
def test_evidence(r):
    declared=str(r.get('test',''))
    names=list(dict.fromkeys(re.findall(r'\b[A-Za-z0-9_]+Tests\.[A-Za-z0-9_]+',declared)))
    if re.fullmatch(r'[A-Za-z0-9_]+Tests', declared.strip()): names.append(declared.strip())
    if 'NeedsDecayCheckedRowsTests' in declared: names.append('NeedsDecayCheckedRowsTests')
    if r['id']=='M2-005': names.append('NativeNumericOracleTests.ProductionNumericHelpersMatchShippedArmInstructions')
    out=[]; matched=[]
    for name in names:
        hits=[x for x in results if name in x.attrib['testName']]
        out.append(f'- `{name}`: '+(f'{len(hits)} case(s): '+', '.join(f'{o}: {n}' for o,n in collections.Counter(x.attrib['outcome'] for x in hits).items()) if hits else 'NO MATCH in this run'))
        for p,text in test_sources.items():
            cls,_,method=name.partition('.')
            if cls not in text: continue
            if not method:
                if Path(p).stem==cls: matched.append((p,text))
            elif re.search(r'\b'+re.escape(method)+r'\s*\(',text):
                lines=text.splitlines(); hit=next((i for i,l in enumerate(lines) if re.search(r'\b'+re.escape(method)+r'\s*\(',l)),None)
                if hit is not None:
                    lo=hit
                    while lo>0 and not lines[lo-1].strip().startswith(('public ','private ','protected ','[Fact','[Theory')): lo-=1
                    lo=max(0,lo-2); hi=hit+1; depth=0; opened=False
                    while hi<len(lines):
                        depth+=lines[hi].count('{')-lines[hi].count('}')
                        if '{' in lines[hi]: opened=True
                        hi+=1
                        if opened and depth<=0: break
                    matched.append((f'{p}:{lo+1}-{hi}', '\n'.join(lines[lo:hi])))
    out.append('\nSource tests are quoted below; a test result is not a fidelity verdict. No native emulator was newly executed in packet preparation.')
    for p,text in dict(matched).items(): out.append(f'\nSource: `{p}`\n```csharp\n{text}\n```')
    if r['id'] in ('M2-003','M2-005'):
        fixture='cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/m1_m5_numeric_oracle.json'
        f=json.loads(read(fixture)); funcs={'M2-003':{'lift_height','lift_angle','lift_angle_to_height','lift_height_to_angle'},'M2-005':{'rgb555_fragment'}}[r['id']]
        actual=sorted(set(x.get('function','') for x in f['rows']))
        blocked=f.get('blocked',f.get('blocked_inputs',[]))
        out.append('\nNative oracle artifact: `'+fixture+'`; SHA256 '+sha(ROOT/fixture)+'; generator `'+f['generator']+'`.\nAvailable function names: '+', '.join(actual)+'.')
        for key,value in f.items():
            if key not in ('rows','blocked','blocked_inputs'): out.append(f'\n{key}: `{json.dumps(value)}`')
        chosen=[x for x in f['rows'] if x.get('function') in funcs]
        blockedchosen=[x for x in blocked if x.get('function') in funcs or (r['id']=='M2-003' and 'lift' in x.get('function',''))]
        out.append(f'\nStored native outputs relevant here: {len(chosen)}; relevant blocked calls: {len(blockedchosen)}.\n```json\n'+json.dumps({'rows':chosen,'blocked_calls':blockedchosen},indent=2)+'\n```')
        # Preserve the published coverage limits without introducing a verdict.
        report='re-analysis/research/20261007-oracle-coverage.md'
        quotes=[l for l in read(report).splitlines() if ('M2-003' in l or 'light packing' in l)]
        out.append('\nSource: `'+report+'`\n'+'\n'.join('> '+l for l in quotes))
        out.append('\nOracle generator source: `'+f['generator']+'`, SHA256 '+sha(ROOT/f['generator'])+'\n```python\n'+read(f['generator'])+'\n```')
    elif not any('Native' in n or 'Emulator' in n for n in names):
        out.append('\nNo separate native-emulator/oracle artifact is identified by this record or the M1/M2 numeric-oracle coverage report. The listed source-regression tests and their actual results are included; no absence-of-coverage verdict is made.')
    return '\n'.join(out)

summary=[]
for r in included:
    id=r['id']; rows=source_rows(r); inv='re-analysis/inventory/'+r['subsystem']+'.md'
    rowtext='\n'.join(l for _,l in rows)
    extra=[]
    if id in ('M1-046','M1-047','M1-053'):
        p='re-analysis/jobs/B-M1M2.md'
        text=read(p)
        extra.append((p,text[text.index('## Rows checked (manager, 2026-10-09):'):]))
        p='re-analysis/research/'+('20261009-M1-053-projection-rows.md' if id=='M1-053' else '20261009-M1-final-extraction.md')
        extra.append((p,read(p)))
    if r['id'] in ('M1-041','M2-002'):
        p='re-analysis/research/20261002-B-CORE2-verify.md'
        # Retain the checked repair inputs, with their historical date and wording.
        source=read(p).splitlines()
        lines=[15,16,17] if r['id']=='M1-041' else [27,38,39,40]
        extra.append((p,'\n'.join(f'[{i}] {source[i-1]}' for i in lines)))
    if r['id'] in ('M1-015','M1-024','M1-044','M1-045','M1-048','M1-050'):
        inputs=['re-analysis/jobs/B-M1M2.md']
        if r['id'] in ('M1-015','M1-024','M1-044'): inputs.append('re-analysis/research/20261009-M1M2-opus-pass.md')
        if r['id']=='M1-048': inputs.append('re-analysis/research/20261005-M1M2-policy-check.md')
        if r['id']=='M1-050': inputs.append('re-analysis/research/20261006-M1M2-missing-triage.md')
        for p in inputs:
            lines=read(p).splitlines()
            if p.endswith('B-M1M2.md'):
                start=next(i for i,l in enumerate(lines) if l.startswith('## The app-boundary decision'))
                quoted='\n'.join(lines[start:])
            elif p.endswith('missing-triage.md'):
                quoted='\n'.join(l for l in lines if 'S13' in l)
            elif p.endswith('policy-check.md'):
                quoted='\n'.join(l for l in lines if l.startswith('| U') or 'U1' in l or 'U7' in l)
            else: quoted=read(p)
            extra.append((p,quoted))
    citationtext=json.dumps(r,ensure_ascii=False)+'\n'+rowtext+'\n'+'\n'.join(t for _,t in extra)
    if id in ('M1-046','M1-047','M1-053'):
        # Adopted extraction tables also use bare eight-digit VAs and named PLTs.
        # Normalize only the address scan; the original quoted text stays verbatim.
        citationtext=re.sub(r'(?<![0-9A-Fa-f])([0-9A-Fa-f]{8})(?![0-9A-Fa-f])',r'0x\1',citationtext)
        ranges=addresses(citationtext,minimum=0x4A0000)
    else: ranges=addresses(citationtext)
    native_text=native(ranges)
    library_info=''
    if r['id']=='M1-034':
        cpp=engine.parent/'libc++_shared.so'
        load_library(cpp)
        cpptext='\n'.join(r['evidence'])+'\n'+'\n'.join(l for _,l in rows if re.match(r'^\| G3\.(?:11|12|13|14|15|16)\b',l))
        cpp_ranges=[(a,b) for a,b in addresses(cpptext,minimum=0x10000) if b<0x100000]
        native_text+='\n\n'+native(cpp_ranges)
        library_info=f' Shipped libc++_shared.so is also read locally, SHA256 `{sha(cpp)}`; its cited VA intervals: '+', '.join(f'0x{a:08X}..0x{b:08X}' for a,b in cpp_ranges)+'.'
        load_library(engine)
    paths=sorted(tags[id]|{r['location']})
    if id=='M1-053':
        # Preserve exact supplier-adapter hunks, including those tagged to their owning layers.
        paths=sorted(set(paths)|{'cozmo-stack/src/Cozmo.Robot/'+p for p in (
            'Camera.cs','Motion.cs','Sensors.cs','Manipulation/Docking.cs',
            'Vision/RobotStateHistory.cs','Vision/VisionSystem.cs')})
    parts=[f'# {id} — verification evidence packet',f'Snapshot: `{BASE}`. Current record is reproduced verbatim as JSON. No new verdict, status change, approval, or settlement is supplied.',
      '## Current manifest text',f'Source: `{manifest_path}`; file SHA256 `{sha(ROOT/manifest_path)}`.\n```json\n'+json.dumps(r,ensure_ascii=False,indent=2)+'\n```',
      '## Build rows quoted from their source',f'Source: `{inv}`; SHA256 `{sha(ROOT/inv)}`. Line numbers refer to the snapshot above. Original classifications/wording belong to the quoted source, not this packet. Section/table supersets are retained where row ownership was NEW or a correction crosses records.']
    parts += [f'> [{i}] {l}' for i,l in rows]
    for p,text in extra:
        parts.append(f'Additional historical checked repair input, quoted from `{p}`; SHA256 `{sha(ROOT/p)}`. Its historical verdict words are source text, not a new packet verdict.\n'+'\n'.join('> '+l for l in text.splitlines()))
    parts += ['## Implementation commits and exact diff hunks','The following are complete chronological Git patch histories of the current manifest location and every production file carrying this record’s fidelity tag. They include initial construction, shared-file edits, and later repairs. This deliberate superset preserves exact hunks and commit provenance without inferring that every shared-file hunk implements this record. `--follow --root -p` includes pre-tag construction/renames; commit hashes, parents and paths are preserved. No hunk is rewritten or summarized.']
    for p in paths:
        anchors=[f'{i}: {l.strip()}' for i,l in enumerate(read(p).splitlines(),1) if 'fidelity:' in l and id in l.split('fidelity:',1)[1]]
        parts.append(f'### `{p}`\nCurrent fidelity-tag anchors for {id}:\n```text\n'+'\n'.join(anchors)+'\n```\nCurrent source line-to-commit attribution (`git blame --line-porcelain`); this maps every current line of the shared file to the preserved patch history, without assigning untagged lines invented record ownership:\n'+line_origins(p)+'\n\n```diff\n'+history(p)+'\n```')
    parts += ['## Native instruction text for cited addresses',f'Primary ELF: `libcozmoEngine.so`; SHA256 `{sha(engine)}`. Capstone `{capstone.__version__}`; LIEF `{lief.__version__}`. Addresses below are ELF VAs extracted from the current record and quoted rows, including slash/abbreviated range endpoints. Full containing sized ELF functions are included, as are intervening functions for ranges. Endpoints are inclusive in citations and exclusive in function headers. Data citations receive raw byte dumps; they are not represented as instructions. Linear disassembly preserves bytes (including embedded tables/literals); `.byte` is Capstone skip-data. Function mode comes from the ELF function symbol low bit. No decompiler text or external implementation is used.',
      library_info+'\nCited engine address intervals:\n```text\n'+'\n'.join(f'0x{a:08X}..0x{b:08X}' for a,b in ranges)+'\n```', '```text\n'+native_text+'\n```',
      '## Tests, oracle evidence, and recorded results',f'Packet-preparation TRX: `test-results/packet-tests.trx`, SHA256 `{sha(trx_path)}`. Run counters: `{json.dumps(counters,sort_keys=True)}`. This is an offline regression run; hardware was not run.',test_evidence(r)]
    if not ranges:
        parts.insert(-3,'No native address range is cited in this record or its policy row. The current manifest and original policy row above are retained; no native evidence is invented for this host policy.')
    (OUT/(id+'.md')).write_text('\n\n'.join(parts)+'\n',encoding='utf-8')
    summary.append({'id':id,'file':id+'.md','rows':len(rows),'cited_intervals':len(ranges),'history_paths':paths})
index=['# M1 verification packet — 2026-10-09',f'Prepared for the operator’s request in this chat. Source snapshot `{BASE}` on main. No new verdicts. Explicit operator authorization to commit/push overrides the ordinary research-lane no-commit rule.',
 '## Coverage',f'{len(records)} current M1 records; {len(included)} included, {len(excluded)} excluded. Built records and policy/equivalence records with no explicit open MISSING are included. Current non-MISSING verification uncertainty is retained verbatim; inclusion does not mean complete fidelity. Hardware-only and unbuilt recoverable records are excluded. The manager adopted M1-046 U1-U8, M1-047 P1-P4 and M1-053 P2a-P2m in B-M1M2.md; their builds and supplier boundaries are now included.',
 '| Record | Packet | Quoted lines | Native cited intervals |\n|---|---|---:|---:|']
index += [f'| {s["id"]} | [{s["file"]}]({s["file"]}) | {s["rows"]} | {s["cited_intervals"]} |' for s in summary]
index += ['## Excluded records and reasons','Each current excluded record is reproduced in full below so exclusions do not depend on an older report.']
for r in records:
    if r['id'] in excluded: index += [f'### {r["id"]}',excluded[r['id']], '```json\n'+json.dumps(r,ensure_ascii=False,indent=2)+'\n```']
index += ['## Reproduction and artifact boundaries','`python re-analysis/research/20261009-M1-verify-packet/build_packet.py` packages the checked-out snapshot after the offline test TRX exists. It only writes in this directory. The native ELF is read locally and is not uploaded. Per-record files are self-contained for manifest, rows, diff text, native transcripts, and relevant test/result text. Complete shared-file histories intentionally contain unrelated hunks; they are explicitly labeled as supersets rather than assigned invented record-level causal ownership.',
 'Test command: `dotnet test cozmo-stack/Cozmo.sln --logger "trx;LogFileName=packet-tests.trx" --results-directory .scratch/m1-final-tests`, with DOTNET_PROCESSOR_COUNT=4 and ThreadPoolMinThreads=32. The supplied TRX records the actual result; existing oracle blocked imports remain blocked.']
index += ['## Adopted rows and supplier boundaries',
 'M1-046/-047: [final extraction rows](../20261009-M1-final-extraction.md). M1-053: [projection rows](../20261009-M1-053-projection-rows.md). Manager adoption is quoted in the three record files. Root pose, tracking writers, image-result commit and other suppliers remain owned by their named higher-layer records, including their lifetime gaps. Native member allocation bookkeeping is represented by engine-thread-confined managed handle storage. No supplier or M1 record is settled by this packet.',
 '## Offline run and packet validation',f'Offline run: {counters["passed"]} passed, {counters["failed"]} failed, {counters["notExecuted"]} not executed. The full solution suite is recorded without result substitution.']
for result in results:
    if result.attrib['outcome']!='Passed':
        message=result.find('t:Output/t:ErrorInfo/t:Message',ns)
        index.append('`'+result.attrib['testName']+'`: '+result.attrib['outcome']+'\n\n'+(message.text if message is not None else 'No error text supplied.'))
index += ['`validate_packet.py` checks the included/excluded partition, verbatim manifest values and inventory lines, exact Git patch histories, native transcript bytes against the local ELF, citation byte coverage, and declared-test result matching. Machine-readable results are in `validation.json`. These are packaging checks, not an Opus/source-fidelity verdict.']
(OUT/'README.md').write_text('\n\n'.join(index)+'\n',encoding='utf-8')
(OUT/'coverage.json').write_text(json.dumps({'source_commit':BASE,'manifest_sha256':sha(ROOT/manifest_path),'engine_sha256':sha(engine),'included':summary,'excluded':excluded,'test_counters':counters},indent=2)+'\n',encoding='utf-8')
print(json.dumps({'included':len(included),'excluded':len(excluded),'bytes':sum(p.stat().st_size for p in OUT.glob('*.md')),'source_commit':BASE}))
