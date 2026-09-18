import sys,re,struct,collections,statistics,json
TYPES={1:"ConnReq",2:"ConnResp",3:"Disconnect",4:"SingleRel",5:"SingleUnrel",6:"MultiPart",7:"MultRel",8:"MultUnrel",9:"MultMixed",10:"Ack",11:"Ping"}
UNREL={5,7,8,9,10,11}
cat=json.load(open(sys.argv[2]))  # tag catalog: {"0xF0":"State",...}
def parse(raw):
    if raw[:7]!=b"COZ\x03RE\x01": return None
    t=raw[7]; smin,smax,ack=struct.unpack_from("<HHH",raw,8); body=raw[14:]
    subs=[]
    if t in (7,8,9):
        seq=smin; o=0
        while o<len(body):
            st=body[o]; sz=struct.unpack_from("<H",body,o+1)[0]; o+=3
            rel=(smin or smax) and st not in UNREL
            subs.append((st,seq if rel else 0,body[o:o+sz])); o+=sz
            if rel: seq=1 if seq==65534 else seq+1
    else: subs.append((t,smin,body))
    return t,smin,smax,ack,subs
def analyze(path):
    from datetime import datetime
    rows=[]
    for line in open(path,encoding="utf-8-sig"):
        m=re.match(r"(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)",line.strip())
        if not m: continue
        ts=datetime.fromisoformat(m.group(1).replace("Z","+00:00")).timestamp()
        rows.append((ts,m.group(2),bytes.fromhex(m.group(3).replace(" ",""))))
    t0=rows[0][0]
    print(f"== {path}: {len(rows)} frames, {rows[-1][0]-t0:.1f}s, TX={sum(1 for r in rows if r[1]=='TX')} RX={sum(1 for r in rows if r[1]=='RX')}")
    ftypes=collections.Counter(); stypes=collections.Counter(); tags=collections.Counter(); sizes={"TX":[],"RX":[]}
    rx_times=[]; pings_rx=[]; ack_lat={}; tx_rel={}; rx_seqs=[]; dup_frames=0; seen_rx_ranges=collections.Counter(); fw=None; unknown_tags=collections.Counter(); traces=[]
    for ts,d,raw in rows:
        p=parse(raw)
        if not p: print("  undecodable",d,raw[:16].hex()); continue
        t,smin,smax,ack,subs=p
        ftypes[(d,TYPES.get(t,t))]+=1; sizes[d].append(len(raw))
        if d=="RX":
            rx_times.append(ts)
            if smin: seen_rx_ranges[(smin,smax)]+=1
            for s,seq,pl in subs:
                if s in(4,5) and pl: tags[(TYPES[s],f"0x{pl[0]:02x} {cat.get(f'0x{pl[0]:02X}','?')}")]+=1
                if s==11 and len(pl)>=17: pings_rx.append((ts,struct.unpack_from("<d",pl)[0],struct.unpack_from("<II",pl,8),pl[16]))
                if s in(4,5) and pl and pl[0]==0xee: fw=pl
                if s in(4,5) and pl and pl[0]==0xb0: traces.append(pl[1:])
            for s,seq,pl in subs: stypes[("RX",TYPES.get(s,s))]+=1
            # ack latency for our reliable sends
            for q,(tq) in list(tx_rel.items()):
                if ack and ((q<=ack) if ack>=q else False): ack_lat[q]=ts-tq; tx_rel.pop(q)
        else:
            for s,seq,pl in subs:
                stypes[("TX",TYPES.get(s,s))]+=1
                if seq: tx_rel.setdefault(seq,ts)
    print("  frame types:",dict(sorted(ftypes.items())))
    print("  sub-message types:",dict(sorted(stypes.items())))
    print("  RX CLAD tags:",dict(sorted(tags.items(),key=lambda x:-x[1])))
    print(f"  frame sizes: TX max {max(sizes['TX'])} RX max {max(sizes['RX'])} RX mean {statistics.mean(sizes['RX']):.0f}")
    dts=[b-a for a,b in zip(rx_times,rx_times[1:])]; print(f"  RX inter-arrival: mean {statistics.mean(dts)*1000:.1f}ms median {statistics.median(dts)*1000:.1f}ms max {max(dts)*1000:.0f}ms")
    dup=[k for k,v in seen_rx_ranges.items() if v>1]; print(f"  RX reliable ranges seen more than once (robot resends): {len(dup)} e.g. {dup[:5]}")
    print(f"  ack latency for our reliable seqs (ms): {{ {', '.join(f'{k}:{v*1000:.0f}' for k,v in sorted(ack_lat.items()))} }}  unacked: {list(tx_rel)}")
    isr=collections.Counter(p[3] for p in pings_rx); print(f"  robot pings: {len(pings_rx)} isReply values {dict(isr)}; sample {pings_rx[:2]}")
    if fw:
        print(f"  FirmwareVersion raw body ({len(fw)-1}B): first 8 bytes {fw[1:9].hex()} | u16@1={struct.unpack_from('<H',fw,1)[0]} byte@3={fw[3]:#x} json starts at {fw.find(b'{')} json len {len(fw)-fw.find(b'{')}")
    print(f"  Trace messages: {len(traces)}; first 3 raw: {[t.hex() for t in traces[:3]]}")
analyze(sys.argv[1])
