"""Original755 candidates into9F319C only; reuse878 word attribution.
Keep actual9F2F88 indirect tail UNKNOWN rather than equating a rejected Thumb branch with target absence.
"""
from pathlib import Path
baseline=Path(__file__).with_name('20261011-sound-routing-member-interior-attribution.py')
source=baseline.read_text(encoding='utf-8')
for before,after in (
 ('0x9d2410<=int(x[\'target\'][1:],16)<0x9d24c8','0x9f319c<=int(x[\'target\'][1:],16)<0x9f3274'),
 ('assert len(selected)==33','assert len(selected)==34'),
 ('range(0x9d224c,0x9d23e4,4)','range(0x9f2e34,0x9f3160,4)'),
 ('0x9d23ec','0x9f3164'),('0x9d23dc','0x9f315c'),('0x9d23e0','0x9f315c'),
 ('009D23EC','009F3164'),
 ('Literal read on existing9D224C ARM path, after9D23E0 POP-PC','Literal read on existing9F2E34 ARM path, after9F315C branch'),
 ('  assert a&3==2','  assert a&3==2 or a==0x9f318c'),
 ('Upper half of different ARM instruction on recovered Layer path','Halfword of different ARM instruction on recovered voice/limiter path'),
 ("'literal23EC_readers':literal_refs","'literal3164_readers':literal_refs"),
 ('Only33 existing755 candidates into9D2410;32 upper-half ARM-word attributions and one literal.','Only34 existing755 candidates into9F319C;32 upper-half ARM words, one lower half of9F318C STR and one literal.'),
 ('Other34 candidates into9F319C are not covered here.','Other33 candidates into9D2410 were separately retained878. Actual9F2F88 ARM BX target from incoming receiver liveC4 remains UNKNOWN; no target exclusion inferred from rejecting its Thumb interpretation.'),
 ('33 existing candidates retained','34 existing candidates retained'),
):
 assert before in source,before
 source=source.replace(before,after)
source=source.replace('context=[decode(0x9f315c),decode(0x9f315c),','context=[decode(0x9f3158),decode(0x9f315c),')
source=source.replace("Path(__file__).with_suffix('.json').write_bytes", "result['actual_indirect_tail_context']=[decode(a) for a in (0x9f2e34,0x9f2e38,0x9f2e48,0x9f2f70,0x9f2f74,0x9f2f78,0x9f2f7c,0x9f2f80,0x9f2f84,0x9f2f88)]\nPath(__file__).with_suffix('.json').write_bytes")
exec(compile(source,str(baseline),'exec'),{'__file__':str(Path(__file__).resolve())})
