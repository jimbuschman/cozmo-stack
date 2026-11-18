import argparse,base64,collections,hashlib,json,struct
from pathlib import Path

def messages(raw):
 if len(raw)<14 or raw[:7]!=b'COZ\x03RE\x01': return
 kind=raw[7];seq=struct.unpack_from('<H',raw,8)[0]
 if kind not in (7,8,9):
  yield kind,seq if kind in (1,2,3,4,6) else 0,raw[14:]
  return
 at=14
 while at<len(raw):
  if at+3>len(raw): raise ValueError('short subheader')
  kind,size=struct.unpack_from('<BH',raw,at);at+=3
  if at+size>len(raw): raise ValueError('short payload')
  rel=kind in (1,2,3,4,6) and seq!=0
  yield kind,seq if rel else 0,raw[at:at+size]
  if rel:seq=seq%65534+1
  at+=size

def scan(path):
 high=[None,None];seen=[set(),set()];partial=[bytearray(),bytearray()];next_part=[1,1]
 sent=sent_frames=played=played_frames=0
 peak=peak_frames=0;minimum=0
 recent=collections.deque();failures=[];feedback=0;repeats=0;starts=collections.Counter()
 first_feedback=None;last_feedback=None;last_send=None;last_start=last_ca=last_cb=None
 anomalies=collections.Counter();sizes=collections.Counter();values=collections.Counter();locations={};unsafe=set();prefix=[];digest=hashlib.sha256();lines=0
 def counts():return dict(streamed=sent,played=played,in_flight=sent-played,
                         streamed_frames=sent_frames,played_frames=played_frames,in_flight_frames=sent_frames-played_frames)
 with path.open('rb') as source:
  for lineno,line in enumerate(source,1):
   digest.update(line);lines=lineno
   if not line.strip():continue
   try:row=json.loads(line)
   except json.JSONDecodeError:
    anomalies['invalid_json']+=1;continue
   if 'rawBase64' in row:
    at=row['atMs'];direction=int(row['outbound']);raw=base64.b64decode(row['rawBase64'])
   elif 'hex' in row:
    at=row.get('t',row.get('tMs'));direction=int(row['dir']=='out');raw=bytes.fromhex(row['hex'])
   elif 'hexOmitted' in row:
    subs=row.get('subs',[])
    if row.get('dir')=='in' and subs and all(v.get('tag')=='0xF2' for v in subs):
     anomalies['omitted_camera_only_frame']+=1
    else:anomalies['omitted_relevant_frame']+=1
    continue
   else:anomalies['unknown_schema']+=1;continue
   try:parts=list(messages(raw))
   except ValueError:anomalies['invalid_frame']+=1;continue
   for kind,seq,payload in parts:
    if seq:
     previous=high[direction]
     absolute=seq if previous is None else previous+((seq-previous+32767)%65534-32767)
     if absolute in seen[direction]:repeats+=1;continue
     seen[direction].add(absolute);high[direction]=absolute if previous is None else max(previous,absolute)
    if kind in (1,2,3):
     anomalies['connection_kind_'+str(kind)]+=1
     continue
    if kind==6:
     if len(payload)<=2:anomalies['short_multipart']+=1;continue
     index,total=payload[:2]
     if index!=next_part[direction]:
      anomalies['multipart_out_of_order']+=1;continue
     if index==1:partial[direction].clear()
     partial[direction].extend(payload[2:]);next_part[direction]+=1
     if index!=total:continue
     payload=bytes(partial[direction]);partial[direction].clear();next_part[direction]=1
    elif kind not in (4,5):continue
    if not payload:continue
    tag=payload[0]
    if direction and 0x8e<=tag<=0x9b:
     sizes[(tag,len(payload))]+=1
     if path.parent.name=='20261118-075139-LLM-SESSION' and lineno==27390:
      prefix.append(dict(line=lineno,at_ms=at,before=counts(),size=len(payload)))
     sent+=len(payload);sent_frames+=tag in (0x8e,0x8f,0x9a)
     last_send=dict(line=lineno,at_ms=at,tag=tag,size=len(payload))
     if tag==0x9b and len(payload)>1:last_start=payload[1];starts[last_start]+=1
    elif not direction:
     if tag==0xf1 and len(payload)>=16:
      _,new_played,new_frames=struct.unpack_from('<Iii',payload,1)
      if first_feedback is None:first_feedback=dict(line=lineno,at_ms=at,bytes=new_played,frames=new_frames)
      if new_played<played or new_frames<played_frames:anomalies['feedback_decreased']+=1
      played,played_frames=new_played,new_frames;feedback+=1
      last_feedback=dict(line=lineno,at_ms=at,tag=payload[14],**counts())
     elif tag==0xca and len(payload)>1:last_ca=payload[1]
     elif tag==0xcb and len(payload)>1:last_cb=payload[1]
     elif tag==0xb0 and len(payload)>=9:
      fmt,name,level,n=struct.unpack_from('<IHbB',payload,1)
      if name==379 and len(payload)>=9+4*n:
       args=list(struct.unpack_from('<'+'i'*n,payload,9))
       unsafe.update(v for t,v in recent)
       failures.append(dict(line=lineno,at_ms=at,format_word=fmt,format_id=fmt&65535,args=args,
        **counts(),peak_last_100ms=max((v for t,v in recent if t>=at-100),default=sent-played),
        peak_last_500ms=max((v for t,v in recent if t>=at-500),default=sent-played),
        peak_last_1000ms=max((v for t,v in recent),default=sent-played),
        last_feedback=last_feedback,last_send=last_send,last_start_sent=last_start,last_ca=last_ca,last_cb=last_cb))
    if direction and 0x8e<=tag<=0x9b or not direction and tag==0xf1:
     outstanding=sent-played
     values[outstanding]+=1
     if outstanding>=7000 and outstanding not in locations:locations[outstanding]=dict(line=lineno,at_ms=at,**counts())
     peak=max(peak,outstanding);minimum=min(minimum,outstanding);peak_frames=max(peak_frames,sent_frames-played_frames)
     recent.append((at,outstanding))
     while recent and recent[0][0]<at-1000:recent.popleft()
 bad={k:n for k,n in anomalies.items() if n and k not in
      {'omitted_camera_only_frame','connection_kind_1','connection_kind_2','connection_kind_3'}}
 if bad or anomalies['connection_kind_2']>1:
  raise ValueError(f'{path}: unsupported/incomplete counter epoch: {dict(bad)}')
 if first_feedback and (first_feedback['bytes'] or first_feedback['frames']):
  raise ValueError(f'{path}: nonzero initial feedback, unknown prefix')
 return dict(bundle=path.parent.name,frames_sha256=digest.hexdigest(),lines=lines,feedback=feedback,
  first_feedback=first_feedback,peak_bytes=peak,minimum_bytes=minimum,peak_frames=peak_frames,final=counts(),
  buffer_full=failures,anomalies=dict(anomalies),reliable_repeats=repeats,
  start_messages=sum(starts.values()),critical_prefix=prefix,
  message_sizes={f'{tag:02x}:{size}':count for (tag,size),count in sorted(sizes.items())},
  occupancy_values=dict(sorted(values.items())),occupancy_locations=locations,unsafe_occupancy_values=sorted(unsafe))

