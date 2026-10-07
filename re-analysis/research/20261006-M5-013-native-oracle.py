"""Execute shipped keyframe constructor/list-copy/IsDone primitive in Unicorn.
Only phone allocation/memclr imports are serviced. Not an end-to-end producer trace.
Local dependency: python -m pip install --target re-analysis/research/_m5013_dependencies unicorn==2.1.4
"""
from pathlib import Path
import sys,struct,json,hashlib
root=Path(__file__).resolve().parents[2]
out=root/'re-analysis/research';sys.path.insert(0,str(out/'_m5013_dependencies'))
import lief
from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE,UC_HOOK_MEM_WRITE
from unicorn.arm_const import UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3,UC_ARM_REG_SP,UC_ARM_REG_LR,UC_ARM_REG_PC
binary=root/'resources/lib/armeabi-v7a/libcozmoEngine.so'
elf=lief.parse(str(binary))
uc=Uc(UC_ARCH_ARM,UC_MODE_ARM)
pages=set()
for s in elf.segments:
    if s.type!=lief.ELF.Segment.TYPE.LOAD:continue
    lo=s.virtual_address&~4095;hi=(s.virtual_address+s.virtual_size+4095)&~4095
    for p in range(lo,hi,4096):
        if p not in pages:uc.mem_map(p,4096);pages.add(p)
    uc.mem_write(s.virtual_address,bytes(s.content))
for a in [0x20000000,0x21000000,0x22000000,0x23000000,0x24000000]:uc.mem_map(a,0x100000)
uc.mem_map(0x20100000,0xF00000)
uc.mem_map(0x22100000,0xF00000)
stub={};alloc=0x20000000;calls=[];phase='initialization';watched={0x22000028:'source flag'};writes=[];recent=[]
def put(a,v):uc.mem_write(a,struct.pack('<I',v&0xffffffff))
def word(a):return struct.unpack('<I',uc.mem_read(a,4))[0]
for r in elf.relocations:
    if r.type.name in ['ARM_ABS32','ARM_GLOB_DAT','ARM_JUMP_SLOT'] and r.has_symbol:
        value=r.symbol.value
        if not value:
            name=r.symbol.name
            if name not in stub:stub[name]=0x24000000+4*len(stub)
            value=stub[name]
        if r.type.name=='ARM_ABS32':value+=word(r.address)
        put(r.address,value)
byaddr={v:k for k,v in stub.items()}
for a in byaddr:uc.mem_write(a,struct.pack('<I',0xE12FFF1E)) # ARM bx lr, preserving real interworking/IT return.
def hook(uc,a,size,data):
    global alloc
    recent.append(a)
    if len(recent)>100:recent.pop(0)
    if a==0x23000000:uc.emu_stop();return
    if a not in byaddr:return
    name=byaddr[a];args=[uc.reg_read(r) for r in [UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2]]
    calls.append({'import':name,'args':args})
    if name=='__aeabi_memclr4':uc.mem_write(args[0],bytes(args[1]))
    elif name in ['__aeabi_memcpy','__aeabi_memmove']:uc.mem_write(args[0],bytes(uc.mem_read(args[1],args[2])))
    elif name=='memchr':
        data=bytes(uc.mem_read(args[0],args[2]));position=data.find(bytes([args[1]&255]))
        uc.reg_write(UC_ARM_REG_R0,0 if position<0 else args[0]+position)
    elif name=='_Znwj':
        p=alloc;alloc+=(args[0]+15)&~15
        assert alloc<0x21000000
        uc.mem_write(p,bytes(args[0]));uc.reg_write(UC_ARM_REG_R0,p)
        if args[0]==0x34:watched[p+0x30]='copied node flag'
    elif name=='_ZdlPv':pass
    else:
        (out/'20261006-M5-013-oracle-diagnostic.json').write_text(json.dumps({'phase':phase,'import':name,'recent_pcs':[hex(x) for x in recent],'recent_imports':calls[-12:],'object_vtable':hex(word(0x22000000))},indent=2)+'\n',encoding='utf-8')
        raise RuntimeError('Unserviced import on native path: '+name)
    # Execute the stub's real bx lr instead of redirecting PC from a code hook;
    # direct PC injection skips Unicorn's conditional-call/interworking bookkeeping.
