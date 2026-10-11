"""R-M5-047: direct shipped LoadAnimationGroupFile; no native worker threads.
All output and fixture code stay in research. Use python -B to avoid bytecode writes.
"""
from pathlib import Path
import sys, json, struct, hashlib, math
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 're-analysis/tools/emu'))
from emu_elf import Machine, Abort, BASES, STACK, STACK_SIZE, HEAP, HEAP_SIZE
from unicorn import UC_HOOK_CODE, UC_HOOK_MEM_WRITE
from unicorn.arm_const import UC_ARM_REG_PC, UC_ARM_REG_LR, UC_ARM_REG_SP
B = BASES['libcozmoEngine.so']
DIR = ROOT / 're-analysis/obb/assets/cozmo_resources/assets/animationGroups'
OUT = Path(__file__).with_name('20261011-m5-047-stack-emu.json')

def cppstr(m, raw):
    s, data = m.malloc(12), m.malloc(len(raw)+1)
    m.wr(s, bytes(12)); m.wr(data, raw+b'\0')
    m.call(B+0x4e02b3, s, data, len(raw), count=1000000)
    return s

def dump_native():
    import lief, capstone
    path=ROOT/'resources/lib/armeabi-v7a/libcozmoEngine.so'
    raw=path.read_bytes(); elf=lief.parse(str(path))
    md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
    lines=['Engine SHA256 '+hashlib.sha256(raw).hexdigest(),
           'ELF addresses; Thumb. Raw instruction ranges, not decompiler output.']
    ranges=[(0x521d7c,0x521f84),(0x58b890,0x58b8b6),(0x58b6f8,0x58b70a),
            (0x58c044,0x58c056),(0x58a5ec,0x58a740),(0x58c4b0,0x58c4ca),(0x58c706,0x58c7c6)]
    for start,end in ranges:
        seg=next(s for s in elf.segments if s.virtual_address<=start<s.virtual_address+s.physical_size)
        off=start-seg.virtual_address+seg.file_offset
        lines.append('\nRANGE '+hex(start)+'..'+hex(end)+' exclusive')
        lines.extend(f'{i.address:08X}: {i.mnemonic:12} {i.op_str}' for i in md.disasm(raw[off:off+end-start],start))
    Path(__file__).with_name('20261011-m5-047-stack-native.txt').write_text('\n'.join(lines)+'\n')

