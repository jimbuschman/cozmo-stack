"""Navigation candidates only; re-open any candidate before extracting a row."""
import importlib,struct
n=importlib.import_module('20261007-queue5-native')
s=n.elf.get_section('.init_array')
print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
print('Candidate PC-relative addresses; not proof of a store or of absence.')
for (entry,) in struct.iter_unpack('<I',n.read(s.virtual_address,s.size)):
    if entry&1:continue
    f=n.enclosing(entry)
    if not f or f[0]!=entry:continue
    z=min(n.end(f),entry+0x10000)
    md=n.capstone.Cs(n.capstone.CS_ARCH_ARM,n.capstone.CS_MODE_ARM)
    md.detail=True
    regs={}
    for a in range(entry,z,4):
        ins=list(md.disasm(n.read(a,4),a))
        if not ins:continue
        i=ins[0]
        if i.mnemonic=='ldr' and len(i.operands)==2:
            dst,src=i.operands
            if src.type==n.ARM_OP_MEM and src.mem.base==n.ARM_REG_PC and not src.mem.index:
                try:regs[dst.reg]=n.word(a+8+src.mem.disp)
                except ValueError:pass
        elif i.mnemonic=='add' and len(i.operands)==3:
            dst,p,r=i.operands
            if p.reg==n.ARM_REG_PC and r.reg in regs:
                target=(a+8+regs[r.reg])&0xffffffff
                if 0x108DA00<=target<0x108DB00:
                    print(f'init={entry:08X} instruction={a:08X} candidate={target:08X}')
