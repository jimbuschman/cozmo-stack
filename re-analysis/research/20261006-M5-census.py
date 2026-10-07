"""Native navigation census; listed candidates still require type/dataflow review."""
from pathlib import Path
import bisect, lief, capstone
b=lief.parse('resources/lib/armeabi-v7a/libcozmoEngine.so')
functions=[]
for line in Path('re-analysis/decomp/libcozmoEngine/index.tsv').read_text().splitlines():
    fields=line.split('\t')
    if len(fields)==5:
        try: functions.append((int(fields[0],16),fields[4]))
        except ValueError: pass
functions.sort(); addresses=[a for a,n in functions]
def owner(a):
    i=bisect.bisect_right(addresses,a)-1
    return functions[i][1] if i>=0 else '?'
c=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB); c.skipdata=True
out=['Thumb navigation census across .text; literal-pool/ARM false positives possible. No absence proof without construction-path review.']
s=b.get_section('.text')
for a,size,mn,ops in c.disasm_lite(bytes(s.content),s.virtual_address):
    direct=mn in ('bl','b.w') and ops in ('#0x564290','#0x4ac4b4')
    field=mn.startswith('strb') and ('#0x28]' in ops or '#0x30]' in ops) and any(x in owner(a) for x in ('Animation','KeyFrame','Face','Track'))
    if direct or field: out.append(f'{a:08X}: {mn} {ops} ; {owner(a)}')
Path('re-analysis/research/20261006-M5-sprite-census.txt').write_text('\n'.join(out))
print('\n'.join(out))
