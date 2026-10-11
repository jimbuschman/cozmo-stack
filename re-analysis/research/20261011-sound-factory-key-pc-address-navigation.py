"""Original factory input and key bytes: direct PC forms only, no admission proof."""
from pathlib import Path
import hashlib
baseline=Path(__file__).with_name('20261010-sound-weight-pc-address-navigation.py')
source=baseline.read_text(encoding='utf-8')
assert source.count('target=0xA08024;')==1 and source.count('value&~1==target')==2
source=source.replace('target=0xA08024;','targets={0xA3B684,0xA3B694,*range(0x108D9A0,0x108D9B0)};')
source=source.replace('value&~1==target','value&~1 in targets')
source=source.replace("'target':'00A08024'","'targets':[f'{t:08X}' for t in sorted(targets)]")
print('PC FORM MODEL SHA256',hashlib.sha256(baseline.read_bytes()).hexdigest())
namespace={'__file__':str(Path(__file__).resolve())}
exec(compile(source,str(baseline),'exec'),namespace)
sections=[s for s in namespace['elf'].sections if s.has(namespace['lief'].ELF.Section.FLAGS.EXECINSTR)]
lo=min(s.virtual_address for s in sections);hi=max(s.virtual_address+s.size for s in sections)
assert lo>=4095 and hi+4099<0x108D9A0
result=namespace['r']
result['single_thumb_pc_immediate_key_bound']={'executable_low':f'{lo:08X}','executable_exclusive_high':f'{hi:08X}','conservative_pc_plus_imm12_high':f'{hi+4099:08X}','key_low':'0108D9A0','boundary':'Single Thumb ADR16/ADDW/SUBW PC immediate only. No wrap possible at these image bounds; loads, later arithmetic and indirect/copy/foreign aliases are not excluded.'}
Path(__file__).with_suffix('.json').write_bytes((namespace['json'].dumps(result,indent=2)+'\n').encode())