def run(poison, prior=None, heap_pad=0):
    m = Machine(libs=['libc++_shared.so','libcozmoEngine.so'], poison=poison)
    files = {}; io = []
    def fopen(q):
        p = Path(q.cstr(q.reg(0)).decode()); mode=q.cstr(q.reg(1)).decode()
        if not p.is_relative_to(DIR) or 'w' in mode or '+' in mode:
            raise Abort('unexpected fopen fixture request '+str(p)+' '+mode)
        handle=q.malloc(16); files[handle]=[p.read_bytes(),0]
        io.append(dict(operation='fopen',path=str(p.relative_to(ROOT)),mode=mode,handle=hex(handle)))
        return handle
    def fread(q):
        dest,size,n,handle = [q.reg(i) for i in range(4)]
        data,pos=files[handle]; raw=data[pos:pos+size*n]
        q.wr(dest,raw); files[handle][1]+=len(raw)
        return len(raw)//size if size else 0
    def fseek(q):
        handle,off,whence=[q.reg(i) for i in range(3)]
        if off & 0x80000000: off-=1<<32
        data,pos=files[handle]; files[handle][1]=off+([0,pos,len(data)][whence]); return 0
    m.libc.update(fopen=fopen, fread=fread, fseek=fseek, ftell=lambda q:files[q.reg(0)][1],
                  fclose=lambda q:0, feof=lambda q:int(files[q.reg(0)][1]>=len(files[q.reg(0)][0])),
                  ferror=lambda q:0)
    m.libc['__aeabi_memcpy4']=m.libc['__aeabi_memcpy']
    m.libc['__aeabi_memcpy8']=m.libc['__aeabi_memcpy']
    m.libc['fseeko']=fseek
    m.libc['ftello']=m.libc['ftell']
    m.libc['ceilf']=lambda q:struct.unpack('<I',struct.pack('<f',float(math.ceil(struct.unpack('<f',struct.pack('<I',q.reg(0)))[0]))))[0]
    m.relocate()
    if heap_pad: m.malloc(heap_pad)
    loader,container,rng=m.malloc(0x280),m.malloc(0x2c),m.malloc(0x9d8)
    m.wr(loader,bytes(0x280)); m.wr(rng,bytes(0x9d8))
    m.call(B+0x58b485,container,rng,count=1000000)
    m.w32(loader+0x60,container)
    # loader+0x58=NULL selects the shipped "no canned lookup" branch (0x58c55a).
    paths=([prior] if prior else [])+['Bored/ag_bored.json']
    strings=[cppstr(m,str(DIR/p).encode()) for p in paths]
    events=[]; writes={}; current={'file':None,'entry':0,'targets':None}
    def write(uc, access, addr, size, value, data):
        pc=uc.reg_read(UC_ARM_REG_PC)
        if STACK<=addr<STACK+STACK_SIZE:
            for i in range(size):
                writes[addr+i]=dict(pc=hex(pc),address=hex(addr),size=size,value=hex(value & ((1<<(size*8))-1)),file=current['file'])
    def observe(uc, addr, size, data):
        if addr==B+0x58c044:
            events.append(dict(kind='lookup-save',file=current['file'],sp_before=hex(m.sp()),r7=hex(m.reg(7)),r8=hex(m.reg(8)),lr=hex(m.uc.reg_read(UC_ARM_REG_LR))))
        if addr==B+0x58a5f2:
            sp=m.sp(); current['targets']=[sp+0x40,sp+0x44]; current['entry']=0
            events.append(dict(kind='group-entry',file=current['file'],sp=hex(sp),words=[hex(m.r32(a)) for a in current['targets']]))
        if addr==B+0x58a706:
            targets=current['targets']
            events.append(dict(kind='copy',file=current['file'],entry=current['entry'],sp=hex(m.sp()),
                words=[hex(m.r32(a)) for a in targets],last_writes=[[writes.get(a+i) for i in range(4)] for a in targets]))
            current['entry']+=1
    m.uc.hook_add(UC_HOOK_MEM_WRITE,write,begin=STACK,end=STACK+STACK_SIZE-1)
    m.uc.hook_add(UC_HOOK_CODE,observe,begin=B+0x58a5ec,end=B+0x58a70c)
    m.uc.hook_add(UC_HOOK_CODE,observe,begin=B+0x58c044,end=B+0x58c044)
    m.poison_stack()  # exactly once; prior and target jobs reuse all stack bytes
    outcomes=[]
    for path,s in zip(paths,strings):
        current['file']=path
        try:
            ret=m.call(B+0x521d7d,loader,s,count=5000000); outcome='returned'; result=hex(ret)
        except Exception as e:
            outcome=str(e); result=None
        outcomes.append(dict(file=path,outcome=outcome,result=result,pc=hex(m.uc.reg_read(UC_ARM_REG_PC)),lr=hex(m.uc.reg_read(UC_ARM_REG_LR))))
        if outcome!='returned': break
    return dict(poison=hex(poison),prior=prior,heap_pad=heap_pad,loader=hex(loader),container=hex(container),io=io,outcomes=outcomes,events=events)

if __name__=='__main__':
    dump_native()
    results=[]
    for args in [(0,None,0),(0xa5,None,0),(0xa5,'audio/ag_audio_test.json',0),(0xa5,None,0x10000)]:
        r=run(*args); results.append(r)
        print(json.dumps({k:v for k,v in r.items() if k!='events'}),flush=True)
        OUT.write_text(json.dumps(results,indent=2)+'\n')
