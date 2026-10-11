from pathlib import Path
import json,re
p=Path(__file__).parent
def read(name):
 text=(p/name).read_text(encoding="utf-8-sig")
 events={}
 for line in text.splitlines():
  if line.startswith("{"):
   e=json.loads(line);events[(e["address"],e["mode"],e["kind"],e["effective"])]=e
 return events,text
old,ot=read("20261010-sound-quota-input-pointer-expansion.txt")
new,nt=read("20261010-sound-quota-address-fair-navigation.txt")
assert "processed candidate windows" in nt and nt.rstrip().endswith("No exhaustive runtime writer absence.")
r={"baseline":"668 EXEC-only read map, selected seed forms", "new":"811 original LOAD reads, full GOT, Thumb MOVW, width/flag/FIFO/CBZ local navigation", "baselineEvents":len(old),"newEvents":len(new),"baselineCaps":ot.count("LIMIT local state cap"),"newCaps":nt.count("LIMIT local state cap"),"baselineWindowCount":re.findall(r"processed candidate windows (\d+)",ot),"newWindowCount":re.findall(r"processed candidate windows (\d+)",nt),"added":[new[k] for k in sorted(new.keys()-old.keys())],"missing":[old[k] for k in sorted(old.keys()-new.keys())]}
(p/"20261010-sound-quota-address-fair-comparison.json").write_text(json.dumps(r,indent=2)+"\n",encoding="utf-8")
print({k:v for k,v in r.items() if k not in ("added","missing")})
for key in ("added","missing"):
 for e in r[key]:print(key,e)
