"""Independent 38-word shipped-neutral fixture and compiler regression candidate.
Writes only research files; does not alter or settle production records.
"""
from pathlib import Path
import json,re,struct,subprocess
root=Path(__file__).resolve().parents[2]
out=root/'re-analysis/research'
asset=root/'re-analysis/obb/assets/cozmo_resources/assets/animations/anim_singlepose_01.bin'
b=asset.read_bytes()
u32=lambda p:struct.unpack_from('<I',b,p)[0]
def field(t,n):
    v=t-struct.unpack_from('<i',b,t)[0]
    return t+struct.unpack_from('<H',b,v+4+2*n)[0]
def target(p):return p+u32(p)
def tables(t,n):
    v=target(field(t,n));return [target(v+4+4*i) for i in range(u32(v))]
clips=tables(u32(0),0)
neutral=[]
for c in clips:
    s=target(field(c,0))
    if b[s+4:s+4+u32(s)]==b'anim_neutral_eyes_01':neutral.append(c)
assert len(neutral)==1
faces=tables(target(field(neutral[0],1)),1)
f=faces[0]
offsets=[]
for eye in [6,7]:
    v=target(field(f,eye));assert u32(v)==19
    offsets.extend(v+4+i*4 for i in range(19))
expected=[u32(p) for p in offsets]
src=(root/'cozmo-stack/src/Cozmo.Robot/Animation/ProceduralFace.cs').read_text(encoding='utf-8')
method=src[src.index('public static ProceduralFacePose ShippedNeutral()'):]
arrays=re.findall(r'Eye.FromAsset\(new\[\]\s*\{(.*?)\}',method,re.S)
tokens=[m for a in arrays[:2] for m in re.findall(r'-?\d+(?:\.\d+)?f',a)]
assert len(tokens)==38
proj=out/'20261006-neutral-pose-compiler';proj.mkdir(exist_ok=True)
(proj/'LiteralBits.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>\n',encoding='utf-8')
candidate=list(tokens);candidate[2]='BitConverter.UInt32BitsToSingle(0x3F9B6F43)'
candidate[3]=candidate[22]='BitConverter.UInt32BitsToSingle(0x3F67C075)'
code='using System;\nusing System.IO;\n'
code+='float[] original={'+','.join(tokens)+'};\n'
code+='float[] candidate={'+','.join(candidate)+'};\n'
# Oracle is copied from asset words, never recomputed by the C# implementation.
code+='uint[] expected={'+','.join(f'0x{x:08X}u' for x in expected)+'};\n'
code+='using var w=new StreamWriter(args[0]);\nw.WriteLine("index,asset,original,candidate");\n'
code+='for(int i=0;i<38;i++){uint a=BitConverter.SingleToUInt32Bits(original[i]);uint c=BitConverter.SingleToUInt32Bits(candidate[i]);w.WriteLine($"{i},{expected[i]:X8},{a:X8},{c:X8}");}\n'
(proj/'Program.cs').write_text(code,encoding='utf-8')
(out/'20261006-neutral-pose-native-fixture.json').write_text(json.dumps({'asset':str(asset.relative_to(root)).replace('\\','/'),'clip':'anim_neutral_eyes_01','first_face':hex(f),'words':[{'eye':'left' if i<19 else 'right','parameter':i%19,'offset':hex(p),'bits':f'{x:08X}'} for i,(p,x) in enumerate(zip(offsets,expected))]},indent=2)+'\n',encoding='utf-8')
result=out/'20261006-neutral-pose-compiled.csv'
subprocess.run(['dotnet','run','--project',str(proj/'LiteralBits.csproj'),'--',str(result)],check=True)
import csv
rows=list(csv.DictReader(result.open()))
assert len(rows)==38
assert all(r['asset']==r['candidate'] for r in rows)
print('38 candidate words match; original mismatches:',[r for r in rows if r['asset']!=r['original']])
manifest=json.loads((root/'re-analysis/fidelity_manifest.json').read_text(encoding='utf-8'))
report=['| Item | Coverage | Outcome |','|---|---|---|',
'| Original Q9 left scale-X discrepancy | CHECKED | Native asset word rechecked; original compiler result differs by one ULP. |',
'| Full neutral-eye arrays (38 words) | CHECKED | Three original words differ; all 38 proposed replacement-array words match. |',
'| Current production callers and ownership | CHECKED | Raw-face API uses ShippedNeutral; streamer loads asset-backed pose separately. |',
'| Formal fidelity settlement | PARTIAL | Manager approval required; no record changed. |',
'| Other queue PARTIALs and audio renderer | PARTIAL | Not resolved by this bounded float investigation; existing blockers remain. |','',
'Research follow-up to the operator\'s request to resolve the remaining evidence. This report closes the neutral-literal investigation, not the whole queue or any fidelity record. It corrects Q9\'s method naming/ownership shorthand: the actual method is `ProceduralFacePose.ShippedNeutral()` at ProceduralFace.cs:349, not `Neutral()` and not an AnimationStreamer fallback.','',
'Current records, quoted before the findings:','']
for r in manifest['records']:
    if r['id'] in ['M5-002','M5-010','M5-020']:
        report.extend([f'> {r["id"]}: {r["title"]}. Status: {r["status"]}.','> Evidence: '+json.dumps(r['evidence'],ensure_ascii=False),''])
