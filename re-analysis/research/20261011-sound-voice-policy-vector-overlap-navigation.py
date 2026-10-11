"""Only add vector-store extent observations to838's unchanged finite seeds.

No DSP arithmetic is evaluated. The838 file remains unchanged.
"""
from pathlib import Path
import hashlib, json

r=Path(__file__).resolve().parent
baseline=r/'20261011-sound-voice-policy-composed-store-navigation.py'
source=baseline.read_text(encoding='utf-8')
extension='''
scalar_extent=extent
def extent(i,env):
    if i.mnemonic.startswith(('vstm','vst1','vst2','vst3','vst4')):
        regs=[o for o in i.operands if o.type==ARM_OP_REG and md.reg_name(o.reg)[0] in 'dsq' and md.reg_name(o.reg)[1:].isdigit()]
        if not regs:return None
        width=0
        for o in regs:
            if o.vector_index>=0:
                try:width+=int(i.mnemonic.split('.')[-1])//8
                except ValueError:return None
            else:width+=8 if md.reg_name(o.reg).startswith('d') else 16 if md.reg_name(o.reg).startswith('q') else 4
        if i.mnemonic.startswith('vstm'):
            base=env.get(name(i.operands[0].reg))
            if base is None:return None
            w=int.from_bytes(i.bytes,'little');pbit=(w>>24)&1;up=(w>>23)&1
            return (base+(4 if pbit else 0) if up else base-width+(0 if pbit else 4)),width
        mem=next((o.mem for o in i.operands if o.type==ARM_OP_MEM),None)
        if mem is None or mem.index or name(mem.base) not in env:return None
        # NEON VST's :alignment annotation is Capstone mem.disp, not an offset.
        # Rn is the encoded initial destination; post-index updates follow the store.
        w=int.from_bytes(i.bytes,'little')
        assert name(mem.base)=='r'+str((w>>16)&15)
        return env[name(mem.base)],width
    return scalar_extent(i,env)
'''
marker='events={}; seeds=0; bounded=[]'
assert source.count(marker)==1
source=source.replace(marker,extension+'\n'+marker)
source=source.replace("Path(__file__).with_suffix('.json').write_text(json.dumps(out,indent=2)+'\\n',encoding='utf-8')",'')
ns={'__file__':str(Path(__file__).resolve())}
exec(compile(source,str(baseline),'exec'),ns)
out=ns['out']
old=json.loads(baseline.with_suffix('.json').read_text(encoding='utf-8'))
key=lambda x:(x['seed'],x['store'],x['relativeStart'],x['width'])
original={key(x) for x in old['events']}
assert original <= {key(x) for x in out['events']}
new=[x for x in out['events'] if key(x) not in original]
control=ns['decode'](0xA0B01C)
assert control is not None and control.mnemonic=='vst1.64'
assert ns['extent'](control,{'r13':0})==(0,16)
out.update(baselineModelSha256=hashlib.sha256(baseline.read_bytes()).hexdigest(),
           addedVectorEvents=new,
           nativeWidthControl=dict(address='00A0B01C',bytes=control.bytes.hex(),
                                   instruction=control.mnemonic+' '+control.op_str,
                                   actualBase='SP',relativeStart=0,width=16,
                                   boundary='Encoded width and actual SP destination only, not a policy-writer admission witness.'),
           limit='Same838 finite seeds/windows and limitations, with only VST1/2/3/4 and VSTM destination extents added. Vector contents/arithmetic are not traced. Lane widths follow encoded element size; unmodeled/conditional/branch/call/indexed/writeback/receiver and other-mode paths remain unproved. No absence inference.')
Path(__file__).with_suffix('.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(addedVectorEvents=new)))