def summarize(rows):
 usable=[r for r in rows if 'feedback' in r and r['feedback']]
 failures=[x for r in usable for x in r['buffer_full']]
 if not failures:raise ValueError('No BufferFull samples: cannot select a measured budget')
 floor=min(x['peak_last_500ms'] for x in failures)
 clean=[r for r in usable if not r['buffer_full']]
 values={int(v) for r in clean for v in r['occupancy_values']}
 frontier=max(v for v in values if 0<v<floor)
 margin=745+2 # Native AudioSample + StartOfAnimation; emu_anim_buffer.py verifies both sizes.
 policy=dict(failure_window_ms=500,lowest_failure_window_peak_bytes=floor,
  largest_clean_observed_below_failure_floor=frontier,margin_bytes=margin,
  margin_definition='one native AudioSample (745) plus StartOfAnimation (2), not firmware allocator overhead',
  selected_budget_bytes=frontier-margin,
  frontier_inputs=[dict(bundle=r['bundle'],**r['occupancy_locations'][frontier])
                   for r in clean if frontier in r['occupancy_locations']],
  clean_run_peak_max=max(r['peak_bytes'] for r in clean),
  buffer_full_count=len(failures),capture_count=len(rows),raw_capture_count=sum('feedback' in r for r in rows))
 for r in rows:
  values=r.pop('occupancy_values',None)
  r.pop('unsafe_occupancy_values',None);r.pop('occupancy_locations',None)
  if values:
   values=sorted((int(k),n) for k,n in values.items() if int(k)>0)
   total=sum(n for k,n in values)
   def quantile(q):
    target=max(1,int(total*q+0.999999));at=0
    for v,n in values:
     at+=n
     if at>=target:return v
    return None
   r['positive_occupancy_distribution']=dict(samples=total,p50=quantile(.5),p95=quantile(.95),p99=quantile(.99))
 return policy

def main():
 p=argparse.ArgumentParser(description='Measure serialized animation occupancy and BufferFull traces; no firmware allocator is inferred.')
 p.add_argument('root',type=Path);p.add_argument('output',type=Path);a=p.parse_args()
 rows=[]
 for d in sorted(a.root.iterdir()):
  if not d.is_dir() or not d.name[:8].isdigit():continue
  path=d/'frames.jsonl'
  if path.exists():
   row=scan(path);rows.append(row);print(row['bundle'],row['peak_bytes'],len(row['buffer_full']),flush=True)
  else:rows.append(dict(bundle=d.name,missing='frames.jsonl; occupancy unknown, excluded from budget selection'))
 policy=summarize(rows)
 result=dict(schema=1,input_root=str(a.root),policy_selection=policy,bundles=rows,
  boundary='observed serialized counter occupancy; no claim about firmware allocation, flush semantics or an unrecorded run',
  missing_inputs='The literal C:/Users/jbuschman/Downloads paths are unavailable here; this scans their available local hardware-bundle copies. Two older bundles contain no raw frames.')
 a.output.write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps(policy,indent=2))
if __name__=='__main__':main()