report.extend(['The scalar defect is in the helper\'s asset copy, rather than the M5-002 constructor default or the asset-loaded M5-010 reset path. M5-020 provenance explicitly describes ShowExpression/HoldExpression retaining the shipped neutral face; these entry points actually consume the rounded copy. No claim about its still-unresolved RNG seed is changed.','',
'| Step | Citation | Behaviour / gate / order | Result or remaining uncertainty | Float width and bits |','|---|---|---|---|---|',
'| N1 | anim_singlepose_01.bin root/clip/keyframe vectors; companion native fixture | Follow FlatBuffer root to clip named anim_neutral_eyes_01, first ProceduralFace, left/right fields 6/7, each length 19. Preserve eye and parameter order. | Native bytes independently establish all 38 expected words. | binary32 throughout. |',
'| N2 | asset 0xF8; ProceduralFace.cs:354 | Left EyeScaleX (parameter 2). | DEFECT: original literal is one ULP high. | Native 3F9B6F43; C# 3F9B6F44. |',
'| N3 | asset 0xFC; ProceduralFace.cs:354 | Left EyeScaleY (parameter 3). | DEFECT: original literal is seven ULPs low; omitted from Q9 because 0.90528 is outside the baseline. | Native 3F67C075; C# 3F67C06E. |',
'| N4 | asset 0xAC; ProceduralFace.cs:360 | Right EyeScaleY (parameter 3). | Same defect as N3. | Native 3F67C075; C# 3F67C06E. |',
'| N5 | engine 0x00583790..0x0058382E; Q9 native companion | SetEyeArrayHelper requires 19 floats, passes each to Clip then stores at eye-specific offset. | Existing native bounds/NaN semantics are not replaced by guessed behavior. The values in question are ordinary finite scales. | Word loads/stores preserve f32 input before the existing Clip rules. |',
'| N6 | CozmoAnimations.cs:560,564 | CozmoFace construction and ResetToConstructed call ShippedNeutral. | Retained Current carries the three incorrect words before a caller renders it. | f32. |',
'| N7 | CozmoAnimations.cs:583,598..601 | Neutral branch of ShowExpression renders via SetParameters -> renderer -> Display.Show; HoldExpression renders -> Display.Hold. Other expressions use trigger path. | Fix only the three constants; do not change routing, duration or nonneutral behavior. Whether a ULP changes any bitmap for a particular geometry is not established by this scalar comparison. | f32 pose parameters. |',
'| N8 | AnimationScheduler.cs:607..637 | Streamer resolves and loads neutral clip separately; it does not call ShippedNeutral. | No streamer defect is inferred from the helper literals. | Asset f32. |','',
'Exact proposed implementation: replace only left EyeScaleX with `BitConverter.UInt32BitsToSingle(0x3F9B6F43)` and both EyeScaleY literals with `BitConverter.UInt32BitsToSingle(0x3F67C075)`. No production edit was made in this research task.','',
'The companion JSON fixture contains the 38 native words and byte offsets, generated directly from the shipped asset. The companion net9.0 compiler probe (SDK 9.0.201) copies current source literals and the three proposed bit-pattern substitutions: original 35/38 match; proposed 38/38 match. This is a literal-conversion check, not a production regression test. A builder regression must call the actual ShippedNeutral(), compare every parameter against the checked-in native fixture, and exercise ShowExpression/HoldExpression through their normal entry if claiming downstream output. It must not calculate expected values using the implementation.','',
'Remaining queue gaps are unchanged: M5-013 alias/bulk-store writer proof, whole-runtime ADP renderer/corpus, broader M6 seams and higher-layer dependencies, exhaustive helper/alias test provenance, and hardware/live-entry coverage. Formal settlement remains with the manager. This closes the bounded neutral-value evidence without inventing those missing paths.'])
(out/'20261006-neutral-pose-followup.md').write_text('\n'.join(report)+'\n',encoding='utf-8')
