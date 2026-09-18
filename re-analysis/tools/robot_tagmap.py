import re,sys,json,lief,capstone,collections
SP=sys.argv[1]
so=lief.ELF.parse("resources/lib/armeabi-v7a/libcozmoEngine.so"); raw=open("resources/lib/armeabi-v7a/libcozmoEngine.so","rb").read()
def va2off(va):
    for seg in so.segments:
        if seg.type==lief.ELF.Segment.TYPE.LOAD and seg.virtual_address<=va<seg.virtual_address+seg.virtual_size:
            return va-seg.virtual_address+seg.file_offset
md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
valid=json.load(open(f"{SP}/robot_tags_official.json"))
valid={u:set(int(t) for t in d) for u,d in valid.items()}
setters={}
for l in open(f"{SP}/exports_demangled.txt",encoding="utf-8"):
    a,d=l.rstrip("\n").split(" ",1); a=int(a,16)&~1
    m=re.match(r"Anki::Cozmo::RobotInterface::(EngineToRobot|RobotToEngine)::Set_(\w+)\((.*)\)$",d)
    if m: setters.setdefault(m.group(1),{})[m.group(2)]=(a,m.group(3))
res={}
for u,ss in setters.items():
    res[u]={}
    for name,(a,ptype) in ss.items():
        code=raw[va2off(a):va2off(a)+200]; imms=collections.Counter()
        for ins in md.disasm(code,a):
            if ins.mnemonic in("cmp","movs","mov","mov.w","cmp.w","movw") and "#" in ins.op_str:
                try: v=int(ins.op_str.split("#")[1],0)
                except: continue
                if v in valid[u]: imms[v]+=1
            if ins.mnemonic in("pop","pop.w","bx") : break
        tag=imms.most_common(1)[0][0] if imms else None
        res[u][name]=(tag,ptype.split("::")[-1].replace(" const&",""))
    print(u,"setters:",len(ss),"tagged:",sum(1 for v in res[u].values() if v[0] is not None))
# invert
final={u:{v[0]:(n,v[1]) for n,v in d.items() if v[0] is not None} for u,d in res.items()}
json.dump(final,open(f"{SP}/robot_tags_official_named.json","w"),indent=1)
for u in final:
    print("==",u,"=="); print(" ".join(f"{t:02x}:{n}({ty})" for t,(n,ty) in sorted(final[u].items())))
    dup=collections.Counter(v[0] for v in res[u].values()); print("dup tags:",[t for t,c in dup.items() if c>1 and t is not None])