uc.hook_add(UC_HOOK_CODE,hook)
def memory_write(uc,access,address,size,value,data):
    for p,label in watched.items():
        if address<=p<address+size:writes.append({'phase':phase,'pc':hex(uc.reg_read(UC_ARM_REG_PC)),'address':hex(address),'size':size,'value':value,'field':label})
uc.hook_add(UC_HOOK_MEM_WRITE,memory_write)
def run(a,r0,r1=0,r2=0):
    uc.reg_write(UC_ARM_REG_SP,0x210F0000)
    uc.reg_write(UC_ARM_REG_LR,0x23000001)
    for r,v in [(UC_ARM_REG_R0,r0),(UC_ARM_REG_R1,r1),(UC_ARM_REG_R2,r2)]:uc.reg_write(r,v)
    uc.emu_start(a|1,0x23000000,count=100000)
    assert uc.reg_read(UC_ARM_REG_PC)&~1==0x23000000
    return uc.reg_read(UC_ARM_REG_R0)
obj=0x22000000;name=0x22000100
uc.mem_write(obj,bytes([0xA5])*0x2C)
uc.mem_write(name,b'\x08test\x00'+bytes(6))
phase='constructor';run(0x564290,obj,name)
constructor={'flag':uc.mem_read(obj+0x28,1)[0],'index':word(obj+0x24),'vector_header':[word(obj+i) for i in [0x18,0x1C,0x20]]}
assert constructor=={'flag':0,'index':0,'vector_header':[0,0,0]}
cases=[]
for flag in [0,1]:
    for payload in [b'',b'\x00\x81\xff\x7f']:
        uc.mem_write(obj+0x28,bytes([flag]));put(obj+0x24,7)
        p=0x22000400
        uc.mem_write(p,payload or b'\x00')
        for off,value in zip([0x18,0x1C,0x20],[p,p+len(payload),p+len(payload)]):put(obj+off,value)
        head=0x22000200;put(head,head);put(head+4,head);put(head+8,0)
        phase=f'list-copy flag={flag} length={len(payload)}';run(0x5645B0,head,obj)
        node=word(head);dst=node+8
        actual={'flag':uc.mem_read(dst+0x28,1)[0],'index':word(dst+0x24),'payload':bytes(uc.mem_read(word(dst+0x18),word(dst+0x1C)-word(dst+0x18))).hex()}
        assert actual=={'flag':flag,'index':7,'payload':payload.hex()}
        cases.append({'input_flag':flag,'input_payload':payload.hex(),'native_copy':actual})
assign_cases=[]
for flag in [0,1]:
    for capacity,payload in [(0,b'abc'),(8,b'xy'),(4,b'longer payload'),(4,b'')]:
        for off in [0x18,0x1C,0x20]:put(obj+off,0)
        uc.mem_write(obj+0x28,bytes([flag]));put(obj+0x24,7)
        phase=f'vector-assign flag={flag} initial-capacity={capacity} length={len(payload)}'
        if capacity:
            run(0x4FC5A6,obj+0x18,capacity)
            p=word(obj+0x18);uc.mem_write(p,bytes([0x5A])*capacity);put(obj+0x1C,p+capacity)
        p=0x22000500;uc.mem_write(p,payload or b'\x00')
        run(0x4FC6F8,obj+0x18,p,p+len(payload))
        actual={'flag':uc.mem_read(obj+0x28,1)[0],'index':word(obj+0x24),'payload':bytes(uc.mem_read(word(obj+0x18),word(obj+0x1C)-word(obj+0x18))).hex()}
        assert actual=={'flag':flag,'index':7,'payload':payload.hex()}
        assign_cases.append({'initial_capacity':capacity,'input_flag':flag,'input_payload':payload.hex(),'native_result':actual})
uc.mem_write(obj+0x28,b'\x01')
phase='IsDone synthetic true';done=run(0x4F976C,obj)
assert done==0
asset_case=None;asset_cases=[];parse_failures=[];files_examined=0
def asset_field(data,t,n):
    v=t-struct.unpack_from('<i',data,t)[0];size=struct.unpack_from('<H',data,v)[0]
    if 4+2*n+2>size:return 0
    offset=struct.unpack_from('<H',data,v+4+2*n)[0];return t+offset if offset else 0
