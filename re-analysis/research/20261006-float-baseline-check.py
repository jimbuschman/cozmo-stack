"""Q9: native-byte oracle versus compiled C# literals; writes research artifacts only.
Run prepare, dotnet run --project <generated project>, then report.
"""
import csv, hashlib, json, re, struct, sys
from pathlib import Path
import lief

root = Path(__file__).resolve().parents[2]
out = root / 're-analysis/research'
prefix = '20261006-float-baseline'
selected = [line.split() for line in (out / (prefix+'-selected.txt')).read_text().splitlines()]
lib = root / 'resources/lib/armeabi-v7a/libopencv_imgproc.so'
asset = root / 're-analysis/obb/assets/cozmo_resources/assets/animations/anim_singlepose_01.bin'
elf = lief.parse(str(lib))
table = bytes(elf.get_content_from_virtual_address(0xE7910,451*4))
assert len(table) == 1804
source = root / 'cozmo-stack/src/Cozmo.Robot/Animation/OpenCv310.cs'
text = source.read_text()
start = text.index('{', text.index('float[] SinTable'))+1
end = text.index('};',start)
tokens = list(re.finditer(r'-?\d+\.\d+f',text[start:end]))
assert len(tokens) == 451
entries=[]
for i,m in enumerate(tokens):
    entries.append(dict(key=m[0].lstrip('-')[:-1],token=m[0],path='Cozmo.Robot/Animation/OpenCv310.cs',line=text[:start+m.start()].count('\n')+1,address=f'libopencv_imgproc.so VA 0x{0xE7910+4*i:08X} (table[{i}])',bits=struct.unpack_from('<I',table,4*i)[0]))
face = root / 'cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFace.cs'
ft=face.read_text()
offsets={'9.169666':0xF0,'1.214333':0xF8,'10.206374':0xA0,'1.222037':0xA8}
ab=asset.read_bytes()
assert b'anim_neutral_eyes_01' in ab
# Follow actual root -> clips -> matching name -> keyframes -> first face -> eye vectors.
u32=lambda p: struct.unpack_from('<I',ab,p)[0]
def field(t,n):
    v=t-struct.unpack_from('<i',ab,t)[0]
    off=struct.unpack_from('<H',ab,v+4+2*n)[0]
    assert off
    return t+off
def target(p): return p+u32(p)
def tables(t,n):
    v=target(field(t,n))
    return [target(v+4+4*i) for i in range(u32(v))]
clips=tables(u32(0),0)
neutral=[]
for c in clips:
    s=target(field(c,0))
    if ab[s+4:s+4+u32(s)]==b'anim_neutral_eyes_01': neutral.append(c)
assert len(neutral)==1
k=target(field(neutral[0],1))
f=tables(k,1)[0]
left=target(field(f,6)); right=target(field(f,7))
assert u32(left)==u32(right)==19
assert [left+4,left+12,right+4,right+12]==[0xF0,0xF8,0xA0,0xA8]
for m in re.finditer(r'-?\d+\.\d+f',ft):
    key=m[0].lstrip('-')[:-1]
    if key in offsets:
        off=offsets[key]
        entries.append(dict(key=key,token=m[0],path='Cozmo.Robot/Animation/ProceduralFace.cs',line=ft[:m.start()].count('\n')+1,address=f'anim_singlepose_01.bin offset 0x{off:X}',bits=struct.unpack_from('<I',ab,off)[0]))
entries=[e for e in entries if [e['path'],e['key']] in selected]
assert len({(e['path'],e['key']) for e in entries})==len(selected)==91
project=out/(prefix+'-compiler')
if sys.argv[1]=='prepare':
    project.mkdir(exist_ok=True)
    (project/'LiteralBits.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>\n')
    code=['// Literal expressions copied from production source; expected bits are read independently from shipped bytes.','using System;','using System.IO;','var values = new float[] {']
    code.extend(e['token']+',' for e in entries)
    code.extend(['};','using var output = new StreamWriter(args[0]);','output.WriteLine("index,bits");','for (int i=0;i<values.Length;i++) output.WriteLine($"{i},{BitConverter.SingleToUInt32Bits(values[i]):X8}");'])
    (project/'Program.cs').write_text('\n'.join(code)+'\n')
    print(f'{len(entries)} signed literal occurrences, {len(selected)} baseline entries prepared')
