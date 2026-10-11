from pathlib import Path
import re,json
p=Path(__file__).parent
def read(name):
 t=(p/name).read_text(encoding="utf-8-sig");e={}
 assert "processed candidate windows" in t and t.rstrip().endswith("No exhaustive runtime writer absence.")
 for l in t.splitlines():
  if l.startswith("{"):
   x=json.loads(l);e[(x["address"],x["mode"],x["kind"],x["effective"])]=x
 return e,t
old,ot=read("20261010-sound-rate-frame-full-got-navigation.txt")
delta,dt=read("20261010-sound-rate-frame-movw-got-delta-navigation.txt")
count=lambda t:int(re.search(r"processed candidate windows (\d+)",t)[1])
r={"baseline":"808 literal seeds plus original MOVW filter", "delta":"809 omitted ARM/Thumb MOVW low-half seeds; disjoint from808 seeds", "baselineEvents":len(old),"deltaEvents":len(delta),"combinedEvents":len(old.keys()|delta.keys()),"baselineWindows":count(ot),"deltaWindows":count(dt),"combinedWindows":count(ot)+count(dt),"baselineCaps":ot.count("LIMIT local state cap"),"deltaCaps":dt.count("LIMIT local state cap"),"added":[delta[k] for k in sorted(delta.keys()-old.keys())],"sharedIdentities":len(delta.keys()&old.keys()),"limit":"Encoding navigation only; no caller/writer/alias absence or admitted input proof."}
(p/"20261010-sound-rate-frame-movw-got-delta-comparison.json").write_text(json.dumps(r,indent=2)+"\n",encoding="utf-8")
print({k:v for k,v in r.items() if k!="added"})
for e in r["added"]:print(e)
