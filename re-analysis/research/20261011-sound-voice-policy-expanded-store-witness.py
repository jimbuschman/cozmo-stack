"""Q14-033 exact-offset ARM store navigation and bounded receiver dispositions.

This is not an all-writer, all-mode, code-boundary or alias-absence proof.
"""
from pathlib import Path
import hashlib, json
import lief, capstone
from capstone.arm import ARM_OP_MEM, ARM_REG_SP

root = Path(__file__).resolve().parents[2]
p = root / 'resources/lib/armeabi-v7a/libcozmoEngine.so'
digest = hashlib.sha256(p.read_bytes()).hexdigest()
assert digest == '02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1'
elf = lief.parse(str(p))
lo, hi = 0x97D000, 0xA7AA00
sec = next(s for s in elf.sections if s.virtual_address <= lo and hi <= s.virtual_address + s.size)
md = capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_ARM)
md.detail = True
md.skipdata = True
paid = {0xA557A8, 0xA557AC, 0xA559D0, 0xA55B60}
allocated = {0x99E95C, 0x99E960, 0xA354AC, 0xA354B0}
global_registry = {0x9CF118, 0x9CF11C, 0x9CF178, 0x9CF194,
                   0x9CF2A8, 0x9CF2B8, 0x9CF2C0}
global_settings = {0x99EAB4, 0x99EAD4}
embedded = {0xA76408, 0xA7640C}
rows = []
for i in md.disasm(bytes(sec.content)[lo-sec.virtual_address:hi-sec.virtual_address], lo):
    if not i.id or not i.mnemonic.startswith(('str', 'vstr')):
        continue
    mem = next((o.mem for o in i.operands if o.type == ARM_OP_MEM and o.mem.disp in (0xE0, 0xE4)), None)
    if mem is None:
        continue
    a = i.address
    if mem.base == ARM_REG_SP:
        kind = 'direct-stack-store'
        reason = 'This instruction addresses current SP plus its literal displacement, not a formed voice receiver; subsequent copying/escape is not excluded.'
    elif a in paid:
        kind = 'paid-voice-writer'
        reason = 'VW650 owns this actual voice policy write and its selected new/retained/virtualizing input.'
    elif a in allocated:
        kind = 'separate-allocation-product'
        reason = ('R7 is the successful 0x118-byte allocation at99E6AC..B8, retained through startup initialization.'
                  if a < 0xA00000 else
                  'R3 is the successful 0x208-byte allocation atA353B4..D4, published to incoming managerBC; not the incoming receiver.')
    elif a in global_registry:
        kind = 'fixed-global-registry-product'
        reason = '9CF07C indexes GOT1040074 whose raw value is108D904;9CF090 loads current registry FP from that global. E0/E4 are bucket pointer/count backing fields on that selected product, not new settings R4.'
    elif a in global_settings:
        kind = 'fixed-global-settings-address'
        reason = '99E40C forms R4=108D868. These writes are defaults at that fixed global address, rather than the voice argument; they do not prove its settings immutable.'
    elif a in embedded:
        kind = 'known-embedded-stage-offset'
        reason = 'A76284 retains incoming receiver IP; actual voice constructor calls it with V+1D0 and V+3A0. These stores therefore address V+2B0/2B4 and V+480/484 for those two calls, not V+E0/E4. Alternate calls are not excluded.'
    else:
        kind = 'retained-receiver-frontier'
        reason = 'Positive exact-offset encoding; receiver/admitted input must be attributed before any voice-alias exclusion. Existing local contracts are not reopened solely by offset coincidence.'
    rows.append(dict(address=f'{a:08X}', instruction=i.mnemonic+' '+i.op_str,
                     bytes=i.bytes.hex(), disposition=kind, reason=reason))
assert len(rows) == 50
assert paid | allocated | global_registry | global_settings | embedded <= {int(r['address'], 16) for r in rows}
counts = {k: sum(r['disposition'] == k for r in rows) for k in sorted({r['disposition'] for r in rows})}
out = dict(engineSha256=digest, interval=[f'{lo:08X}',f'{hi:08X}'],
           limit='Aligned ARM STR/VSTR with literal starting displacement E0/E4 in this interval only. Thumb, literal/code boundaries, overlapping/block stores, composed/indexed addresses, copies, escaped pointers and other intervals are not certified absent.',
           counts=counts, candidates=rows)
Path(__file__).with_suffix('.json').write_text(json.dumps(out, indent=2)+'\n', encoding='utf-8')
print(json.dumps(counts, sort_keys=True))