else:
    actual=list(csv.DictReader((out/(prefix+'-compiled.csv')).open()))
    assert len(actual)==len(entries)
    for e,a in zip(entries,actual): e['csharp_bits']=int(a['bits'],16)
    (out/(prefix+'-results.json')).write_text(json.dumps(entries,indent=2)+'\n')
    rows=['| Baseline item (path / unsigned key) | Coverage | Occurrences | Result |','|---|---|---:|---|']
    for path,key in selected:
        es=[e for e in entries if e['path']==path and e['key']==key]
        match=all(e['bits']==e['csharp_bits'] for e in es)
        rows.append(f'| `{path}` / `{key}` | CHECKED | {len(es)} | '+('MATCH' if match else 'WRONG')+' |')
    rows.extend(['','Q9 of `requests/20261006-codex-queue-3.md`. All 91 M3–M5 baseline entries checked: 87 OpenCV table literals and four neutral-face asset literals. No selected M3/M4 entries occur in the baseline. Selection is by the current manifest ownership and the M5-tagged OpenCV helper; keys in the baseline omit signs, so every signed occurrence is checked separately.','',f'{len(entries)} compiled literal occurrences; {sum(e["bits"]!=e["csharp_bits"] for e in entries)} differences. Compiler: .NET SDK 9.0.201, net9.0. Results are compiler-emitted binary32 words, not values calculated by a Python float model or a C# implementation helper. Expected words come directly from the shipped ELF table and animation asset. Companion `20261006-float-baseline-results.json` retains every occurrence.','', 'The sine table is binary32 at `libopencv_imgproc.so` VA `0x000E7910..0x000E801C` (451 words, exclusive end). `cv::ellipse2Poly` at `0x00043C48..0x00043DE8` loads table words and widens them to binary64 for coordinate arithmetic; widening does not change the stored literal word. These are table values, not newly evaluated sine functions. See the companion native instruction dump.','', 'The four face values are binary32 asset words in the `anim_neutral_eyes_01` entry of `anim_singlepose_01.bin`, consumed as floats by the face loader (`libcozmoEngine.so`, `0x00583790..0x00583808`). Their asset offsets are citations, not engine virtual addresses. Matching them does not establish the entire neutral-pose fallback lifecycle.',''])
    for p in [lib,asset]: rows.append(f'- `{p.relative_to(root).as_posix()}` SHA256 `{hashlib.sha256(p.read_bytes()).hexdigest()}`.')
    rows.extend(['','| Step | C# file:line / signed literal | Source address or asset offset | Width | Engine bits | Compiled C# bits | Result |','|---:|---|---|---|---|---|---|'])
    for i,e in enumerate(entries,1): rows.append(f'| {i} | `{e["path"]}:{e["line"]}` / `{e["token"]}` | {e["address"]} | binary32 | `0x{e["bits"]:08X}` | `0x{e["csharp_bits"]:08X}` | '+('MATCH' if e['bits']==e['csharp_bits'] else 'WRONG')+' |')
    rows.extend(['','No source change or fidelity reclassification is proposed by this literal check. There are no branch gates, action ordering or failure results attached to a stored scalar; consumer semantics remain in their owning records. No unresolved literal or width remains within Q9’s selected scope.','', 'Queue summary: Q1 through Q9 reports have been published. Q3 remains blocked on a production engine/C# audio renderer and corpus (zero paired runs); the Q2 cached-mode writer proof, Q5 end-to-end runtime seams, Q6 higher-layer dependencies, Q7 exhaustive alias/helper assertion provenance and Q8 hardware/live-entry coverage retain the specific PARTIAL limitations stated in their reports. Q9 checks all selected literal entries. No fidelity record was settled, and no hardware run was performed.'])
    report='\n'.join(rows)+'\n'
    report=report.replace('consumed as floats by the face loader', 'passed as 19-word float arrays through ProceduralFace::SetEyeArrayHelper')
    report=report.replace('No source change or fidelity reclassification is proposed by this literal check.', 'One source correction is needed: the left-eye scale-X literal at ProceduralFace.cs:354 must reproduce binary32 0x3F9B6F43 (for example BitConverter.UInt32BitsToSingle(0x3F9B6F43)). The current decimal literal emits 0x3F9B6F44, one ULP higher. This report does not implement or reclassify it.')
    manifest=json.loads((root/'re-analysis/fidelity_manifest.json').read_text())
    quotes=[]
    for r in manifest['records']:
        if r['id'] in ['M5-002','M5-010']:
            quotes.extend([f'> {r["id"]}: {r["title"]}. Status: {r["status"]}.', '> Evidence: '+json.dumps(r['evidence']), ''])
    marker='| Step | C# file:line / signed literal |'
    insertion='Current manifest records, quoted before the defect: binary32 parameter storage relates to M5-002, neutral/reset ownership to M5-010. This finding is confined to the fallback Neutral() asset constants; it does not contradict the constructor-default portion of M5-002 or certify the rest of M5-010.\n\n'+'\n'.join(quotes)+'\nDEFECT: left-eye scale-X in the shipped first neutral-eye keyframe is `0x3F9B6F43`, not the earlier research note’s `0x3F9B6F44`. The actual FlatBuffer root/clip-name/keyframe/19-float-vector walk establishes offset `0xF8`; the decimal literal compiles to `0x3F9B6F44`. The three other neutral scalars match.\n\n'
    report=report.replace(marker,insertion+marker)
    (out/'20261006-float-baseline-M3-M5.md').write_text(report,encoding='utf-8')
    q8=out/'20261006-test-entry-M3-M5.md'
    q8.write_text(q8.read_text().replace('CozmoRobot.HandleRobotMessage` (`CozmoRobot.cs:638`)', 'CozmoRobot.RouteToDevices` (`CozmoRobot.cs:648`)'))
    print(f'checked {len(entries)} occurrences; differences: {sum(e["bits"]!=e["csharp_bits"] for e in entries)}')