def asset_target(data,p):return p+struct.unpack_from('<I',data,p)[0]
def asset_tables(data,t,n):
    p=asset_field(data,t,n)
    if not p:return []
    v=asset_target(data,p);count=struct.unpack_from('<I',data,v)[0]
    return [asset_target(data,v+4+4*i) for i in range(count)]
def asset_string(data,t,n):
    s=asset_target(data,asset_field(data,t,n));length=struct.unpack_from('<I',data,s)[0]
    return data[s+4:s+4+length].decode('utf-8')
directory=root/'re-analysis/obb/assets/cozmo_resources/assets/animations'
for file in sorted(directory.glob('*.bin')):
    files_examined+=1
    data=file.read_bytes()
    try:
        for clip in asset_tables(data,struct.unpack_from('<I',data,0)[0],0):
            kp=asset_field(data,clip,1)
            if not kp:continue
            frames=asset_tables(data,asset_target(data,kp),5)
            if frames:
                for frame in frames:asset_cases.append({'file':file,'bytes':data,'clip':asset_string(data,clip,0),'frame':frame})
    except (struct.error,IndexError,UnicodeDecodeError) as error:
        parse_failures.append({'file':str(file.relative_to(root)).replace('\\','/'),'error':str(error)});continue
assert asset_cases,'No shipped FaceAnimation keyframe asset found'
factory_cases=[]
selected_cases=asset_cases if '--all-assets' in sys.argv else asset_cases[:1]
for asset_case in selected_cases:
    data=asset_case['bytes'];assert len(data)<0xFE0000
    base=0x22010000;uc.mem_write(base,data)
    head=0x22001000;put(head,head);put(head+4,head);put(head+8,0);put(head+12,head);uc.mem_write(head+16,b'\x00')
    context=0x22002000;uc.mem_write(context,b'\x0Aasset\x00'+bytes(5))
    watched={0x22000028:'source flag'}
    phase='shipped asset FlatBuffer track factory'
    factory_result=run(0x576678,head,base+asset_case['frame'],context)
    assert factory_result==0 and word(head+8)==1
    node=word(head);dst=node+8
    flag=uc.mem_read(dst+0x28,1)[0];index=word(dst+0x24)
    assert flag==0 and index==0
    string=dst+12;tag=uc.mem_read(string,1)[0]
    if tag&1: n=word(string+4);p=word(string+8)
    else:n=tag>>1;p=string+1
    native_name=bytes(uc.mem_read(p,n)).decode('utf-8')
    factory_cases.append({'asset':str(asset_case['file'].relative_to(root)).replace('\\','/'),'asset_sha256':hashlib.sha256(data).hexdigest(),'clip':asset_case['clip'],'flatbuffer_table_offset':hex(asset_case['frame']),'return':factory_result,'node_flag':flag,'node_index':index,'native_normalized_name':native_name})
    # End this independent list's node lifetimes using the native clear routine.
    phase='factory case native cleanup';run(0x523AB8,head)
factory=factory_cases[0]
assert all((w['phase'].startswith('list-copy') or w['phase']=='shipped asset FlatBuffer track factory') and w['pc']=='0x564616' for w in writes)
result={'engine_sha256':hashlib.sha256(binary.read_bytes()).hexdigest(),'scope':'Native primitives and shipped-asset FlatBuffer track-factory paths; synthetic true flag tests copy preservation and branch consumption, never an enabling producer. Phone import shims are recorded below. CPU write hook excludes Python fixture setup and shim memset; those operations are explicit in the script/import log.','constructor':constructor,'copy_cases':cases,'vector_assign_cases':assign_cases,'cpu_flag_writes':writes,'is_done_when_flag_true':done,'shipped_asset_factory':factory,'asset_files_examined':files_examined,'asset_parse_failures':parse_failures,'asset_corpus_available_cases':len(asset_cases),'asset_corpus_executed_cases':len(factory_cases),'shipped_asset_factory_cases':factory_cases,'imports_serviced':calls}
(out/'20261006-M5-013-native-oracle-result.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(f'Native checks passed, including {len(factory_cases)}/{len(asset_cases)} shipped sprite keyframe factory cases; no global producer-absence claim.')
