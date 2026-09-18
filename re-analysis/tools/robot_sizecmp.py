import re,sys,json,lief,capstone
SP=sys.argv[1]
so=lief.ELF.parse("resources/lib/armeabi-v7a/libcozmoEngine.so"); raw=open("resources/lib/armeabi-v7a/libcozmoEngine.so","rb").read()
def va2off(va):
    for seg in so.segments:
        if seg.type==lief.ELF.Segment.TYPE.LOAD and seg.virtual_address<=va<seg.virtual_address+seg.virtual_size:
            return va-seg.virtual_address+seg.file_offset
md=capstone.Cs(capstone.CS_ARCH_ARM,capstone.CS_MODE_THUMB)
sizes={}
for l in open(f"{SP}/exports_demangled.txt",encoding="utf-8"):
    a,d=l.rstrip("\n").split(" ",1); a=int(a,16)&~1
    m=re.match(r"Anki::Cozmo::(?!ExternalInterface::|VizInterface::)(?:\w+::)?(?:OTA::)?(\w+)::Size\(\) const$",d)
    if not m: continue
    ins=list(md.disasm(raw[va2off(a):va2off(a)+64],a))
    val=None
    # trivial: movs r0,#imm ; bx lr   or movw r0,#imm ; bx lr
    if len(ins)>=2 and ins[0].mnemonic in("movs","movw","mov.w","mov") and ins[0].op_str.startswith("r0, #") and ins[1].mnemonic=="bx":
        val=int(ins[0].op_str.split("#")[1],0)
    k=m.group(1); sizes[k]=(val if val is not None else "variable") if (k not in sizes or "RobotInterface" in d) else sizes[k]
tags=json.load(open(f"{SP}/robot_tags_official_named.json"))
pyc=json.load(open(f"{SP}/pycozmo_packets.json"))
rows=[]
for u in ("EngineToRobot","RobotToEngine"):
    for t,(member,typ) in tags[u].items():
        t=int(t); typ=typ.replace("&&",""); osz=sizes.get(typ,"?"); p=pyc.get(f"{t:02x}")
        rows.append((t,u,member,typ,osz,p["name"] if p else "-",(p["size"] if p and not p["variable"] else ("var" if p else "-")), p["unknown_args"] if p else 0))
json.dump(rows,open(f"{SP}/robot_msg_compare.json","w"),indent=0)
print("official structs sized:",sum(1 for v in sizes.values() if v!="variable"),"variable:",sum(1 for v in sizes.values() if v=="variable"))
print(f"{'tag':4} {'official member':28} {'official type':28} {'osz':>5} | {'pycozmo':26} {'psz':>5} unk")
for r in rows:
    flag=""
    if r[5]=="-": flag="  <-- MISSING in PyCozmo"
    elif r[4]!="variable" and r[6]!="var" and r[4]!=r[6]: flag="  <-- SIZE MISMATCH"
    print(f"0x{r[0]:02x} {r[2]:28} {r[3]:28} {str(r[4]):>5} | {r[5]:26} {str(r[6]):>5} {r[7]}{flag}")
pyc_only=[k for k in pyc if not k.startswith('type_') and int(k,16) not in {int(t) for u in tags for t in tags[u]}]
print("PyCozmo-only IDs:",pyc_only)
